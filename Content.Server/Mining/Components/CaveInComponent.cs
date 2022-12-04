using Content.Shared.Mining;
using Content.Shared.Damage;

/// <summary>
/// Defines an entity that triggers a cave in if supports are not provided.
/// </summary>
[RegisterComponent]
public sealed class CaveInComponent : Component
{
    [DataField("supportRange")]
    public int SupportRange = 3;

    [DataField("collapseRange")]
    public int CollapseRange = 2;

    [DataField("damage", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = default!;
}
