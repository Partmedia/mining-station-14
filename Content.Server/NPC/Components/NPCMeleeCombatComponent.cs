namespace Content.Server.NPC.Components;

/// <summary>
/// Added to NPCs whenever they're in melee combat so they can be handled by the dedicated system.
/// </summary>
[RegisterComponent]
public sealed class NPCMeleeCombatComponent : Component
{
    /// <summary>
    /// Weapon we're using to attack the target. Can also be ourselves.
    /// </summary>
    [ViewVariables] public EntityUid Weapon;

    /// <summary>
    /// If the target is moving what is the chance for this NPC to miss.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float MissChance;

    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public CombatStatus Status = CombatStatus.Normal;

    /// <summary>
    /// Time that we last hit the enemy. If this gets too long, give up (and go back to HTN).
    /// </summary>
    [ViewVariables]
    public TimeSpan LastHit;
}

public enum CombatStatus : byte
{
    /// <summary>
    /// The target isn't in LOS anymore.
    /// </summary>
    NotInSight,

    /// <summary>
    /// Due to some generic reason we are unable to attack the target.
    /// </summary>
    Unspecified,

    /// <summary>
    /// Set if we can't reach the target for whatever reason.
    /// </summary>
    TargetUnreachable,

    /// <summary>
    /// If the target is outside of our melee range.
    /// </summary>
    TargetOutOfRange,

    /// <summary>
    /// Set if the weapon we were assigned is no longer valid.
    /// </summary>
    NoWeapon,

    /// <summary>
    /// Set LastHit timer elapses.
    /// </summary>
    GiveUp,

    /// <summary>
    /// No dramas.
    /// </summary>
    Normal,
}
