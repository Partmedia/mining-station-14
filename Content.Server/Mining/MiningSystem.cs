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
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Shared.Damage;

namespace Content.Server.Mining;

/// <summary>
/// This handles creating ores when the entity is destroyed.
/// </summary>
public sealed class MiningSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;

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

    private void CaveInCheck(EntityUid uid, CaveInComponent component)
    {
        //get the support range of the mined rock
        //check for all entities in range
        //if there are no walls or asteroid rocks in range, spawn rocks within the surrounding area
        var pos = Transform(uid).MapPosition;
        var xform = _entities.GetComponent<TransformComponent>(uid);
        var range = component.SupportRange;
        var supported = false;

        var box = Box2.CenteredAround(pos.Position, (range, range));
        var mapGrids = _mapManager.FindGridsIntersecting(pos.MapId, box).ToList();
        var grids = mapGrids.Select(x => x.Owner).ToList();

        List<Direction> directions = new List<Direction>();
        directions.Add(Direction.North);
        directions.Add(Direction.South);
        directions.Add(Direction.East);
        directions.Add(Direction.West);
        directions.Add(Direction.NorthEast);
        directions.Add(Direction.NorthWest);
        directions.Add(Direction.SouthEast);
        directions.Add(Direction.SouthWest);

        foreach (var grid in mapGrids)
        {
            //to be able to cave-in, the space in question must be surrounded by tiles (for balance reasons to prevent infinite mining, for in-game lets say the chunk has no ceiling because its too small)
            bool CheckSurroundSpace(Vector2i origin, Direction dir)
            {

                // Currently no support for spreading off or across grids.
                var index = origin + dir.ToIntVec();

                var hasSpace = true;
                if (!grid.TryGetTileRef(index, out var tile) || tile.Tile.IsEmpty)
                {
                    hasSpace = false;
                }

                return hasSpace;
            }

            var origin = grid.TileIndicesFor(xform.Coordinates);
            var hasSpace = true;

            foreach (var direction in directions)
            {
                hasSpace = CheckSurroundSpace(origin, direction);
                if (!hasSpace)
                    break;
            }
            if (!hasSpace)
                return;

            //cave-in prevention requires TWO supports on opposing sides (sort of like in jenga) 
            bool CheckSupportDirs(Vector2i origin, Direction dir1, Direction dir2, bool supported, int range, int count)
            {
                count++;

                if (!supported)
                {
                    // Currently no support for spreading off or across grids.
                    var index1 = origin + dir1.ToIntVec();
                    var index2 = origin + dir2.ToIntVec();

                    if (EntityManager.TryGetComponent<MetaDataComponent>(uid, out var caveIn))
                    {
                        var support1 = false;
                        foreach (var entity in _lookup.GetEntitiesIntersecting(grid.GridTileToLocal(index1)))
                        {
                            if (entity != uid)
                            {
                                if (EntityManager.TryGetComponent<CaveSupportComponent?>(entity, out var support))
                                    support1 = true;
                            }
                        }

                        //if there is nothing for support but the support range has not been fully expended, check if the support's support exists
                        if (!support1 && range > count)
                        {
                            //TODO maybe find a better way to do this... (compile directions in to a list, iterate through list) - I got a list now, just need to use it...
                            support1 = CheckSupportDirs(index1, Direction.North, Direction.South, support1, range, count);
                            support1 = CheckSupportDirs(index1, Direction.North, Direction.SouthEast, support1, range, count);
                            support1 = CheckSupportDirs(index1, Direction.North, Direction.SouthWest, support1, range, count);

                            support1 = CheckSupportDirs(index1, Direction.West, Direction.East, support1, range, count);
                            support1 = CheckSupportDirs(index1, Direction.West, Direction.NorthEast, support1, range, count);
                            support1 = CheckSupportDirs(index1, Direction.West, Direction.SouthEast, support1, range, count);

                            support1 = CheckSupportDirs(index1, Direction.NorthEast, Direction.SouthWest, support1, range, count);
                            support1 = CheckSupportDirs(index1, Direction.NorthEast, Direction.South, support1, range, count);

                            support1 = CheckSupportDirs(index1, Direction.East, Direction.SouthWest, support1, range, count);

                            support1 = CheckSupportDirs(index1, Direction.NorthWest, Direction.SouthEast, support1, range, count);
                            support1 = CheckSupportDirs(index1, Direction.NorthWest, Direction.South, support1, range, count);
                            support1 = CheckSupportDirs(index1, Direction.NorthWest, Direction.East, support1, range, count);
                        }

                        var support2 = false;
                        foreach (var entity in _lookup.GetEntitiesIntersecting(grid.GridTileToLocal(index2)))
                        {
                            if (entity != uid)
                            {
                                if (EntityManager.TryGetComponent<CaveSupportComponent?>(entity, out var support))
                                    support2 = true;
                            }
                        }
                        //if there is nothing for support but the support range has not been fully expended, check if the support's support exists
                        if (!support2 && range > count)
                        {
                            //TODO maybe find a better way to do this... (see above)
                            support2 = CheckSupportDirs(index2, Direction.North, Direction.South, support2, range, count);
                            support2 = CheckSupportDirs(index2, Direction.North, Direction.SouthEast, support2, range, count);
                            support2 = CheckSupportDirs(index2, Direction.North, Direction.SouthWest, support2, range, count);

                            support2 = CheckSupportDirs(index2, Direction.West, Direction.East, support2, range, count);
                            support2 = CheckSupportDirs(index2, Direction.West, Direction.NorthEast, support2, range, count);
                            support2 = CheckSupportDirs(index2, Direction.West, Direction.SouthEast, support2, range, count);

                            support2 = CheckSupportDirs(index2, Direction.NorthEast, Direction.SouthWest, support2, range, count);
                            support2 = CheckSupportDirs(index2, Direction.NorthEast, Direction.South, support2, range, count);

                            support2 = CheckSupportDirs(index2, Direction.East, Direction.SouthWest, support2, range, count);

                            support2 = CheckSupportDirs(index2, Direction.NorthWest, Direction.SouthEast, support2, range, count);
                            support2 = CheckSupportDirs(index2, Direction.NorthWest, Direction.South, support2, range, count);
                            support2 = CheckSupportDirs(index2, Direction.NorthWest, Direction.East, support2, range, count);
                        }
                        if (support1 && support2)
                            supported = true;
                    }
                }

                return supported;
            }

            //TODO maybe find a better way to do this... (see above) 
            supported = CheckSupportDirs(origin, Direction.North, Direction.South, supported, range, 0);
            supported = CheckSupportDirs(origin, Direction.North, Direction.SouthEast, supported, range, 0);
            supported = CheckSupportDirs(origin, Direction.North, Direction.SouthWest, supported, range, 0);

            supported = CheckSupportDirs(origin, Direction.West, Direction.East, supported, range, 0);
            supported = CheckSupportDirs(origin, Direction.West, Direction.NorthEast, supported, range, 0);
            supported = CheckSupportDirs(origin, Direction.West, Direction.SouthEast, supported, range, 0);

            supported = CheckSupportDirs(origin, Direction.NorthEast, Direction.SouthWest, supported, range, 0);
            supported = CheckSupportDirs(origin, Direction.NorthEast, Direction.South, supported, range, 0);

            supported = CheckSupportDirs(origin, Direction.East, Direction.SouthWest, supported, range, 0);

            supported = CheckSupportDirs(origin, Direction.NorthWest, Direction.SouthEast, supported, range, 0);
            supported = CheckSupportDirs(origin, Direction.NorthWest, Direction.South, supported, range, 0);
            supported = CheckSupportDirs(origin, Direction.NorthWest, Direction.East, supported, range, 0);

            


        }

        if (!supported)
        {
            CaveIn(uid, component);
        }
    }

    private void CaveIn(EntityUid uid, CaveInComponent component)
    {
        var pos = Transform(uid).MapPosition;
        var impact = component.CollapseRange;
        var range = component.SupportRange;
        var xform = _entities.GetComponent<TransformComponent>(uid);
        var damage = component.Damage;

        var box = Box2.CenteredAround(pos.Position, (range, range));
        var mapGrids = _mapManager.FindGridsIntersecting(pos.MapId, box).ToList();
        SoundSystem.Play("/Audio/Effects/explosion1.ogg", Filter.Pvs(uid), uid, AudioHelpers.WithVariation(0.2f));

        foreach (var grid in mapGrids)
        {
            void SpreadToDir(Vector2i origin, Direction dir, int range, int count)
            {

                count++;

                // Currently no support for spreading off or across grids.
                var index = origin + dir.ToIntVec();

                var occupied = false;
                List<EntityUid> damageableList = new List<EntityUid>();
                foreach (var entity in _lookup.GetEntitiesIntersecting(grid.GridTileToLocal(index)))
                {
                    if (entity != uid)
                    {
                        if (!damageableList.Contains(entity))
                            damageableList.Add(entity);
                        if (EntityManager.TryGetComponent<CaveSupportComponent?>(entity, out var support))
                            occupied = true;
                    }
                }

                if (!(!grid.TryGetTileRef(index, out var tile) || tile.Tile.IsEmpty) && !occupied && EntityManager.TryGetComponent<MetaDataComponent>(uid, out var caveIn))
                {
                    var newEffect = EntityManager.SpawnEntity(
                        "AsteroidRock",
                        grid.GridTileToLocal(index));

                    foreach (var entity in damageableList)
                    {
                        // damage
                        if (damage != null && HasComp<DamageableComponent>(entity))
                            _damageableSystem.TryChangeDamage(entity, damage, ignoreResistances: false);
                    }
                }

                if (count < range)
                {
                    SpreadToDir(index, Direction.North, impact, count);
                    SpreadToDir(index, Direction.NorthEast, impact, count);
                    SpreadToDir(index, Direction.NorthWest, impact, count);
                    SpreadToDir(index, Direction.East, impact, count);
                    SpreadToDir(index, Direction.South, impact, count);
                    SpreadToDir(index, Direction.SouthEast, impact, count);
                    SpreadToDir(index, Direction.SouthWest, impact, count);
                    SpreadToDir(index, Direction.West, impact, count);
                }
            }

            var origin = grid.TileIndicesFor(xform.Coordinates);
            SpreadToDir(origin, Direction.North, impact, 0);
            SpreadToDir(origin, Direction.NorthEast, impact, 0);
            SpreadToDir(origin, Direction.NorthWest, impact, 0);
            SpreadToDir(origin, Direction.East, impact, 0);
            SpreadToDir(origin, Direction.South, impact, 0);
            SpreadToDir(origin, Direction.SouthEast, impact, 0);
            SpreadToDir(origin, Direction.SouthWest, impact, 0);
            SpreadToDir(origin, Direction.West, impact, 0);
        }
    }


    private void OnDestruction(EntityUid uid, OreVeinComponent component, DestructionEventArgs args)
    {
        //run a cave in check
        if (EntityManager.TryGetComponent<CaveInComponent?>(uid, out var caveIn))
            CaveInCheck(uid, caveIn);

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
