using Content.Shared.DragDrop;

namespace Content.Shared.Warps;

public class SharedWarperSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SharedWarperComponent, CanDropTargetEvent>(OnCanDrop);
    }

    private void OnCanDrop(EntityUid uid, SharedWarperComponent component, ref CanDropTargetEvent args)
    {
        args.CanDrop = true;
    }
}
