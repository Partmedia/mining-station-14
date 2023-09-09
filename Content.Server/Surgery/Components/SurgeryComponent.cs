using Content.Shared.Damage;

namespace Content.Server.Surgery
{
    [RegisterComponent]
    [Access(typeof(SurgerySystem))]
    public sealed class SurgeryComponent : Component
    {
        /// <summary>
        ///     Update whenever a part or organ is removed, replaced, or whenever a clamp or cautery is used
        ///     Check all parts and determine if the owner is still bleeding
        ///     <see cref="SurgeryComponent"/>
        /// </summary>
        public bool OrganBleeding = false;
        public bool PartBleeding = false;

        public float InitialOrganBloodloss = 15f;
        public float InitialPartBloodloss = 30f;

        public float SurgeryBleed = 0f;
        public float BasePartBleed = 20f;
        public float BaseOrganBleed = 15f;

        public float BleedLastChecked = 0f;
        public float BleedCheckInterval = 5f;

        /// <summary>
        ///     Necrosis timers
        ///     If a clamp has been on for too long and a part/organ is still attached to slot then deal cellular damage to the entity
        ///     When part/organ damage implemented, apply to part/organ in slot
        ///     <see cref="SurgeryComponent"/>
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Clamped = false;
        [ViewVariables(VVAccess.ReadWrite)]
        public Dictionary<EntityUid, float> ClampedTimes = new Dictionary<EntityUid, float>();
        public float BaseNecrosisTimeThreshold = 600f; //give them a decent amount of time to figure it out, say 10 minutes?

        public bool Necrosis = false;

        [DataField("necrosisDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier NecrosisDamage = default!; //if necrosis starts happening, give them time to work it out before they incur too much cellular damage

        public float ClampLastChecked = 0f;
        public float ClampCheckInterval = 1f;

        /// <summary>
        ///     Update whenever a part or skeleton is opened or closed
        ///     Check all parts and determine if the owner is still open or partially opened
        ///     Periodically apply blunt damage if owner is not buckled/prone (they can move from one buckle to another if done carefully)
        ///     <see cref="SurgeryComponent"/>
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Opened = false;

        [DataField("openedDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier OpenedDamage = default!;

        public float OpenedLastChecked = 0f;
        public float OpenedCheckInterval = 15f; //give them enough time to move someone to another bed, with an effective (but not actually) random chance of hurting them during

        /// <summary>
        ///     Update whenever the owner is sedated or the sedation timer runs out
        ///     If a procedure takes places while the owner is not sedated, apply airloss to represent shock
        ///     Airloss applied should be based on relevant shock values multiplied by any time mods (a slow or improper procedure will lead to more pain)
        ///     <see cref="SurgeryComponent"/>
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Sedated = false;

        [DataField("incisorShockDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier IncisorShockDamage = default!;//20f;

        [DataField("smallClampShockDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier SmallClampShockDamage = default!;//0f;

        [DataField("largeClampShockDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier LargeClampShockDamage = default!;//0f;

        [DataField("sawShockDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier SawShockDamage = default!;//75f;

        [DataField("drillShockDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier DrillShockDamage = default!;//25f;

        [DataField("sutureShockDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier SutureShockDamage = default!;//10f;

        [DataField("hardSutureShockDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier HardSutureShockDamage = default!;//15f;

        [DataField("cauterizerShockDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier CauterizerShockDamage = default!;//15f;

        [DataField("manipulatorShockDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier ManipulatorShockDamage = default!;//10f;

        [DataField("retractorShockDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier RetractorShockDamage = default!;//10f;

        public Dictionary<ToolUsage, DamageSpecifier> UsageShock = new Dictionary<ToolUsage, DamageSpecifier>();

        /// <summary>
        ///    The species of organs compatible with the entity (other than its own species)
        ///    Part mismatches are tracked per part and deal damage periodically n number of times
        /// </summary>
        [DataField("compatibleSpecies", required: true)]
        public List<string> CompatibleSpecies = new List<string>();

        [DataField("cellularRejectionDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier CellularRejectionDamage = default!;

        [DataField("rejectionCheckInterval")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float RejectionCheckInterval = 600f;

        [ViewVariables(VVAccess.ReadWrite)]
        public float RejectionLastChecked = 0f;
    }
}
