using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Robust.Shared.Timing;
using JetBrains.Annotations;
using Content.Shared.Rejuvenate;

namespace Content.Server.Body.Systems
{
    [UsedImplicitly]
    public sealed class ToxinFilterSystem : EntitySystem
    {

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ToxinFilterComponent, RejuvenateEvent>(OnRejuvenate);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var (filter, body) in EntityManager.EntityQuery<ToxinFilterComponent, BodyComponent>())
            {
                var uid = filter.Owner;

                if (filter.ToxinBuildUp >= filter.ToxinThreshold)
                    StopFilter(uid, filter);

                if (filter.Working)
                    filter.IntervalLastChecked += frameTime;

                if (filter.Working && filter.IntervalLastChecked >= filter.RegenerationInterval)
                {
                    filter.ToxinBuildUp -= filter.RegenerationAmount;

                    if (filter.ToxinBuildUp < 0)
                        filter.ToxinBuildUp = 0;

                    filter.IntervalLastChecked = 0;
                }
            }
        }

        //Stop the filter i.e. the liver no longer works for whatever reason
        public void StopFilter(EntityUid uid, ToxinFilterComponent filter)
        {
            filter.Working = false;
        }

        private void OnRejuvenate(EntityUid uid, ToxinFilterComponent filter, RejuvenateEvent args)
        {
            filter.Working = true;
            filter.ToxinBuildUp = 0f;
        }
    }  
}
