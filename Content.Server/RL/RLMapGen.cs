using Content.Server.Spawners.EntitySystems;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

public sealed class RLMapGen : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileManager = default!;
    [Dependency] private readonly RLSystem _rl = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MapGridComponent, MapInitEvent>(OnMapInit, before: new []{typeof(ConditionalSpawnerSystem)});
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

#if RL
        // Send to RL, read back spawn list
        if (_rl.Available())
        {
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            // Array setup
            var arg = RL.eval_str($"(make-map {width} {height})");
            if (RL.nil(arg))
            {
                Logger.ErrorS("RLMapGen", "Failed to generate map: MAKE-MAP returned NIL");
                return;
            }

            // Populate array
            foreach ((var v, var val) in miningTiles)
            {
                int tx = v.X - minX;
                int ty = v.Y - minY;
                RL.si_aset(4, arg, RL.num(tx), RL.num(ty), RL.num(val));
            }

            // Run mapgen
            var fn = RL.readstr("mapgen");
            var form = RL.list2(fn, arg);
            var ret = RL.eval(form);
            if (!RL.ensure_list(ret, 3))
            {
                Logger.ErrorS("RLMapGen", "Failed to generate map: MAPGEN did not return a list");
                return;
            }

            // Read back results
            var entities = RL.ecl_car(ret);
            int nent = 0;
            while (!RL.nil(entities))
            {
                var el = RL.ecl_car(entities);
                if (!RL.ensure_list(el, 3))
                {
                    Logger.ErrorS("RLMapGen", "RL returned a malformed entity list");
                }
                else
                {
                    var e = RL.cstr(RL.ecl_car(el));
                    var x = RL.cint(RL.ecl_cadr(el));
                    var y = RL.cint(RL.ecl_caddr(el));
                    int tx = x + minX;
                    int ty = y + minY;
                    var coord = map.GridTileToLocal(new Vector2i(tx, ty));
                    try
                    {
                        _entMan.SpawnEntity(e, coord);
                        nent++;
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorS("RLMapGen", "Error spawning prototype: " + ex.ToString());
                    }
                }
                entities = RL.ecl_cdr(entities);
            }

            var tiles = RL.ecl_cadr(ret);
            int ntiles = 0;
            while (!RL.nil(tiles))
            {
                var el = RL.ecl_car(tiles);
                if (!RL.ensure_list(el, 3))
                {
                    Logger.ErrorS("RLMapGen", "RL returned a malformed tile list");
                }
                else
                {
                    var name = RL.cstr(RL.ecl_car(el));
                    var x = RL.cint(RL.ecl_cadr(el));
                    var y = RL.cint(RL.ecl_caddr(el));
                    int tx = x + minX;
                    int ty = y + minY;
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
                tiles = RL.ecl_cdr(tiles);
            }

            int ndecor = 0;
            {
            var decor = RL.ecl_caddr(ret);
            }

            Logger.InfoS("RLMapGen", $"Generated {nent} entities, {ntiles} tiles, {ndecor} decor");
        }
#endif
    }
}
