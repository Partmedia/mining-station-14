using System.Linq;

using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.Ghost.Components;
using Content.Server.Popups;
using Content.Shared.DragDrop;
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
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly RLMapGen _procgen = default!;

    public int dungeonLevel = 0;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WarperComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<WarperComponent, DragDropEvent>(OnDragDrop);
        SubscribeLocalEvent<WarperComponent, MoveFinished>(OnMoveFinished);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        dungeonLevel = 0;
    }

    private void RandomAnnounce(EntityUid uid, int dlvl)
    {
        string announcement;
        if (dlvl == 1)
            announcement = Loc.GetString("dungeon-enter-announce");
        else
            return;
        var sender = Loc.GetString("admin-announce-announcer-default");
        _chatSystem.DispatchStationAnnouncement(uid, announcement, sender);
    }

    private bool GenerateDungeon(EntityUid uid, WarperComponent component)
    {
        // Destination is next level
        int lvl = dungeonLevel + 1;
        MapId mapIdDown = _mapManager.CreateMap();
        string path = _procgen.GetTemplate(lvl);

        // Map generator relies on the global dungeonLevel, so temporarily set it here
        dungeonLevel = lvl;
        if (!_map.TryLoad(mapIdDown, path, out var maps))
        {
            Logger.ErrorS("warper", $"Could not load map {path} for dungeon level {lvl}");
            dungeonLevel = lvl - 1;
            return false;
        }

        // Back it out
        dungeonLevel = lvl - 1;

        // Prepare map
        var map = maps.First();
        var gravity = _entMan.EnsureComponent<GravityComponent>(map);
        gravity.Enabled = true;
        _entMan.Dirty(gravity);

        // create destination back up on current map
        var upDest = _entMan.SpawnEntity("WarpPoint", Transform(uid).Coordinates);
        if (TryComp<WarpPointComponent>(upDest, out var upWarp))
        {
            upWarp.ID = $"dlvl{lvl - 1}down";
        }
        else
        {
            Logger.ErrorS("warper", "Could not find WarpPointComponent when setting return destination");
            return false;
        }

        // find stairs up in new map
        WarperComponent? warperBackUp = FindStairsUp(mapIdDown);
        if (warperBackUp == null)
        {
            Logger.ErrorS("warper", "Could not find stairs going back up");
            return false;
        }

        // link back upstairs
        warperBackUp.ID = $"dlvl{lvl - 1}down";

        // Create destination downstairs
        var downDest = _entMan.SpawnEntity("WarpPoint", Transform(warperBackUp.Owner).Coordinates);
        if (TryComp<WarpPointComponent>(downDest, out var downWarp))
        {
            downWarp.ID = $"dlvl{lvl}up";
            downWarp.Location = $"Dungeon Level {lvl:00}";
        }
        else
        {
            Logger.ErrorS("warper", "Could not find WarpPointComponent when setting destination downstairs");
            return false;
        }

        component.ID = $"dlvl{lvl}up";
        dungeonLevel = lvl;
        RandomAnnounce(uid, dungeonLevel);
        return true;
    }

    private WarperComponent? FindStairsUp(MapId id)
    {
        foreach (var warper in EntityManager.EntityQuery<WarperComponent>())
        {
            // if it's not going down assume it's going up
            if (Transform(warper.Owner).MapID == id && !warper.Dungeon)
            {
                return warper;
            }
        }
        return null;
    }

    private void OnActivate(EntityUid uid, WarperComponent component, ActivateInWorldEvent args)
    {
        if (component.Dungeon && GenerateDungeon(uid, component))
        {
            // don't generate again
            component.Dungeon = false;
        }

        DoWarp(args.Target, args.User, args.User, component);
    }

    private void DoWarp(EntityUid uid, EntityUid user, EntityUid victim, WarperComponent component)
    {
        if (component.ID is null)
        {
            Logger.DebugS("warper", "Warper has no destination");
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", uid)), user, Filter.Entities(user));
            return;
        }

        var dest = _warpPointSystem.FindWarpPoint(component.ID);
        if (dest is null)
        {
            Logger.DebugS("warper", String.Format("Warp destination '{0}' not found", component.ID));
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", uid)), user, Filter.Entities(user));
            return;
        }

        var entMan = IoCManager.Resolve<IEntityManager>();
        TransformComponent? destXform;
        entMan.TryGetComponent<TransformComponent>(dest.Value, out destXform);
        if (destXform is null)
        {
            Logger.DebugS("warper", String.Format("Warp destination '{0}' has no transform", component.ID));
            _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", uid)), user, Filter.Entities(user));
            return;
        }

        // Check that the destination map is initialized and return unless in aghost mode.
        var mapMgr = IoCManager.Resolve<IMapManager>();
        var destMap = destXform.MapID;
        if (!mapMgr.IsMapInitialized(destMap) || mapMgr.IsMapPaused(destMap))
        {
            if (!entMan.HasComponent<GhostComponent>(user))
            {
                // Normal ghosts cannot interact, so if we're here this is already an admin ghost.
                Logger.DebugS("warper", String.Format("Player tried to warp to '{0}', which is not on a running map", component.ID));
                _popupSystem.PopupEntity(Loc.GetString("warper-goes-nowhere", ("warper", uid)), user, Filter.Entities(user));
                return;
            }
        }

        var xform = entMan.GetComponent<TransformComponent>(victim);
        xform.Coordinates = destXform.Coordinates;
        xform.AttachToGridOrMap();
        if (entMan.TryGetComponent(victim, out PhysicsComponent? phys))
        {
            _physics.SetLinearVelocity(victim, Vector2.Zero);
        }
    }

    private void OnDragDrop(EntityUid uid, WarperComponent component, DragDropEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var userUid = args.User;
        var doAfterArgs = new DoAfterEventArgs(userUid, 5, default, uid)
        {
            BreakOnTargetMove = true,
            BreakOnUserMove = true,
            BreakOnDamage = true,
            BreakOnStun = true,
            NeedHand = true,
            TargetFinishedEvent = new MoveFinished(userUid, args.Dragged)
        };

        _doAfter.DoAfter(doAfterArgs);
    }

    private sealed class MoveFinished : EntityEventArgs
    {
        public EntityUid VictimUid;
        public EntityUid UserUid;

        public MoveFinished(EntityUid userUid, EntityUid victimUid)
        {
            UserUid = userUid;
            VictimUid = victimUid;
        }
    }

    private void OnMoveFinished(EntityUid uid, WarperComponent component, MoveFinished args)
    {
        DoWarp(component.Owner, args.UserUid, args.VictimUid, component);
    }
}
