using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Atmos;
using Content.Server.NodeContainer.Nodes;
using Content.Server.NodeContainer;
using Content.Shared.Atmos.Piping;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Shared.Interaction;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

[RegisterComponent]
public sealed class HeatExchangerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("inlet")]
    public string InletName { get; set; } = "inlet";

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("outlet")]
    public string OutletName { get; set; } = "outlet";

    /** Pipe conductivity (mols/kPa/sec). */
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("g")]
    public float G { get; set; } = 1f;

    /** Thermal convection coefficient (J/degK/sec). */
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("k")]
    public float K { get; set; } = 8000f;

    /** Thermal radiation coefficient. Number of "effective" tiles this
     * radiator radiates compared to superconductivity tile losses. */
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("alpha")]
    public float alpha { get; set; } = 400f;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("debug")]
    public bool Debug { get; set; } = false;
}

public sealed class HeatExchangerSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HeatExchangerComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
    }

    private void OnAtmosUpdate(EntityUid uid, HeatExchangerComponent comp, AtmosDeviceUpdateEvent args)
    {
        if (!EntityManager.TryGetComponent(uid, out NodeContainerComponent? nodeContainer)
                || !nodeContainer.TryGetNode(comp.InletName, out PipeNode? inlet)
                || !nodeContainer.TryGetNode(comp.OutletName, out PipeNode? outlet))
        {
            return;
        }

        // Positive dN flows from inlet to outlet
        var dt = 1/_atmosphereSystem.AtmosTickRate;
        var dP = inlet.Air.Pressure - outlet.Air.Pressure;
        var dN = comp.G*dP*dt;

        GasMixture xfer;
        if (dN > 0)
            xfer = inlet.Air.Remove(dN);
        else
            xfer = outlet.Air.Remove(-dN);

        var radTemp = Atmospherics.TCMB;

        // Convection
        var environment = _atmosphereSystem.GetContainingMixture(uid, true, true);
        if (environment != null)
        {
            radTemp = environment.Temperature;

            // Positive dT is from pipe to surroundings
            var dT = xfer.Temperature - environment.Temperature;
            var dE = comp.K * dT * dt;
            var envLim = Math.Abs(_atmosphereSystem.GetHeatCapacity(environment) * dT * dt);
            var xferLim = Math.Abs(_atmosphereSystem.GetHeatCapacity(xfer) * dT * dt);
            var dEactual = Math.Sign(dE) * Math.Min(Math.Abs(dE), Math.Min(envLim, xferLim));
            _atmosphereSystem.AddHeat(xfer, -dEactual);
            _atmosphereSystem.AddHeat(environment, dEactual);
            if (comp.Debug)
                Logger.InfoS("exchanger", $"({uid}) convect dN={dN} dT={dT} dE={dEactual} ({xferLim}, {envLim})");
        }

        // Radiation
        float dTR = xfer.Temperature - radTemp;
        float a0 = _cfg.GetCVar(CCVars.SuperconductionTileLoss) / MathF.Pow(Atmospherics.T20C, 4);
        float dER = comp.alpha * a0 * MathF.Pow(dTR, 4) * dt;
        _atmosphereSystem.AddHeat(xfer, -dER);
        if (comp.Debug)
            Logger.InfoS("exchanger", $"({uid}) radiate dN={dN} dTR={dTR} dER={dER}");

        if (dN > 0)
            _atmosphereSystem.Merge(outlet.Air, xfer);
        else
            _atmosphereSystem.Merge(inlet.Air, xfer);

    }
}
