using Content.Shared.Mining;

/// <summary>
/// Defines an entity that prevents a cave in upon ore destruction.
/// </summary>
[RegisterComponent]
public sealed class CaveSupportComponent : Component
{
    /** If true, not eligible to cave in during a quake. Set to false for rocks or no quakes happen. */
    [DataField("quakeProof")]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool QuakeProof = true;
}
