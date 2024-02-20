namespace Content.Server.MiningCredits
{
    [RegisterComponent, Access(typeof(MiningCreditSystem))]
    public sealed class MiningCreditComponent : Component
    {
        //Num credits miner has earned
        [ViewVariables(VVAccess.ReadWrite)]
        public int NumCredits = 0;

        //how often the miner gets a credit
        [DataField("rewardInterval")]
        public float RewardInterval = 60f;

        //num credits miner ears every interval
        [DataField("rewardNum")]
        public int RewardNum = 1;

        //seconds since last reward
        [ViewVariables(VVAccess.ReadWrite)]
        public float LastRewardInterval = 0f;

        //check if the mind has been transferred elsewhere
        //do not update or tally end of round if transferred
        [ViewVariables]
        public bool Transferred = false;

        //Entity uid of previous tracked entity
        [ViewVariables]
        public EntityUid? PreviousEntity;

        [ViewVariables]
        public string? PlayerName;
    }
}
