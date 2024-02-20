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
    public sealed class ToxinRemoverSystem : EntitySystem
    {

        [Dependency] private readonly PopupSystem _popupSystem = default!;

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

                UpdateRemoverStatus(uid, remover);

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
            if (remover.Working)
            {
                _popupSystem.PopupEntity("you feel a short sharp pain in your abdomen", uid, uid);
                remover.Working = false;
                remover.Condition = OrganCondition.Failure;
            }
        }

        public void UpdateRemoverStatus(EntityUid body, ToxinRemoverComponent remover)
        {
            //Update and signal damage
            if (remover.ToxinBuildUp >= remover.CriticalDamage && remover.Working)
            {
                if (remover.Condition != OrganCondition.Critical && remover.Condition != OrganCondition.Failure)
                    _popupSystem.PopupEntity("you feel a sharp pain in your abdomen", body, body); //TODO loc
                remover.Condition = OrganCondition.Critical;
            }
            else if (remover.ToxinBuildUp >= remover.WarningDamage && remover.Working)
            {
                if (remover.Condition != OrganCondition.Critical && remover.Condition != OrganCondition.Warning)
                    _popupSystem.PopupEntity("you feel a mild pain in your abdomen", body, body); //TODO loc
                remover.Condition = OrganCondition.Warning;
            }
            else if (remover.Working)
            {
                remover.Condition = OrganCondition.Good;
            }
        }

        private void OnRejuvenate(EntityUid uid, ToxinRemoverComponent remover, RejuvenateEvent args)
        {
            remover.Working = true;
            remover.ToxinBuildUp = 0f;
            remover.Condition = OrganCondition.Good;
        }
    }  
}
