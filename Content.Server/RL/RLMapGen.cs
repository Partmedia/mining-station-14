using Content.Server.Spawners.EntitySystems;
using Content.Server.Warps;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;
#if !RL
using Content.Server.RLRpc;
#endif

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
        if (_rl.Available())
        {
#if RL
            var fn = RL.readstr("map-template");
            var form = RL.list2(fn, RL.num(dlvl));
            var ret = RL.eval(form);
            if (!RL.nil(ret))
                return RL.cstr(ret);
            else
                Logger.ErrorS("RLMapGen", "MAP-TEMPLATE returned NIL, using default dungeon template");
#else
            var request = new TemplateRequest();
            request.Level = (uint)dlvl;

            TemplateResponse reply;
            try
            {
                reply = _rl.Client().GetTemplate(request, deadline: _rl.Deadline());
                return reply.Path;
            }
            catch (Exception e)
            {
                Logger.ErrorS("RLMapGen", "Could not make gRPC call:" + e);
            }
#endif
        }
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

        Logger.InfoS("RLMapGen", "Attempting to generate map...");
        if (_rl.Available())
        {
            uint width = (uint)(maxX - minX + 1);
            uint height = (uint)(maxY - minY + 1);
#if RL
            // Array setup
            var arg = RL.eval_str($"(make-map {width} {height})");
            if (RL.nil(arg))
            {
                Logger.ErrorS("RLMapGen", "Failed to generate map: MAKE-MAP returned NIL");
                return;
            }
#else
            var request = new MapGenRequest();
            request.Width = width;
            request.Height = height;
            request.DungeonLevel = (int)_warperSystem.dungeonLevel;
#endif

            // Populate array
            foreach ((var v, var val) in miningTiles)
            {
                int tx = v.X - minX;
                int ty = v.Y - minY;
#if RL
                RL.si_aset(4, arg, RL.num(tx), RL.num(ty), RL.num(val));
#else
                var req = new TileRequest();
                req.Request = (uint)val;
                req.X = (uint)tx;
                req.Y = (uint)ty;
                request.MapData.Add(req);
#endif
            }

            // Run mapgen
#if RL
            var fn = RL.readstr("mapgen");
            var form = RL.list3(fn, arg, RL.num(_warperSystem.dungeonLevel));
            var ret = RL.eval(form);
            if (!RL.ensure_list(ret, 3))
            {
                Logger.ErrorS("RLMapGen", "Failed to generate map: MAPGEN did not return a list");
                return;
            }

            // Read back results
            var entities = RL.ecl_car(ret);
#else
            MapGenResponse reply;
            try
            {
                reply = _rl.Client().MapGen(request, deadline: _rl.Deadline());
            }
            catch (Exception e)
            {
                Logger.ErrorS("RLMapGen", "Could not make gRPC call:" + e);
                return;
            }
#endif
            int nent = 0;
#if RL
            while (!RL.nil(entities))
            {
                var el = RL.ecl_car(entities);
                if (!RL.ensure_list(el, 3))
                {
                    Logger.ErrorS("RLMapGen", "RL returned a malformed entity list");
                    return;
                }
                var e = RL.cstr(RL.ecl_car(el));
                var x = RL.cint(RL.ecl_cadr(el));
                var y = RL.cint(RL.ecl_caddr(el));
                int tx = x + minX;
                int ty = y + minY;
#else
            foreach (var ent in reply.Entities)
            {
                int tx = (int)ent.X + minX;
                int ty = (int)ent.Y + minY;
                var e = ent.Entity;
#endif
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
#if RL
                entities = RL.ecl_cdr(entities);
#endif
            }

            int ntiles = 0;
#if RL
            var tiles = RL.ecl_cadr(ret);
            while (!RL.nil(tiles))
            {
                var el = RL.ecl_car(tiles);
                if (!RL.ensure_list(el, 3))
                {
                    Logger.ErrorS("RLMapGen", "RL returned a malformed tile list");
                    return;
                }

                var name = RL.cstr(RL.ecl_car(el));
                var x = RL.cint(RL.ecl_cadr(el));
                var y = RL.cint(RL.ecl_caddr(el));
                int tx = x + minX;
                int ty = y + minY;
#else
            foreach (var t in reply.Tiles)
            {
                var name = t.Tile;
                var x = t.X;
                var y = t.Y;
                int tx = (int)x + minX;
                int ty = (int)y + minY;
#endif

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
#if RL
                tiles = RL.ecl_cdr(tiles);
#endif
            }

            int ndecor = 0;
            Logger.InfoS("RLMapGen", $"Generated {nent} entities, {ntiles} tiles, {ndecor} decor");
        }
    }
}
