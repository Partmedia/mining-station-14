using Content.Shared.Audio;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Tiles;

public sealed class FloorTileSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stackSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FloorTileComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, FloorTileComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Handled)
            return;

        if (!TryComp<StackComponent>(uid, out var stack))
            return;

        if (component.OutputTiles == null)
            return;

        // this looks a bit sussy but it might be because it needs to be able to place off of grids and expand them
        var location = args.ClickLocation.AlignWithClosestGridTile();
        var physics = GetEntityQuery<PhysicsComponent>();
        foreach (var ent in location.GetEntitiesInTile(lookupSystem: _lookup))
        {
            // check that we the tile we're trying to access isn't blocked by a wall or something
            if (physics.TryGetComponent(ent, out var phys) &&
                phys.BodyType == BodyType.Static &&
                phys.Hard &&
                (phys.CollisionLayer & (int) CollisionGroup.Impassable) != 0)
                return;
        }
        var locationMap = location.ToMap(EntityManager);
        if (locationMap.MapId == MapId.Nullspace)
            return;
        _mapManager.TryGetGrid(location.EntityId, out var mapGrid);

        foreach (var currentTile in component.OutputTiles)
        {
            var currentTileDefinition = (ContentTileDefinition) _tileDefinitionManager[currentTile];

            if (mapGrid != null)
            {
                var ev = new FloorTileAttemptEvent();
                RaiseLocalEvent(mapGrid);

                if (HasComp<ProtectedGridComponent>(mapGrid.Owner) || ev.Cancelled)
                {
                    if (_netManager.IsClient && _timing.IsFirstTimePredicted)
                        _popup.PopupEntity(Loc.GetString("invalid-floor-placement"), args.User);

                    return;
                }

                var tile = mapGrid.GetTileRef(location);
                var baseTurf = (ContentTileDefinition) _tileDefinitionManager[tile.Tile.TypeId];

                if (HasBaseTurf(currentTileDefinition, baseTurf.ID))
                {
                    if (!_stackSystem.Use(uid, 1, stack))
                        continue;

                    PlaceAt(args.User, mapGrid, location, currentTileDefinition.TileId, component.PlaceTileSound);
                    args.Handled = true;
                    return;
                }
            }
            else if (HasBaseTurf(currentTileDefinition, ContentTileDefinition.SpaceID))
            {
                mapGrid = _mapManager.CreateGrid(locationMap.MapId);
                var gridXform = Transform(mapGrid.Owner);
                _transform.SetWorldPosition(gridXform, locationMap.Position);
                location = new EntityCoordinates(mapGrid.Owner, Vector2.Zero);
                PlaceAt(args.User, mapGrid, location, _tileDefinitionManager[component.OutputTiles[0]].TileId, component.PlaceTileSound, mapGrid.TileSize / 2f);
                args.Handled = true;
                return;
            }
        }
    }

    public bool HasBaseTurf(ContentTileDefinition tileDef, string baseTurf)
    {
        foreach (var tileBaseTurf in tileDef.BaseTurfs)
        {
            if (baseTurf == tileBaseTurf)
                return true;
        }

        return false;
    }

    private void PlaceAt(EntityUid user, MapGridComponent mapGrid, EntityCoordinates location, ushort tileId, SoundSpecifier placeSound, float offset = 0)
    {
        var variant = _random.Pick(((ContentTileDefinition) _tileDefinitionManager[tileId]).PlacementVariants);
        mapGrid.SetTile(location.Offset(new Vector2(offset, offset)), new Tile(tileId, 0, variant));

        _audio.PlayPredicted(placeSound, mapGrid.Owner, user, AudioHelpers.WithVariation(0.125f, _random));
    }
}
