using Content.Shared.DragDrop;
using Content.Shared.Warps;

namespace Content.Client.Warps;

[RegisterComponent]
internal sealed class WarperComponent : SharedWarperComponent
{
    public override bool DragDropOn(DragDropEvent eventArgs)
    {
        return true;
    }
}
