using Content.Shared.Mining;
using Content.Shared.Damage;
using Robust.Shared.Audio;

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

    [DataField("damage", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = default!;

    //triggers on a timer instead of a destruction event
    [DataField("timed")]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Timed = false;

    [DataField("time")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Time = 30f;

    [DataField("timer")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Timer = 0f;

    //number of timer completes before collapse
    [DataField("timerWarnings")]
    [ViewVariables(VVAccess.ReadWrite)]
    public int TimerWarnings = 0;

    [ViewVariables]
    public int WarningCounter = 0;

    public string QuakeSound = "/Audio/Effects/ominous_quake.ogg";

}
