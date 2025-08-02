using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Materials;
using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Shared.Stacks;
using Content.Shared.Construction.Components;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.OuterRim.Generator;
using Robust.Server.GameObjects;

using Content.Server.Construction;
using Content.Server.Construction.Components;

namespace Content.Server.OuterRim.Generator;

/// <inheritdoc/>
public sealed class GeneratorSystem : SharedGeneratorSystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SharedGeneratorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SharedGeneratorComponent, SetTargetPowerMessage>(OnTargetPowerSet);
        SubscribeLocalEvent<SharedGeneratorComponent, SetAutoThrottleMessage>(OnAutoThrottleSet);

        SubscribeLocalEvent<SharedGeneratorComponent, RefreshPartsEvent>(OnRefreshParts);
        SubscribeLocalEvent<SharedGeneratorComponent, UpgradeExamineEvent>(OnUpgradeExamine);
    }

    private void OnUpgradeExamine(EntityUid uid, SharedGeneratorComponent component, UpgradeExamineEvent args)
    {
        args.AddPercentageUpgrade("upgrade-power-supply", component.Upgrade);
    }

    private void OnRefreshParts(EntityUid uid, SharedGeneratorComponent component, RefreshPartsEvent args)
    {
        var rating = args.PartRatings["Capacitor"];
        component.Upgrade = (rating-1)*0.5f + 1f;
    }

    private void OnTargetPowerSet(EntityUid uid, SharedGeneratorComponent component, SetTargetPowerMessage args)
    {
        component.TargetPower = args.TargetPower;
        component.AutoThrottleEnabled = false;
    }

    private void OnAutoThrottleSet(EntityUid uid, SharedGeneratorComponent component, SetAutoThrottleMessage args)
    {
        component.AutoThrottleEnabled = args.On;
    }

    private void OnInteractUsing(EntityUid uid, SharedGeneratorComponent component, InteractUsingEvent args)
    {
        if (!TryComp(args.Used, out MaterialComponent? mat) || !TryComp(args.Used, out StackComponent? stack) || EntityManager.IsQueuedForDeletion(args.Used))
            return;

        if (!mat.Materials.ContainsKey(component.FuelMaterial))
            return;

        component.RemainingFuel += stack.Count;
        QueueDel(args.Used);
        return;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (gen, supplier, xform) in EntityQuery<SharedGeneratorComponent, PowerSupplierComponent, TransformComponent>())
        {
            supplier.Enabled = !(gen.RemainingFuel <= 0.0f || xform.Anchored == false);

            if (supplier.Enabled)
            {
                if (gen.AutoThrottleEnabled)
                {
                    // Set max power to capacity of generator, then calculate throttle position based on ramped power.
                    var (_, _, maxPower) = CalcFuelPower(gen, 1);
                    supplier.MaxSupply = maxPower;
                    gen.TargetPower = CalcThrottlePos(gen, supplier.SupplyRampPosition);
                }

                var (fuelRate, energyIn, power) = CalcFuelPower(gen, gen.TargetPower);
                if (!gen.AutoThrottleEnabled)
                {
                    // Set max power to maximum allowed by current throttle position.
                    supplier.MaxSupply = power;
                }

                gen.Output = supplier.SupplyRampPosition;
                gen.Efficiency = gen.Output / energyIn;
                gen.RemainingFuel = MathF.Max(gen.RemainingFuel - (fuelRate * frameTime), 0.0f);

                // Release wasted energy as heat
                var environment = _atmosphereSystem.GetContainingMixture(gen.Owner, true, true);
                if (environment is not null)
                {
                    float dQ = (energyIn - gen.Output) * frameTime;
                    _atmosphereSystem.AddHeat(environment, dQ);
                }
            }
            else
            {
                supplier.MaxSupply = 0;
                gen.Output = 0;
                gen.Efficiency = 0;
            }

            UpdateUi(gen);
        }
    }

    private (float, float, float) CalcFuelPower(SharedGeneratorComponent gen, float throttle)
    {
        float fuelRate = throttle * gen.MaxFuelRate * gen.Upgrade;
        float energyIn = fuelRate * gen.FuelEnergy;
        float power = energyIn * CalcFuelEfficiency(throttle);
        return (fuelRate, energyIn, power);
    }

    /**
     * Calculate throttle position to generate power. Inverse of CalcFuelPower.
     */
    private float CalcThrottlePos(SharedGeneratorComponent gen, float power)
    {
        var c = power / (gen.MaxFuelRate * gen.Upgrade * gen.FuelEnergy);
        var t = (MathF.Sqrt(50*c+1)-1)/5;
        return Math.Clamp(t, 0, 1);
    }

    private static float CalcFuelEfficiency(float targetPower)
    {
        return (float)(targetPower/2 + 0.2);
    }

    private void UpdateUi(SharedGeneratorComponent comp)
    {
        if (!_uiSystem.IsUiOpen(comp.Owner, GeneratorComponentUiKey.Key))
            return;

        _uiSystem.TrySetUiState(comp.Owner, GeneratorComponentUiKey.Key, new GeneratorComponentBuiState(comp));
    }
}
