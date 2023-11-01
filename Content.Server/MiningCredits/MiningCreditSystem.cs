using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Server.Mind;
using Robust.Server.GameObjects;

namespace Content.Server.MiningCredits
{
    public sealed class MiningCreditSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MiningCreditComponent, MindTransferEvent>(OnMindTransferEvent);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var credit in EntityManager.EntityQuery<MiningCreditComponent>())
            {
                //get player session

                //check timing

                //if they are: online and an interval has passed - reward!
                //dead players still get paid, encouraging people to save them so they can get back to work
                //also means I don't have to worry about distinguishing brain transfers and ghosts roles
                //if the player is participating, they get cut in (though this may change in the future)
                
            }
        }

        private void OnMindTransferEvent(EntityUid uid, MiningCreditComponent component, MindTransferEvent args)
        {

            Logger.Debug("oldentity");
            Logger.Debug(args.OldEntity.ToString());
            Logger.Debug("newentity");
            Logger.Debug(args.NewEntity.ToString());

            //transfer credit component and all values to that entity

            //remove from old entity

        }
    }
}
