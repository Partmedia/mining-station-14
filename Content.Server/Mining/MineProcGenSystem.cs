using Robust.Shared.Map.Components;
using Robust.Shared.Map;

namespace Content.Server.Mining;

public sealed class MineProcGenSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly ITileDefinitionManager _tileManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MapGridComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, MapGridComponent comp, MapInitEvent args)
    {
        ProcGen(comp);
    }

    private void ProcGen(MapGridComponent map)
    {
        var miningFloor = _tileManager["FloorMining"].TileId;
        var replaceFloor = _tileManager["FloorAsteroidSand"].TileId;
        foreach (TileRef tile in map.GetAllTiles())
        {
            if (tile.Tile.TypeId == miningFloor)
            {
                var coord = map.GridTileToLocal(tile.GridIndices);
                map.SetTile(tile.GridIndices, new Tile(replaceFloor));
                _entMan.SpawnEntity("AsteroidRockMining", coord);
            }
        }
    }
}
