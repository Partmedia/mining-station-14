using Content.Server.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Body.Organ;

namespace Content.Server.Body.Components
{
    [RegisterComponent, Access(typeof(StomachSystem))]
    public sealed class StomachComponent : Component
    {
        public float AccumulatedFrameTime;

        /// <summary>
        ///     How fast should this component update, in seconds?
        /// </summary>
        [DataField("updateInterval")]
        public float UpdateInterval = 1.0f;

        /// <summary>
        ///     What solution should this stomach push reagents into, on the body?
        /// </summary>
        [DataField("bodySolutionName")]
        public string BodySolutionName = BloodstreamComponent.DefaultChemicalsSolutionName;

        /// <summary>
        ///     Initial internal solution storage volume
        /// </summary>
        [DataField("initialMaxVolume", readOnly: true)]
        public readonly FixedPoint2 InitialMaxVolume = FixedPoint2.New(50);

        /// <summary>
        ///     Time in seconds between reagents being ingested and them being
        ///     transferred to <see cref="BloodstreamComponent"/>
        /// </summary>
        [DataField("digestionDelay")]
        public float DigestionDelay = 20;

        //if the organ accumulates this much toxicity at once, it fails
        [DataField("toxinThreshold")]
        public float ToxinThreshold = 100f;

        //if the organ accumulates this much toxicity at once, it temporarily becomes worse
        [DataField("buildUpThreshold")]
        public float BuildUpThreshold = 50f;

        //the organ self heals over time
        [DataField("regenerationAmount")]
        [ViewVariables]
        public float RegenerationAmount = 10f;

        [DataField("regenerationInterval")]
        public float RegenerationInterval = 60f;

        [ViewVariables]
        public float IntervalLastChecked = 0f;

        [ViewVariables]
        public float ToxinBuildUp = 0f;

        //Current Condition
        [ViewVariables]
        [Access(typeof(StomachSystem), Other = AccessPermissions.ReadExecute)]
        public OrganCondition Condition = OrganCondition.Good;

        //Warning Damage
        public float WarningDamage = 50f;

        //Critical Damage
        public float CriticalDamage = 80f;

        //if this is false, the organ does not work
        [ViewVariables]
        public bool Working = true;

        public List<string> Toxins = new List<string> { "Poison" };

        /// <summary>
        ///     Used to track how long each reagent has been in the stomach
        /// </summary>
        [ViewVariables]
        public readonly List<ReagentDelta> ReagentDeltas = new();

        /// <summary>
        ///     Used to track quantity changes when ingesting & digesting reagents
        /// </summary>
        public sealed class ReagentDelta
        {
            public readonly string ReagentId;
            public readonly FixedPoint2 Quantity;
            public float Lifetime { get; private set; }

            public ReagentDelta(string reagentId, FixedPoint2 quantity)
            {
                ReagentId = reagentId;
                Quantity = quantity;
                Lifetime = 0.0f;
            }

            public void Increment(float delta) => Lifetime += delta;
        }
    }
}
