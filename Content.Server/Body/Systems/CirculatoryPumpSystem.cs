using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Mobs.Systems;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Server.Surgery;

namespace Content.Server.Body.Systems
{
    [UsedImplicitly]
    public sealed class CirculatoryPumpSystem : EntitySystem
    {
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly SurgerySystem _surgerySystem = default!;

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var (pump, body) in EntityManager.EntityQuery<CirculatoryPumpComponent, BodyComponent>())
            {
                var uid = pump.Owner;

                pump.IntervalLastChecked += frameTime;

                if (pump.IntervalLastChecked >= pump.CheckInterval)
                {
                    //if the mob is dead, stop the heart and move one
                    if (_mobState.IsDead(uid) && pump.Working)
                    {
                        StopPump(uid, pump);
                        continue;
                    }

                    if (!pump.Brainless)
                    {
                        //mob MUST have a brain, else stop the pump  
                        var organs = _surgerySystem.GetAllBodyOrgans(uid);
                        var hasBrain = false;
                        foreach (var organ in organs)
                        {
                            if (TryComp<BrainComponent>(organ, out var brain))
                            {
                                hasBrain = true;
                                break;
                            }
                        }

                        if (!hasBrain)
                        {
                            StopPump(uid, pump);
                        }
                    }
                    pump.IntervalLastChecked = 0;
                }
            }
        }

        //Stop the pump i.e. the heart no longer works for whatever reason
        public void StopPump(EntityUid uid, CirculatoryPumpComponent pump)
        {
            pump.Working = false;
        }

        //Start the heart back up! Does not work if the mob is dead...
        public void StartPump(EntityUid uid, CirculatoryPumpComponent pump)
        {
            if (!_mobState.IsDead(uid))
            {
                pump.Working = true;
            }
        }
    }  
}
