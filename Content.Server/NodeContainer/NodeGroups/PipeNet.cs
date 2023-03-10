using System.Linq;
using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.NodeContainer.NodeGroups
{
    public interface IPipeNet : INodeGroup, IGasMixtureHolder
    {
        /// <summary>
        ///     Causes gas in the PipeNet to react.
        /// </summary>
        void Update();
    }

    [NodeGroup(NodeGroupID.Pipe)]
    public sealed class PipeNet : BaseNodeGroup, IPipeNet
    {
        [ViewVariables] public GasMixture Air { get; set; } = new() {Temperature = Atmospherics.T20C};

        [ViewVariables] private float TotalVolume = 0f; //< Total physical volume containable in all the pipes

        [ViewVariables] public Solution Liquids { get; set; } = new();

        [ViewVariables] private AtmosphereSystem? _atmosphereSystem;

        [Dependency] private readonly IPrototypeManager _protoMan = default!;

        public EntityUid? Grid { get; private set; }

        public override void Initialize(Node sourceNode, IEntityManager entMan)
        {
            base.Initialize(sourceNode, entMan);

            Grid = entMan.GetComponent<TransformComponent>(sourceNode.Owner).GridUid;

            if (Grid == null)
            {
                // This is probably due to a cannister or something like that being spawned in space.
                return;
            }

            _atmosphereSystem = entMan.EntitySysManager.GetEntitySystem<AtmosphereSystem>();
            _atmosphereSystem.AddPipeNet(Grid.Value, this);
        }

        public void Update()
        {
            // Vaporize/condense gases. Assume that everything in the pipe instantly reaches
            // thermal equilibrium, i.e. gas temperature always equals liquid temperature.
            // This loop employs a continuation method to converge to the right final
            // temperature and gas/liquid balance. Math is required to understand it.
            const float uPerG = 0.005f; // FIXME: assume 5u per gram (1 g/mL, 1u=5 mL)
            float lastAirT;
            const int maxiter = 10;
            const float reltol = 5e-2f;

            if (_atmosphereSystem == null)
                return;

            for (int iter = 0; iter < maxiter; iter++)
            {
                float alpha = 1 - 1f*iter/maxiter; // 1, 0.9, 0.8...
                lastAirT = Air.Temperature;
                float Hgas = _atmosphereSystem.GetHeatCapacity(Air);
                float Qgas = Hgas * Air.Temperature;
                float Hliquid = Liquids.GetHeatCapacity(_protoMan);
                float Qliquid = Hliquid * Liquids.Temperature;
                float Tfinal = (Qgas + Qliquid) / (Hgas + Hliquid);
                Air.Temperature = Tfinal;
                Liquids.Temperature = Tfinal;
                for (int i = 0; i < Atmospherics.TotalNumberOfGases; i++)
                {
                    var gasProto = _atmosphereSystem.GetGas(i);
                    if (gasProto.Reagent == null)
                        continue;
                    if (!_protoMan.TryIndex(gasProto.Reagent, out ReagentPrototype? liquidProto))
                        continue;
                    if (Tfinal < liquidProto.BoilingPoint + Atmospherics.T0C)
                    {
                        // Condense gases
                        float moles = Air.GetMoles(i);
                        float adjMoles = moles * alpha;
                        var qty = adjMoles * gasProto.MolarMass * uPerG;
                        Air.SetMoles(i, moles - adjMoles);
                        if (qty > 0)
                            Liquids.AddReagent(gasProto.Reagent, qty);
                    }
                    else
                    {
                        // Boil liquids
                        float qty = (float)Liquids.GetReagentQuantity(gasProto.Reagent);
                        float adjQty = qty * alpha;
                        float moles = adjQty / uPerG / gasProto.MolarMass;
                        Liquids.RemoveReagent(gasProto.Reagent, adjQty);
                        Air.AdjustMoles(i, moles);
                    }
                }
                if (MathF.Abs(Tfinal - lastAirT)/lastAirT < reltol)
                    break;
            }
            
            //Air.Volume = TotalVolume - (float)Liquids.Volume/1000*uPerG;

            _atmosphereSystem?.React(Air, this);
        }

        public override void LoadNodes(List<Node> groupNodes)
        {
            base.LoadNodes(groupNodes);

            foreach (var node in groupNodes)
            {
                var pipeNode = (PipeNode) node;
                TotalVolume += pipeNode.Volume;
                Air.Volume = TotalVolume;
            }
        }

        public override void RemoveNode(Node node)
        {
            base.RemoveNode(node);

            // if the node is simply being removed into a separate group, we do nothing, as gas redistribution will be
            // handled by AfterRemake(). But if it is being deleted, we actually want to remove the gas stored in this node.
            if (!node.Deleting || node is not PipeNode pipe)
                return;

            Air.Multiply(1f - pipe.Volume / Air.Volume);
            TotalVolume -= pipe.Volume;
            Air.Volume = TotalVolume;
        }

        public override void AfterRemake(IEnumerable<IGrouping<INodeGroup?, Node>> newGroups)
        {
            RemoveFromGridAtmos();

            var newAir = new List<GasMixture>(newGroups.Count());
            var newLiquids = new List<Solution>(newGroups.Count());
            foreach (var newGroup in newGroups)
            {
                if (newGroup.Key is IPipeNet newPipeNet)
                {
                    newAir.Add(newPipeNet.Air);
                    newLiquids.Add(newPipeNet.Liquids);
                }
            }

            _atmosphereSystem?.DivideInto(Air, newAir);
            _atmosphereSystem?.DivideLiquidsInto(Liquids, newLiquids);
        }

        private void RemoveFromGridAtmos()
        {
            if (Grid == null)
                return;

            _atmosphereSystem?.RemovePipeNet(Grid.Value, this);
        }

        public override string GetDebugData()
        {
            return @$"Pressure: { Air.Pressure:G3}
            Temperature: {Air.Temperature:G3}
            Volume: {Air.Volume:G3}";
        }
    }
}
