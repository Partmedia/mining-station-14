using Content.Server.Spawners.EntitySystems;
using Content.Server.Warps;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

using Content.Server.RL;

public sealed class RLMapGen : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileManager = default!;
    [Dependency] private readonly RLSystem _rl = default!;
    [Dependency] private readonly WarperSystem _warperSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MapGridComponent, MapInitEvent>(OnMapInit, before: new []{typeof(ConditionalSpawnerSystem)});
    }

    public string GetTemplate(int dlvl)
    {
#if RL
        if (_rl.Available())
        {
            var fn = RL.readstr("map-template");
            var form = RL.list2(fn, RL.num(dlvl));
            var ret = RL.eval(form);
            if (!RL.nil(ret))
                return RL.cstr(ret);
            else
                Logger.ErrorS("RLMapGen", "MAP-TEMPLATE returned NIL, using default dungeon template");
        }
#endif
        return "/Mining/Maps/Templates/dungeon.yml"; // fallback
    }

    private void OnMapInit(EntityUid uid, MapGridComponent comp, MapInitEvent args)
    {
        ProcGen(comp);
    }

    private void ProcGen(MapGridComponent map)
    {
        var defaultFloor = _tileManager["FloorAsteroidSand"].TileId;

        // Add all mining tiles to list, determine width and height of bounding grid indices
        List<(Vector2i, int)> miningTiles = new List<(Vector2i, int)>();
        int minX = 0, maxX = 0, minY = 0, maxY = 0;
        foreach (TileRef tile in map.GetAllTiles())
        {
            int val = 0;
            if (tile.Tile.TypeId == _tileManager["FloorMining"].TileId)
                val = 1;
            else if (tile.Tile.TypeId == _tileManager["FloorRLForest"].TileId)
                val = 100;

            if (val != 0)
            {
                map.SetTile(tile.GridIndices, new Tile(defaultFloor));
                miningTiles.Add((tile.GridIndices, val));
                int x = tile.GridIndices.X;
                int y = tile.GridIndices.Y;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        // Send to RL, read back spawn list
            Logger.InfoS("RLMapGen", "Attempting to generate map...");
            var request = new MapGenRequest();

            uint width = (uint)(maxX - minX + 1);
            uint height = (uint)(maxY - minY + 1);
            request.Width = width;
            request.Height = height;
            request.DungeonLevel = (int)_warperSystem.dungeonLevel;
            foreach ((var v, var val) in miningTiles)
            {
                int tx = v.X - minX;
                int ty = v.Y - minY;
                var req = new TileRequest();
                req.Request = (uint)val;
                req.X = (uint)tx;
                req.Y = (uint)ty;
                request.MapData.Add(req);
            }

            // Run mapgen
            try {
                var reply = _rl.Client().MapGen(request, deadline: _rl.Deadline());

                int nent = 0;
                foreach (var ent in reply.Entities)
                {
                    int tx = (int)ent.X + minX;
                    int ty = (int)ent.Y + minY;
                    var coord = map.GridTileToLocal(new Vector2i(tx, ty));
                    try
                    {
                        _entMan.SpawnEntity(ent.Entity, coord);
                        nent++;
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorS("RLMapGen", "Error spawning prototype: " + ex.ToString());
                    }
                }

                int ntiles = 0;
                foreach (var t in reply.Tiles)
                {
                    var name = t.Tile;
                    var x = t.X;
                    var y = t.Y;
                    int tx = (int)x + minX;
                    int ty = (int)y + minY;
                    var coord = new Vector2i(tx, ty);
                    try
                    {
                        var tile = _tileManager[name];
                        map.SetTile(coord, new Tile(tile.TileId, 0, (byte)_random.Next(0, tile.Variants)));
                        ntiles++;
                    }
                    catch (Exception e)
                    {
                        Logger.ErrorS("RLMapGen", "No tile with this name exists: " + name);
                    }
                }

                int ndecor = 0;

                Logger.InfoS("RLMapGen", $"Generated {nent} entities, {ntiles} tiles, {ndecor} decor");
            }
            catch (Exception e)
            {
                Logger.ErrorS("RLMapGen", "Could not make gRPC call:" + e);
            }
    }
}
