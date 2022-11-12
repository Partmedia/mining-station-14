using Content.Shared.Mining;

/// <summary>
/// Defines an entity that triggers a cave in if supports are not provided.
/// </summary>
[RegisterComponent]
public sealed class CaveInComponent : Component
{
    [DataField("supportRange")]
    public int SupportRange = 2;

    [DataField("collapseRange")]
    public int CollapseRange = 2;
}
