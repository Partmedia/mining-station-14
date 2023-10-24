using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Robust.Shared.Timing;
using JetBrains.Annotations;
using Content.Shared.Rejuvenate;

namespace Content.Server.Body.Systems
{
    [UsedImplicitly]
    public sealed class ToxinRemoverSystem : EntitySystem
    {

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ToxinRemoverComponent, RejuvenateEvent>(OnRejuvenate);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var (remover, body) in EntityManager.EntityQuery<ToxinRemoverComponent, BodyComponent>())
            {
                var uid = remover.Owner;

                if (remover.ToxinBuildUp >= remover.ToxinThreshold)
                    StopRemover(uid, remover);

                if (remover.Working)
                    remover.IntervalLastChecked += frameTime;

                if (remover.Working && remover.IntervalLastChecked >= remover.RegenerationInterval)
                {
                    remover.ToxinBuildUp -= remover.RegenerationAmount;

                    if (remover.ToxinBuildUp < 0)
                        remover.ToxinBuildUp = 0;

                    remover.IntervalLastChecked = 0;
                }
            }
        }

        //Stop the remover i.e. the kidneys no longer works for whatever reason
        public void StopRemover(EntityUid uid, ToxinRemoverComponent remover)
        {
            remover.Working = false;
        }

        private void OnRejuvenate(EntityUid uid, ToxinRemoverComponent remover, RejuvenateEvent args)
        {
            remover.Working = true;
            remover.ToxinBuildUp = 0f;
        }
    }  
}
