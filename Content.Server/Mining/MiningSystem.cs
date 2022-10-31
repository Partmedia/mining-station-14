using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Mining.Components;
using Content.Shared.Atmos;
using Content.Shared.Destructible;
using Content.Shared.Mining;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Mining;

/// <summary>
/// This handles creating ores when the entity is destroyed.
/// </summary>
public sealed class MiningSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    private static readonly Gas[] LeakableGases =
    {
        Gas.Miasma,
        Gas.Plasma,
        Gas.Tritium,
        Gas.Frezon,
    };

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OreVeinComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OreVeinComponent, DestructionEventArgs>(OnDestruction);
    }

    private void OnDestruction(EntityUid uid, OreVeinComponent component, DestructionEventArgs args)
    {
        var coords = Transform(uid).Coordinates;
        int toSpawn = 0;
        if (component.CurrentOre != null)
        {
            var proto = _proto.Index<OrePrototype>(component.CurrentOre);
            if (proto.OreEntity != null)
            {
                toSpawn = _random.Next(proto.MinOreYield, proto.MaxOreYield);
                for (var i = 0; i < toSpawn; i++)
                {
                    Spawn(proto.OreEntity, coords.Offset(_random.NextVector2(0.3f)));
                }
            }
        }

        // Mining rocks sometimes emits a random gas.
        if (_random.NextFloat() < 0.1)
        {
            // FIXME: doesn't work because no gas mixture on tile with rock
            var atmosphereSystem = _entMan.EntitySysManager.GetEntitySystem<AtmosphereSystem>();
            var environment = atmosphereSystem.GetContainingMixture(uid, true, true) ?? GasMixture.SpaceGas;
            var gas = _random.Pick(LeakableGases);
            environment.AdjustMoles(gas, 20);
        }

        // Spawn ordinary rock
        int rest = 5 - toSpawn;
        if (rest < 1)
            return;

        for (var i = 0; i < rest; i++)
        {
            Spawn("RockOre", coords.Offset(_random.NextVector2(0.3f)));
        }
    }

    private void OnMapInit(EntityUid uid, OreVeinComponent component, MapInitEvent args)
    {
        if (component.CurrentOre != null || component.OreRarityPrototypeId == null || !_random.Prob(component.OreChance))
            return;

        component.CurrentOre = _proto.Index<WeightedRandomPrototype>(component.OreRarityPrototypeId).Pick(_random);
    }
}
