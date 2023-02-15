using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Construction;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping;
using Content.Shared.Atmos.Piping.Unary.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server.Atmos.Piping.Unary.EntitySystems
{
    [UsedImplicitly]
    public sealed class GasThermoMachineSystem : EntitySystem
    {
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GasThermoMachineComponent, AtmosDeviceUpdateEvent>(OnThermoMachineUpdated);
            SubscribeLocalEvent<GasThermoMachineComponent, AtmosDeviceDisabledEvent>(OnThermoMachineLeaveAtmosphere);
            SubscribeLocalEvent<GasThermoMachineComponent, RefreshPartsEvent>(OnGasThermoRefreshParts);
            SubscribeLocalEvent<GasThermoMachineComponent, UpgradeExamineEvent>(OnGasThermoUpgradeExamine);

            // UI events
            SubscribeLocalEvent<GasThermoMachineComponent, GasThermomachineToggleMessage>(OnToggleMessage);
            SubscribeLocalEvent<GasThermoMachineComponent, GasThermomachineChangeTemperatureMessage>(OnChangeTemperature);
        }

        private void OnThermoMachineUpdated(EntityUid uid, GasThermoMachineComponent thermoMachine, AtmosDeviceUpdateEvent args)
        {
            if (!TryComp(uid, out ApcPowerReceiverComponent? receiver)
                    || !receiver.Powered
                    || !EntityManager.TryGetComponent(uid, out NodeContainerComponent? nodeContainer)
                    || !nodeContainer.TryGetNode(thermoMachine.InletName, out PipeNode? inlet))
            {
                DirtyUI(uid, thermoMachine);
                _appearance.SetData(uid, ThermoMachineVisuals.Enabled, false);
                return;
            }

            float dt = 1/_atmosphereSystem.AtmosTickRate;
            float dQ = receiver.Load * thermoMachine.Cf * dt;
            _atmosphereSystem.AddHeat(inlet.Air, dQ);
            _appearance.SetData(uid, ThermoMachineVisuals.Enabled, true);
        }

        private void OnThermoMachineLeaveAtmosphere(EntityUid uid, GasThermoMachineComponent component, AtmosDeviceDisabledEvent args)
        {
            _appearance.SetData(uid, ThermoMachineVisuals.Enabled, false);

            DirtyUI(uid, component);
        }

        private void OnGasThermoRefreshParts(EntityUid uid, GasThermoMachineComponent component, RefreshPartsEvent args)
        {
            var matterBinRating = args.PartRatings[component.MachinePartHeatCapacity];
            var laserRating = args.PartRatings[component.MachinePartTemperature];

            component.HeatCapacity = component.BaseHeatCapacity * MathF.Pow(matterBinRating, 2);

            switch (component.Mode)
            {
                // 593.15K with stock parts.
                case ThermoMachineMode.Heater:
                    component.MaxTemperature = component.BaseMaxTemperature + component.MaxTemperatureDelta * laserRating;
                    component.MinTemperature = Atmospherics.T20C;
                    break;
                // 73.15K with stock parts.
                case ThermoMachineMode.Freezer:
                    component.MinTemperature = MathF.Max(
                        component.BaseMinTemperature - component.MinTemperatureDelta * laserRating, Atmospherics.TCMB);
                    component.MaxTemperature = Atmospherics.T20C;
                    break;
            }

            DirtyUI(uid, component);
        }

        private void OnGasThermoUpgradeExamine(EntityUid uid, GasThermoMachineComponent component, UpgradeExamineEvent args)
        {
            switch (component.Mode)
            {
                case ThermoMachineMode.Heater:
                    args.AddPercentageUpgrade("gas-thermo-component-upgrade-heating", component.MaxTemperature / (component.BaseMaxTemperature + component.MaxTemperatureDelta));
                    break;
                case ThermoMachineMode.Freezer:
                    args.AddPercentageUpgrade("gas-thermo-component-upgrade-cooling", component.MinTemperature / (component.BaseMinTemperature - component.MinTemperatureDelta));
                    break;
            }
            args.AddPercentageUpgrade("gas-thermo-component-upgrade-heat-capacity", component.HeatCapacity / component.BaseHeatCapacity);
        }

        private void OnToggleMessage(EntityUid uid, GasThermoMachineComponent component, GasThermomachineToggleMessage args)
        {
            component.Enabled = !component.Enabled;

            DirtyUI(uid, component);
        }

        private void OnChangeTemperature(EntityUid uid, GasThermoMachineComponent component, GasThermomachineChangeTemperatureMessage args)
        {
            component.TargetTemperature =
                Math.Clamp(args.Temperature, component.MinTemperature, component.MaxTemperature);

            DirtyUI(uid, component);
        }

        private void DirtyUI(EntityUid uid, GasThermoMachineComponent? thermo, ServerUserInterfaceComponent? ui=null)
        {
            if (!Resolve(uid, ref thermo, ref ui, false))
                return;

            _userInterfaceSystem.TrySetUiState(uid, ThermomachineUiKey.Key,
                new GasThermomachineBoundUserInterfaceState(thermo.MinTemperature, thermo.MaxTemperature, thermo.TargetTemperature, thermo.Enabled, thermo.Mode), null, ui);
        }
    }
}
