using System.Linq;

using Content.Server.Ghost.Components;
using Content.Server.Popups;
using Content.Shared.GameTicking;
using Content.Shared.Gravity;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Player;

namespace Content.Server.Warps;

public class WarperSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly WarpPointSystem _warpPointSystem = default!;

    public int dungeonLevel = 0;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarperComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        dungeonLevel = 0;
    }

    private void OnInteractHand(EntityUid uid, WarperComponent component, InteractHandEvent args)
    {
        if (component.Dungeon)
        {
            // Destination is next level
            dungeonLevel++;

            var dMap = _mapManager.CreateMap();
            var map = _map.LoadMap(dMap, "/Mining/Maps/Templates/dungeon.yml");
            var gravity = _entMan.EnsureComponent<GravityComponent>(map.First());
            gravity.Enabled = true;
            _entMan.Dirty(gravity);

            // create destination back up on current map
            var upDest = _entMan.SpawnEntity("WarpPoint", Transform(uid).Coordinates);
            if (TryComp<WarpPointComponent>(upDest, out var upWarp))
            {
                upWarp.ID = $"dlvl{dungeonLevel-1}down";
            }

            component.ID = $"dlvl{dungeonLevel}up";

            // find stairs up, create warp destination
            foreach (var warper in EntityManager.EntityQuery<WarperComponent>())
            {
                // find ladder going up
                if (Transform(warper.Owner).MapID == dMap && !warper.Dungeon)
                {
                    // link back upstairs
                    warper.ID = $"dlvl{dungeonLevel - 1}down";

                    // Create destination downstairs
                    var downDest = _entMan.SpawnEntity("WarpPoint", Transform(warper.Owner).Coordinates);
                    if (TryComp<WarpPointComponent>(downDest, out var downWarp))
                    {
                        downWarp.ID = $"dlvl{dungeonLevel}up";
                        downWarp.Location = $"Dungeon Level {dungeonLevel:00}";
                    }
                }
            }

            // don't generate again
            component.Dungeon = false;
        }

        if (component.ID is null)
        {
            Logger.DebugS("warper", "Warper has no destination");
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", args.Target)), args.User, Filter.Entities(args.User));
            return;
        }

        var dest = _warpPointSystem.FindWarpPoint(component.ID);
        if (dest is null)
        {
            Logger.DebugS("warper", String.Format("Warp destination '{0}' not found", component.ID));
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", args.Target)), args.User, Filter.Entities(args.User));
            return;
        }

        var entMan = IoCManager.Resolve<IEntityManager>();
        TransformComponent? destXform;
        entMan.TryGetComponent<TransformComponent>(dest.Value, out destXform);
        if (destXform is null)
        {
            Logger.DebugS("warper", String.Format("Warp destination '{0}' has no transform", component.ID));
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", args.Target)), args.User, Filter.Entities(args.User));
            return;
        }

        // Check that the destination map is initialized and return unless in aghost mode.
        var mapMgr = IoCManager.Resolve<IMapManager>();
        var destMap = destXform.MapID;
        if (!mapMgr.IsMapInitialized(destMap) || mapMgr.IsMapPaused(destMap))
        {
            if (!entMan.HasComponent<GhostComponent>(args.User))
            {
                // Normal ghosts cannot interact, so if we're here this is already an admin ghost.
                Logger.DebugS("warper", String.Format("Player tried to warp to '{0}', which is not on a running map", component.ID));
                _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", args.Target)), args.User, Filter.Entities(args.User));
                return;
            }
        }

        var xform = entMan.GetComponent<TransformComponent>(args.User);
        xform.Coordinates = destXform.Coordinates;
        xform.AttachToGridOrMap();
        if (entMan.TryGetComponent(uid, out PhysicsComponent? phys))
        {
            _physics.SetLinearVelocity(uid, Vector2.Zero);
        }
    }
}
