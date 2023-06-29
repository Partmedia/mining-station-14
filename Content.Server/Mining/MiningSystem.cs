using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Mining.Components;
using Content.Server.Popups;
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
using Content.Server.Gravity;
using Content.Shared.Gravity;
using Content.Shared.Construction.Components;
using Content.Server.Construction.Conditions;

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
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly GravitySystem _gravity = default!;

    private Queue<EntityUid> _timerQueue = new(); // entities waiting for their next time event

    /** Directions from center that cave-ins inspect and can spread to. */
    private readonly List<Direction> directions = new List<Direction>{
        Direction.North,
        Direction.South,
        Direction.East,
        Direction.West,
        Direction.NorthEast,
        Direction.NorthWest,
        Direction.SouthEast,
        Direction.SouthWest
    };

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

    private bool CaveInCheck(EntityUid uid, CaveInComponent component)
    {
        if (!TryComp<TransformComponent>(uid, out var xform))
            return true;

        if (TryComp<GravityComponent>(xform.GridUid, out var gravity) && !gravity.Enabled)
            return true;

        //get the support range of the mined rock
        //check for all entities in range
        //if there are no walls or asteroid rocks in range, spawn rocks within the surrounding area
        var pos = Transform(uid).MapPosition;
        var range = component.SupportRange;
        var supported = false;

        var box = Box2.CenteredAround(pos.Position, (range, range));
        var mapGrids = _mapManager.FindGridsIntersecting(pos.MapId, box).ToList();
        var grids = mapGrids.Select(x => x.Owner).ToList();

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
                return true;

            //cave-in prevention requires a support within range
            bool CheckSupportDir(Vector2i origin, Direction dir,  bool supported, int range, int count)
            {
                count++;

                if (!supported)
                {
                    // Currently no support for spreading off or across grids.
                    var index = origin + dir.ToIntVec();

                    if (EntityManager.TryGetComponent<MetaDataComponent>(uid, out var caveIn))
                    {
                        foreach (var entity in _lookup.GetEntitiesIntersecting(grid.GridTileToLocal(index)))
                        {
                            if (entity != uid)
                            {
                                if (EntityManager.TryGetComponent<CaveSupportComponent?>(entity, out var support))
                                    supported = true;
                            }
                        }

                        //if there is nothing for support but the support range has not been fully expended, check if the support's support exists
                        if (!supported && range > count)
                        {
                            foreach (var direction in directions)
                            {
                                supported = CheckSupportDir(index, direction, supported, range, count);
                            }
                        }
                    }
                }

                return supported;
            }

            foreach (var direction in directions)
            {
                supported = CheckSupportDir(origin, direction, supported, range, 0);
            }
        }

        return supported;
    }

    /**
     * Cave in the ceiling centered around entity 'uid' whose CaveInComponent is 'component'.
     */
    public void CaveIn(EntityUid uid, CaveInComponent component)
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
            List<EntityUid> damageableList = new List<EntityUid>(); // anyone who should take damage
            void SpreadToDir(Vector2i origin, Direction dir, int range, int count)
            {

                count++;

                // Currently no support for spreading off or across grids.
                var index = origin + dir.ToIntVec();

                var occupied = false;
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

                }

                if (count < range)
                {
                    foreach (var direction in directions)
                        SpreadToDir(index, direction, impact, count);
                }
            }

            var origin = grid.TileIndicesFor(xform.Coordinates);
            foreach (var direction in directions)
                SpreadToDir(origin, direction, impact, 0);

            foreach (var entity in damageableList)
            {
                if (damage != null && HasComp<DamageableComponent>(entity))
                    _damageableSystem.TryChangeDamage(entity, damage, ignoreResistances: true);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        Queue<EntityUid> _checkQueue = new(); // entities that need to be checked for cave-ins
        Queue<EntityUid> _removeQueue = new();
        foreach (var uid in _timerQueue)
        {
            if (!TryComp<CaveInComponent>(uid, out var timedSpace))
                continue;

            if (!timedSpace.Timed)
                continue;

            timedSpace.Timer += frameTime;

            //check if the entity is anchored - if not REMOVE IT
            if(Transform(uid).Anchored)
                _checkQueue.Enqueue(uid);
            else
                _removeQueue.Enqueue(uid);
        }

        _timerQueue.Clear();
   
        foreach (var uid in _checkQueue)
        {

            //check if the time is up, if not re-queue and move on
            if (!TryComp<CaveInComponent>(uid, out var timedSpace))
                continue;

            //first, check if an entity exists on the same space as the timedSpace
            if (timedSpace.Timer >= timedSpace.Time)
            {
                timedSpace.Timer = 0f;

                var pos = Transform(uid).MapPosition;
                var xform = _entities.GetComponent<TransformComponent>(uid);
                var box = Box2.CenteredAround(pos.Position, (0,0));
                var mapGrids = _mapManager.FindGridsIntersecting(pos.MapId, box).ToList();
                var check = true;
                
                foreach (var grid in mapGrids)
                {
                    var origin = grid.TileIndicesFor(xform.Coordinates);
                    foreach (var entity in _lookup.GetEntitiesIntersecting(grid.GridTileToLocal(origin)))
                    {
                        if (entity != uid)
                        {
                            //if there is an entity with the cave-in component (with timed set to false) set THIS entity for deletion (and of course do NOT re-queue the timer)
                            if (EntityManager.TryGetComponent<CaveInComponent?>(entity, out var rock) && !rock.Timed)
                            {
                                _removeQueue.Enqueue(uid);
                                check = false;
                            }
                            //if there is an entity with the support component, do not check to cave in but DO re-queue the timer
                            else if (EntityManager.TryGetComponent<CaveSupportComponent?>(entity, out var support))
                            {
                                _timerQueue.Enqueue(uid);
                                check = false;
                            }
                        }
                    }
                }

                if (check) {
                    //next, run a cave-in check
                    var supported = CaveInCheck(uid, timedSpace);

                    //if supported, simply re-queue
                    if (supported)
                    {
                        _timerQueue.Enqueue(uid);
                    }
                    else
                    {
                        //if not, check the warnings and play the sound if not exhausted
                        if (timedSpace.WarningCounter < timedSpace.TimerWarnings)
                        {
                            timedSpace.WarningCounter++;
                            _popupSystem.PopupEntity("You hear the rumble of the rocks above you.", uid, Filter.Pvs(uid));
                            SoundSystem.Play(timedSpace.QuakeSound, Filter.Pvs(uid), uid, null);
                            _timerQueue.Enqueue(uid);
                        }
                        else
                        {
                            //if warnings are exhausted, queue this entity for deletion and trigger the cave-in
                            CaveIn(uid, timedSpace);
                            _removeQueue.Enqueue(uid);
                        }
                    }
                }
            }
            else
            {
                _timerQueue.Enqueue(uid);
            }
        }

        _checkQueue.Clear();

        foreach (var uid in _removeQueue)
        {
            EntityManager.DeleteEntity(uid);
        }

        _removeQueue.Clear();
    }

    /**
     * Spawns ore when an ore vein is mined or otherwise destroyed.
     */
    private void OnDestruction(EntityUid uid, OreVeinComponent component, DestructionEventArgs args)
    {
        var coords = Transform(uid).Coordinates;
        //run a cave in check
        if (EntityManager.TryGetComponent<CaveInComponent?>(uid, out var caveIn))
        {
            var supported = CaveInCheck(uid, caveIn);
            if (!supported)
                CaveIn(uid, caveIn);
            else
            {
                //spawn timed space
                Spawn("TimedSpace", coords);
            }
        }
        
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
        if (TryComp<CaveInComponent>(uid, out var timedSpace) && timedSpace.Timed)
            _timerQueue.Enqueue(uid);

        if (component.CurrentOre != null || component.OreRarityPrototypeId == null || !_random.Prob(component.OreChance))
            return;

        component.CurrentOre = _proto.Index<WeightedRandomPrototype>(component.OreRarityPrototypeId).Pick(_random);
    }
}
