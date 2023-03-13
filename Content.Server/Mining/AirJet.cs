using Content.Server.Atmos.EntitySystems;
using Content.Server.Construction;
using Content.Server.NodeContainer.Nodes;
using Content.Server.NodeContainer;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Mining;

[RegisterComponent]
public class AirJetComponent : Component
{
    [DataField("rate")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Rate = 1f; //< Ores per second

    [ViewVariables(VVAccess.ReadWrite)]
    public float Upgrade = 1f; //< Upgrade multiplier

    [ViewVariables(VVAccess.ReadWrite)]
    public float Accum = 0f;

    [DataField("forcePressureRatio")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float ForcePressureRatio = 60f/(4*Atmospherics.OneAtmosphere); // N/kPa

    [DataField("volume")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Volume = 50f; // volume of gas to remove after each jet

    [DataField("targetDist")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float TargetDist = 1f;

    [DataField("range")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Range = 0.2f;
}

public class AirJetSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _sharedAudioSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirJetComponent, UpgradeExamineEvent>(OnUpgradeExamine);
        SubscribeLocalEvent<AirJetComponent, RefreshPartsEvent>(OnRefreshParts);
    }

    private void OnUpgradeExamine(EntityUid uid, AirJetComponent comp, UpgradeExamineEvent args)
    {
        args.AddPercentageUpgrade("lathe-component-upgrade-speed", comp.Upgrade);
    }

    private void OnRefreshParts(EntityUid uid, AirJetComponent comp, RefreshPartsEvent args)
    {
        var rating = args.PartRatings["Manipulator"];
        //comp.Upgrade = rating;
    }

    public override void Update(float frameTime)
    {
        foreach (var (comp, apc, nodeContainer) in EntityManager.EntityQuery<AirJetComponent, ApcPowerReceiverComponent, NodeContainerComponent>())
        {
            if (!apc.Powered || !nodeContainer.TryGetNode("pipe", out PipeNode? inlet))
            {
                // Not powered, don't do anything
                comp.Accum = 0;
                continue;
            }
            
            float incr = 1/(comp.Rate * comp.Upgrade);
            comp.Accum += frameTime;
            if (comp.Accum < incr)
                continue;

            comp.Accum -= incr;

            // do thing here
            var xformQuery = GetEntityQuery<TransformComponent>();
            if (!xformQuery.TryGetComponent(comp.Owner, out var myXform))
            {
                // We need our own transform in order to move things
                continue;
            }

            var environment = _atmosphereSystem.GetContainingMixture(comp.Owner, true, true);
            float envP = environment is null ? 0 : environment.Pressure;
            float dP = inlet.Air.Pressure - envP;

            if (dP < 0)
                return;

            var forceVec = (myXform.WorldRotation - Math.PI/2).ToVec();
            var tileInFront = myXform.WorldPosition + forceVec*comp.TargetDist;
            var coord = new MapCoordinates(tileInFront, myXform.MapID);

            foreach (var uid in _lookup.GetEntitiesInRange(coord, comp.Range))
            {
                // skip self
                if (uid == comp.Owner)
                    continue;

                if (_tagSystem.HasTag(uid, "Ore"))
                {
                    var force = forceVec * comp.ForcePressureRatio*dP;
                    _physics.ApplyLinearImpulse(uid, force);

                    // Only process one
                    break;
                }
            }

            // Release gas
            var released = inlet.Air.RemoveVolume(comp.Volume);
            if (environment != null) {
                _atmosphereSystem.Merge(environment, released);
            }
            _sharedAudioSystem.PlayPvs("/Audio/Items/hiss.ogg", comp.Owner);
        }
    }
}
