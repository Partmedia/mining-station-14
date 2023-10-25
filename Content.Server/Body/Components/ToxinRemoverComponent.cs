using Content.Server.Body.Systems;
using Content.Shared.Body.Organ;

namespace Content.Server.Body.Components
{
    [RegisterComponent]
    public sealed class ToxinRemoverComponent : Component
    {
        //if the organ accumulates this much toxicity at once, it fails
        [DataField("toxinThreshold")]
        public float ToxinThreshold = 300f;

        //if the organ accumulates this much toxicity at once, it temporarily becomes worse
        [DataField("buildUpThreshold")]
        public float BuildUpThreshold = 150f;

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

        //if this is false, the organ does not work
        [ViewVariables]
        public bool Working = true;

        [DataField("toxinRemovalRate")]
        public float ToxinRemovalRate = 1.0f;

        public float BuildUpRemovalMod = 0.01f;

        //Current Condition
        [ViewVariables]
        public OrganCondition Condition = OrganCondition.Good;

        //Warning Damage
        public float WarningDamage = 150f;

        //Critical Damage
        public float CriticalDamage = 250f;

        //if the ToxinRemover is embedded, do not remove it from its host whenever an organ is removed
        [DataField("embedded")]
        public bool Embedded = false;
    }
}
