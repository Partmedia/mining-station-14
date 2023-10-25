using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Robust.Shared.Timing;
using JetBrains.Annotations;
using Content.Shared.Rejuvenate;
using Content.Server.Popups;
using Content.Shared.Body.Organ;

namespace Content.Server.Body.Systems
{
    [UsedImplicitly]
    public sealed class ToxinFilterSystem : EntitySystem
    {

        [Dependency] private readonly PopupSystem _popupSystem = default!;

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

                UpdateFilterStatus(uid,filter);

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

        public void UpdateFilterStatus(EntityUid body, ToxinFilterComponent filter)
        {
            //Update and signal damage
            if (filter.ToxinBuildUp >= filter.CriticalDamage && filter.Working)
            {
                if (filter.Condition != OrganCondition.Critical && filter.Condition != OrganCondition.Failure)
                    _popupSystem.PopupEntity("you feel a sharp pain in your abdomen", body, body); //TODO loc
                filter.Condition = OrganCondition.Critical;
            }
            else if (filter.ToxinBuildUp >= filter.WarningDamage && filter.Working)
            {
                if (filter.Condition != OrganCondition.Critical && filter.Condition != OrganCondition.Warning)
                    _popupSystem.PopupEntity("you feel a mild pain in your abdomen", body, body); //TODO loc
                filter.Condition = OrganCondition.Warning;
            }
            else if (filter.Working)
            {
                filter.Condition = OrganCondition.Good;
            }
        }

        //Stop the filter i.e. the liver no longer works for whatever reason
        public void StopFilter(EntityUid uid, ToxinFilterComponent filter)
        {
            if (filter.Working)
            {
                _popupSystem.PopupEntity("you feel a short sharp pain in your abdomen", uid, uid);
                filter.Working = false;
                filter.Condition = OrganCondition.Failure;
            }
        }

        private void OnRejuvenate(EntityUid uid, ToxinFilterComponent filter, RejuvenateEvent args)
        {
            filter.Working = true;
            filter.ToxinBuildUp = 0f;
            filter.Condition = OrganCondition.Good;
        }
    }  
}
