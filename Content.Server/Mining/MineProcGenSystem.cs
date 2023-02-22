using Content.Server.Spawners.EntitySystems;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Mining;

public sealed class MineProcGenSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileManager = default!;

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
        var miningFloor = _tileManager["FloorMining"].TileId;
        var replaceFloor = _tileManager["FloorAsteroidSand"].TileId;
        foreach (TileRef tile in map.GetAllTiles())
        {
            if (tile.Tile.TypeId == miningFloor)
            {
                var coord = map.GridTileToLocal(tile.GridIndices);
                map.SetTile(tile.GridIndices, new Tile(replaceFloor));

                // Decorations
                if (_random.NextFloat() < 0.3)
                    _entMan.SpawnEntity("BasaltRandom", coord);

                if (_random.NextFloat() < 0.05)
                {
                    // Opening
                    var p = _random.NextFloat();
                    if (p < 0.05)
                        _entMan.SpawnEntity("SpawnMobCarpMagic", coord);
                    else if (p < 0.1)
                        _entMan.SpawnEntity("SpaceTickSpawner", coord);
                    else if (p < 0.2)
                        _entMan.SpawnEntity("CrateFilledSpawner", coord);
                }
                else
                {
                    // Rock
                    if (_random.NextFloat() < 0.3)
                    {
                        // Vein
                        var p = _random.NextFloat();
                        if (p < 0.03)
                            _entMan.SpawnEntity("WallRockUranium", coord);
                        else if (p < 0.07)
                            _entMan.SpawnEntity("WallRockGold", coord);
                        else if (p < 0.17)
                            _entMan.SpawnEntity("WallRockSilver", coord);
                        else if (p < 0.45)
                            _entMan.SpawnEntity("WallRockPlasma", coord);
                        else if (p < 0.7)
                            _entMan.SpawnEntity("WallRockQuartz", coord);
                        else
                            _entMan.SpawnEntity("WallRockTin", coord);
                    }
                    else
                    {
                        if (_random.NextFloat() < 0.6)
                            _entMan.SpawnEntity("AsteroidRockMining", coord);
                        else
                            _entMan.SpawnEntity("MountainRockMining", coord);
                    }

                    // Stuff embedded in rock
                    {
                    var p = _random.NextFloat();
                    if (p < 0.005)
                        _entMan.SpawnEntity("RandomArtifactSpawner", coord);
                    else if (p < 0.01)
                        _entMan.SpawnEntity("MaterialDiamond1", coord);
                    }
                }
            }
        }
    }
}
