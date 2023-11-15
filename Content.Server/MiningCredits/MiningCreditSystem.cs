using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Server.Mind;
using Content.Server.Mind.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Enums;
using Content.Server.Ghost;

namespace Content.Server.MiningCredits
{
    public sealed class MiningCreditSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MiningCreditComponent, MindTransferEvent>(OnMindTransferEvent);
            SubscribeLocalEvent<MiningCreditComponent, ComponentShutdown>(OnShutdown);
        }

        //if an entity is deleted, take the reward num and session player name and store it here until it comes back
        public Dictionary<string, int> LostCredits = new Dictionary<string, int>();

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var credit in EntityManager.EntityQuery<MiningCreditComponent>())
            {
                //get player session
                if (credit.Transferred
                    || !TryComp<MindComponent>(credit.Owner, out var mind)
                    || mind.Mind is null
                    || mind.Mind.Session is null
                    || mind.Mind.Session.Status == SessionStatus.Disconnected)
                    continue;

                if (credit.PlayerName is null)
                    credit.PlayerName = mind.Mind.Session.Data.UserName;

                credit.LastRewardInterval += frameTime;
                //check timing
                if (credit.LastRewardInterval >= credit.RewardInterval)
                {
                    credit.NumCredits += credit.RewardNum;
                    credit.LastRewardInterval = 0;
                }

                //if they are: online and an interval has passed - reward!
                //dead players still get paid, encouraging people to save them so they can get back to work
                //also means I don't have to worry about distinguishing brain transfers and ghosts roles
                //if the player is participating, they get cut in (though this may change in the future)
            }

            if (LostCredits.Count > 0)
            {
                foreach (var mind in EntityManager.EntityQuery<MindComponent>())
                {
                    if (mind.Mind is null
                    || mind.Mind.Session is null
                    || mind.Mind.Session.Status == SessionStatus.Disconnected)
                        continue;

                    if (LostCredits.ContainsKey(mind.Mind.Session.Data.UserName))
                    {
                        if (TryComp<MiningCreditComponent>(mind.Owner, out var creditCheck))
                            LostCredits.Remove(mind.Mind.Session.Data.UserName);
                        else
                        {
                            var creditComponent = EntityManager.EnsureComponent<MiningCreditComponent>(mind.Owner);
                            creditComponent.NumCredits = LostCredits[mind.Mind.Session.Data.UserName];
                            LostCredits.Remove(mind.Mind.Session.Data.UserName);
                        }
                    }

                    if (LostCredits.Count < 0)
                        break;
                }
            }
        }

        private void OnMindTransferEvent(EntityUid uid, MiningCreditComponent component, MindTransferEvent args)
        {

            if (!TryComp<MiningCreditComponent>(args.OldEntity, out var oldCreditComp) || args.NewEntity == args.OldEntity)
                return;

            //transfer credit component and all values to that entity
            var newCreditComp = EntityManager.EnsureComponent<MiningCreditComponent>(args.NewEntity);
            newCreditComp.RewardInterval = oldCreditComp.RewardInterval;
            newCreditComp.RewardNum = oldCreditComp.RewardNum;
            newCreditComp.LastRewardInterval = oldCreditComp.LastRewardInterval;
            newCreditComp.NumCredits = oldCreditComp.NumCredits;
            newCreditComp.PlayerName = oldCreditComp.PlayerName;
            newCreditComp.Transferred = false;
            newCreditComp.PreviousEntity = args.OldEntity;

            //set old component to be transferred
            oldCreditComp.Transferred = true;
            
        }

        private void OnShutdown(EntityUid uid, MiningCreditComponent component, ComponentShutdown args)
        {
            if (component.PlayerName is not null)
                LostCredits[component.PlayerName] = component.NumCredits;
        }
    }
}
