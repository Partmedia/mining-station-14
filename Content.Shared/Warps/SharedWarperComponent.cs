using Content.Shared.DragDrop;

namespace Content.Shared.Warps;

public abstract class SharedWarperComponent : Component, IDragDropOn
{
    bool IDragDropOn.CanDragDropOn(DragDropEvent eventArgs)
    {
        return true;
    }

    public abstract bool DragDropOn(DragDropEvent eventArgs);
}
