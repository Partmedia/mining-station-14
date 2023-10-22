using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Mobs.Systems;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.Damage;
using Content.Shared.Rejuvenate;
using Content.Server.Surgery;
using Content.Server.Nutrition.Components;
using Robust.Shared.Random;
using Content.Shared.Damage;
using Content.Shared.Rejuvenate;
using Content.Server.Surgery;

namespace Content.Server.Body.Systems
{
    [UsedImplicitly]
    public sealed class CirculatoryPumpSystem : EntitySystem
    {
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;
        [Dependency] private readonly SurgerySystem _surgerySystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<CirculatoryPumpComponent, RejuvenateEvent>(OnRejuvenate);
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
                    }

                    if (!pump.Working && !_mobState.IsDead(uid))
                        _damageable.TryChangeDamage(uid, pump.NotWorkingDamage, true, origin: uid);

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

                        if (!pump.Working && !_mobState.IsDead(uid))
                            _damageable.TryChangeDamage(uid, pump.NotWorkingDamage, true, origin: uid);

                        if (!hasBrain)
                        {
                            StopPump(uid, pump);
                        }
                    }

                    if (pump.Working && TryComp<HungerComponent>(uid, out var hunger))
                    {
                        //check overfed ceiling
                        if (hunger.OverfedStrain > 0) {
                            //if the current strain is less than the overfed ceiling, update the strain to equal the overfed ceiling
                            if (pump.Strain < hunger.OverfedStrain - pump.StrainMod && pump.StrainCeiling < hunger.OverfedStrain - pump.StrainMod)
                            {
                                pump.Strain = hunger.OverfedStrain - pump.StrainMod;
                                pump.StrainCeiling = pump.Strain;
                            } 

                        }
                        else
                            pump.StrainCeiling = 0;

                        if (pump.Strain > 0)
                        {
                            pump.Strain -= pump.StrainRecovery;
                            if (pump.Strain < 0)
                                pump.Strain = 0f;
                        }

                        //take the current strain and roll to apply damage
                        if (pump.Strain > 0) {
                            var roll = (int) _random.Next(1, 100);
                            if (roll <= (pump.Strain*pump.StrainDamageMod) * 100)
                            {
                                pump.Damage += pump.Strain * pump.StrainDamageMod;
                                if (pump.Damage > pump.MaxDamage)
                                    pump.Damage = pump.MaxDamage;
                            }
                        }

                        //take the current damage and roll to incur a heart attack
                        if (pump.Damage >= pump.MinDamageThreshold)
                        {
                            var roll = (int) _random.Next(1, 100);
                            if (roll <= ((pump.Damage-pump.MinDamageThreshold) * pump.DamageMod) * 100)
                            {
                                StopPump(uid, pump);
                            }
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

        private void OnRejuvenate(EntityUid uid, CirculatoryPumpComponent pump, RejuvenateEvent args)
        {
            pump.Working = true;
        }
    }  
}
