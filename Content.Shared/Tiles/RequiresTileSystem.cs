using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Map;

namespace Content.Shared.Tiles;

public sealed class RequiresTileSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TileChangedEvent>(OnTileChange);
    }

    private void OnTileChange(ref TileChangedEvent ev)
    {
        foreach (var ent in EntityQuery<RequiresTileComponent>())
        {
            var xform = Transform(ent.Owner);
            if (!_mapManager.TryGetGrid(xform.GridUid, out var grid))
            {
                QueueDel(ent.Owner);
            }
        }
    }
}
