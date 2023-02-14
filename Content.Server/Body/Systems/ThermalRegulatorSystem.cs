using Content.Server.Body.Components;
using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared.ActionBlocker;

namespace Content.Server.Body.Systems;

public sealed class ThermalRegulatorSystem : EntitySystem
{
    [Dependency] private readonly TemperatureSystem _tempSys = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSys = default!;

    public override void Update(float frameTime)
    {
        foreach (var regulator in EntityManager.EntityQuery<ThermalRegulatorComponent>())
        {
            ProcessThermalRegulation(regulator.Owner, regulator, frameTime);
        }
    }

    /// <summary>
    /// Processes thermal regulation for a mob
    /// </summary>
    private void ProcessThermalRegulation(EntityUid uid, ThermalRegulatorComponent comp, float dt)
    {
        if (!EntityManager.TryGetComponent(uid, out TemperatureComponent? temperatureComponent)) return;

        var dT = comp.NormalBodyTemperature - temperatureComponent.CurrentTemperature;
        var dQ = comp.Gain * dT * dt;
        var dQMax = comp.ImplicitHeatRegulation;
        var dQMin = -comp.ImplicitHeatRegulation;
        if (_actionBlockerSys.CanSweat(uid))
            dQMin -= comp.SweatHeatRegulation;
        if (_actionBlockerSys.CanShiver(uid))
            dQMax += comp.ShiveringHeatRegulation;
        if (dQ > dQMax)
            dQ = dQMax;
        else if (dQ < dQMin)
            dQ = dQMin;
        _tempSys.ChangeHeat(uid, dQ, true, temperatureComponent);
    }
}
