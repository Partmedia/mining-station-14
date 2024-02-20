using Content.Shared.DragDrop;
using Content.Shared.Warps;

namespace Content.Server.Warps;

[RegisterComponent]
public sealed class WarperComponent : SharedWarperComponent
{
    /// Warp destination unique identifier.
    [ViewVariables(VVAccess.ReadWrite)] [DataField("id")] public string? ID { get; set; }

    /// Generates a dungeon destination upon use.
    [ViewVariables(VVAccess.ReadWrite)] [DataField("dungeon")] public bool Dungeon = false;

    public override bool DragDropOn(DragDropEvent eventArgs)
    {
        return true;
    }
}
