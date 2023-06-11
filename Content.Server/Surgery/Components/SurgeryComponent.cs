
namespace Content.Server.Surgery
{
    [RegisterComponent]
    [Access(typeof(SurgerySystem))]
    public sealed class SurgeryComponent : Component
    {
        /// <summary>
        ///     Update whenever a part or organ is removed, replaced, or whenever a clamp or cautery is used
        ///     Check all parts and determine if the owner is still bleeding
        ///     Periodically apply bleeding damage if they are
        ///     <see cref="SurgeryComponent"/>
        /// </summary>
        public bool Bleeding = false;

        /// <summary>
        ///     Update whenever a part or skeleton is opened or closed
        ///     Check all parts and determine if the owner is still open or partially opened
        ///     Periodically apply blunt damage if owner moves and is not buckled (they can move from one buckle to another if done carefully)
        ///     <see cref="SurgeryComponent"/>
        /// </summary>
        public bool Opened = false;

        /// <summary>
        ///     Update whenever the owner is sedated or the sedation timer runs out
        ///     If a procedure takes places while the owner is not sedated, apply airloss to represent shock
        ///     Airloss applied should be based on relevant shock values multiplied by any time mods (a slow or improper procedure will lead to more pain)
        ///     <see cref="SurgeryComponent"/>
        /// </summary>
        public bool Sedated = false;

        [DataField("incisorShockDamage")]
        public float IncisorShockDamage = 20f;

        [DataField("smallClampShockDamage")]
        public float SmallClampShockDamage = 0f;

        [DataField("largeClampShockDamage")]
        public float LargeClampShockDamage = 0f;

        [DataField("sawShockDamage")]
        public float SawShockDamage = 75f;

        [DataField("drillShockDamage")]
        public float DrillShockDamage = 25f;

        [DataField("sutureShockDamage")]
        public float SutureShockDamage = 10f;

        [DataField("hardSutureShockDamage")]
        public float HardSutureShockDamage = 15f;

        [DataField("cauterizerShockDamage")]
        public float CauterizerShockDamage = 15f;

        [DataField("manipulatorShockDamage")]
        public float ManipulatorShockDamage = 10f;

        [DataField("retractorShockDamage")]
        public float RetractorShockDamage = 10f;

        /// <summary>
        ///     Update based on the sedative applied, then periodically update
        ///     Sedative will stop working whenever this value reaches or otherwise equals 0
        ///     Note that sleep is not necessarily the same as sedation and that one may outlast the other if they happen to occur at the same time
        ///     <see cref="SurgeryComponent"/>
        /// </summary>
        public bool SedationTime = 0f;
    }
}
