using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Radiation.Events;
using Content.Shared.Rejuvenate;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Body.Organ;
using Robust.Shared.Random;

namespace Content.Shared.Damage
{
    public sealed class DamageableSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedBodySystem _body = default!;
        [Dependency] private readonly INetManager _netMan = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<DamageableComponent, ComponentInit>(DamageableInit);
            SubscribeLocalEvent<DamageableComponent, ComponentHandleState>(DamageableHandleState);
            SubscribeLocalEvent<DamageableComponent, ComponentGetState>(DamageableGetState);
            SubscribeLocalEvent<DamageableComponent, OnIrradiatedEvent>(OnIrradiated);
            SubscribeLocalEvent<DamageableComponent, RejuvenateEvent>(OnRejuvenate);
        }

        /// <summary>
        /// Retrieves the damage examine values.
        /// </summary>
        public FormattedMessage GetDamageExamine(DamageSpecifier damageSpecifier, string? type = null)
        {
            var msg = new FormattedMessage();

            if (string.IsNullOrEmpty(type))
            {
                msg.AddMarkup(Loc.GetString("damage-examine"));
            }
            else
            {
                msg.AddMarkup(Loc.GetString("damage-examine-type", ("type", type)));
            }

            foreach (var damage in damageSpecifier.DamageDict)
            {
                if (damage.Value != FixedPoint2.Zero)
                {
                    msg.PushNewline();
                    msg.AddMarkup(Loc.GetString("damage-value", ("type", damage.Key), ("amount", damage.Value)));
                }
            }

            return msg;
        }

        /// <summary>
        ///     Initialize a damageable component
        /// </summary>
        private void DamageableInit(EntityUid uid, DamageableComponent component, ComponentInit _)
        {
            if (component.DamageContainerID != null &&
                _prototypeManager.TryIndex<DamageContainerPrototype>(component.DamageContainerID,
                out var damageContainerPrototype))
            {
                // Initialize damage dictionary, using the types and groups from the damage
                // container prototype
                foreach (var type in damageContainerPrototype.SupportedTypes)
                {
                    component.Damage.DamageDict.TryAdd(type, FixedPoint2.Zero);
                }

                foreach (var groupID in damageContainerPrototype.SupportedGroups)
                {
                    var group = _prototypeManager.Index<DamageGroupPrototype>(groupID);
                    foreach (var type in group.DamageTypes)
                    {
                        component.Damage.DamageDict.TryAdd(type, FixedPoint2.Zero);
                    }
                }
            }
            else
            {
                // No DamageContainerPrototype was given. So we will allow the container to support all damage types
                foreach (var type in _prototypeManager.EnumeratePrototypes<DamageTypePrototype>())
                {
                    component.Damage.DamageDict.TryAdd(type.ID, FixedPoint2.Zero);
                }
            }

            component.DamagePerGroup = component.Damage.GetDamagePerGroup(_prototypeManager);
            component.TotalDamage = component.Damage.Total;
        }

        /// <summary>
        ///     Directly sets the damage specifier of a damageable component.
        /// </summary>
        /// <remarks>
        ///     Useful for some unfriendly folk. Also ensures that cached values are updated and that a damage changed
        ///     event is raised.
        /// </remarks>
        public void SetDamage(DamageableComponent damageable, DamageSpecifier damage)
        {
            damageable.Damage = damage;
            DamageChanged(damageable);
        }

        /// <summary>
        ///     If the damage in a DamageableComponent was changed, this function should be called.
        /// </summary>
        /// <remarks>
        ///     This updates cached damage information, flags the component as dirty, and raises a damage changed event.
        ///     The damage changed event is used by other systems, such as damage thresholds.
        /// </remarks>
        public void DamageChanged(DamageableComponent component, DamageSpecifier? damageDelta = null,
            bool interruptsDoAfters = true, EntityUid? origin = null)
        {
            component.DamagePerGroup = component.Damage.GetDamagePerGroup(_prototypeManager);
            component.TotalDamage = component.Damage.Total;
            Dirty(component);

            if (EntityManager.TryGetComponent<AppearanceComponent>(component.Owner, out var appearance) && damageDelta != null)
            {
                var data = new DamageVisualizerGroupData(damageDelta.GetDamagePerGroup(_prototypeManager).Keys.ToList());
                _appearance.SetData(component.Owner, DamageVisualizerKeys.DamageUpdateGroups, data, appearance);
            }
            RaiseLocalEvent(component.Owner, new DamageChangedEvent(component, damageDelta, interruptsDoAfters, origin));
        }

        /// <summary>
        ///     Applies damage specified via a <see cref="DamageSpecifier"/>.
        /// </summary>
        /// <remarks>
        ///     <see cref="DamageSpecifier"/> is effectively just a dictionary of damage types and damage values. This
        ///     function just applies the container's resistances (unless otherwise specified) and then changes the
        ///     stored damage data. Division of group damage into types is managed by <see cref="DamageSpecifier"/>.
        /// </remarks>
        /// <returns>
        ///     Returns a <see cref="DamageSpecifier"/> with information about the actual damage changes. This will be
        ///     null if the user had no applicable components that can take damage.
        /// </returns>
        public DamageSpecifier? TryChangeDamage(EntityUid? uid, DamageSpecifier damage, bool ignoreResistances = false,
            bool interruptsDoAfters = true, DamageableComponent? damageable = null, EntityUid? origin = null, bool unblockable = false)
        {
            if (!uid.HasValue || !Resolve(uid.Value, ref damageable, false))
            {
                // TODO BODY SYSTEM pass damage onto body system
                return null;
            }

            if (damage == null)
            {
                Logger.Error("Null DamageSpecifier. Probably because a required yaml field was not given.");
                return null;
            }

            if (damage.Empty)
            {
                return damage;
            }

            // Apply resistances
            if (!ignoreResistances)
            {
                if (damageable.DamageModifierSetId != null &&
                    _prototypeManager.TryIndex<DamageModifierSetPrototype>(damageable.DamageModifierSetId, out var modifierSet))
                {
                    damage = DamageSpecifier.ApplyModifierSet(damage, modifierSet);
                }

                var ev = new DamageModifyEvent(damage);
                RaiseLocalEvent(uid.Value, ev, false);
                damage = ev.Damage;

                if (damage.Empty)
                {
                    return damage;
                }
            }

            // Copy the current damage, for calculating the difference
            DamageSpecifier oldDamage = new(damageable.Damage);

            damageable.Damage.ExclusiveAdd(damage);
            damageable.Damage.ClampMin(FixedPoint2.Zero);

            var delta = damageable.Damage - oldDamage;
            delta.TrimZeros();

            if (!delta.Empty)
            {
                //check for body component
                if (TryComp<BodyComponent>(uid, out var body))
                {
                    //TODO properly parameterise this - cba right now
                    List<string> integrityDamages = new List<string> { "Blunt", "Piercing", "Slash" };
                    List<string> containerDamages = new List<string> { "Piercing" };
                    List<string> criticalDamages = new List<string> { "Slash" };

                    //init these here so if multi damage is at play they all hit the same part/organ
                    var hitPartIndex = -1;
                    var organHitPartIndex = -1;

                    //check if damage brute (blunt, piercing, slash)
                    foreach (KeyValuePair<string, FixedPoint2> entry in delta.DamageDict)
                    {
                        var damageType = entry.Key;
                        var damageValue = entry.Value;

                        if (!integrityDamages.Contains(damageType))
                            continue;

                        if (body.Root is not null && body.Root.Child is not null && TryComp<BodyPartComponent>(body.Root.Child.Value, out var rootPart))
                        {
                            //Get all (non-wearable) parts
                            var bodyPartSlots = _body.GetAllBodyPartSlots(body.Root.Child.Value,rootPart);
                            List<BodyPartComponent> bodyParts = new List<BodyPartComponent> { };
                            foreach (var slot in bodyPartSlots)
                            {
                                if (slot.Child != null && TryComp<BodyPartComponent>(slot.Child.Value, out var part)) //TODO && !part.Wearable
                                    bodyParts.Add(part);
                            }
                            bodyParts.Add(rootPart);


                            if (hitPartIndex < 0)
                            {
                                List<List<int>> hitRanges = new List<List<int>> { };
                                var hitChanceTotal = 0;
                                //Get all HitChance values of all parts, add together for total, generate random number from 0 to total
                                for (var i = 0; i < bodyParts.Count(); i++)
                                {
                                    List<int> range = new List<int> { hitChanceTotal + 1, hitChanceTotal + bodyParts[i].HitChance };
                                    hitChanceTotal += bodyParts[i].HitChance;
                                    hitRanges.Add(range);
                                }

                                if (hitChanceTotal < 1)
                                    continue;

                                var hitNum = _random.Next(1, hitChanceTotal + 1);
                                hitPartIndex = 0;
                                for (var i = 0; i < hitRanges.Count(); i++)
                                {
                                    //select part that the number falls in range of
                                    if (hitNum >= hitRanges[i][0] && hitNum <= hitRanges[i][1])
                                    {
                                        hitPartIndex = i;
                                        break;
                                    }
                                }
                            }
                            var hitPart = bodyParts[hitPartIndex];

                            if (hitPart.Container && containerDamages.Contains(damageType))
                            {
                                //if the selected part is an organ container and the damage is piercing,
                                //generate a random number from 1 to the max integrity of the container part
                                var organDamage = (float) _random.Next(1, (int) Math.Round((double) hitPart.MaxIntegrity) + 1);
                                //if the number is greater than the current integrity of the part,
                                //redirect the difference (if available) to an organ
                                if (organDamage > hitPart.Integrity)
                                {
                                    organDamage -= hitPart.Integrity;
                                    if (organDamage > damageValue)
                                        organDamage = (int) Math.Round((double) damageValue);
                                    damageValue -= organDamage;


                                    List<OrganComponent> partOrgans = new List<OrganComponent> { };
                                    foreach (KeyValuePair<string, OrganSlot> organSlot in hitPart.Organs)
                                    {
                                        if (organSlot.Value.Child is not null && TryComp<OrganComponent>(organSlot.Value.Child.Value, out var organ))
                                            partOrgans.Add(organ);
                                    }

                                    List<List<int>> organHitRanges = new List<List<int>> { };
                                    var organHitChanceTotal = 0;
                                    //Get all HitChance values of all parts, add together for total, generate random number from 0 to total
                                    for (var i = 0; i < partOrgans.Count(); i++)
                                    {
                                        List<int> range = new List<int> { organHitChanceTotal + 1, organHitChanceTotal + partOrgans[i].HitChance };
                                        organHitChanceTotal += partOrgans[i].HitChance;
                                        organHitRanges.Add(range);
                                    }

                                    if (organHitChanceTotal > 0)
                                    {
                                        if (organHitPartIndex < 0)
                                        {
                                            var organHitNum = _random.Next(1, organHitChanceTotal + 1);
                                            organHitPartIndex = 0;
                                            for (var i = 0; i < organHitRanges.Count(); i++)
                                            {
                                                //select part that the number falls in range of
                                                if (organHitNum >= organHitRanges[i][0] && organHitNum <= organHitRanges[i][1])
                                                {
                                                    organHitPartIndex = i;
                                                    break;
                                                }
                                            }
                                        }

                                        var hitOrgan = partOrgans[organHitPartIndex];

                                        _body.ChangeOrganIntegrity(hitOrgan.Owner, hitOrgan, organDamage);
                                    }
                                }
                            }

                            //after (and if) organ damage is subtracted, apply damage to part
                            var isRoot = false;
                            if (body.Root is not null && body.Root.Child is not null && body.Root.Child.Value == hitPart.Owner)
                                isRoot = true;

                            //if the not part is not root and the damage type is slash, roll for a crit hit
                            if (!isRoot && criticalDamages.Contains(damageType))
                            {
                                //roll from 1 to max integrity, if the result is greater than the part's current integrity,
                                //apply integrity damage equal to current integrity
                                var critHit = _random.Next(1, (int) Math.Round((double) hitPart.MaxIntegrity) + 1);

                                if (critHit > hitPart.Integrity - damageValue)
                                {
                                    _body.ChangePartIntegrity(hitPart.Owner, hitPart, hitPart.Integrity, isRoot);
                                }
                                //otherwise, apply integrity damage as normal
                                else
                                {
                                    _body.ChangePartIntegrity(hitPart.Owner, hitPart, damageValue, isRoot);
                                }
                            }
                            else
                            {
                                _body.ChangePartIntegrity(hitPart.Owner, hitPart, damageValue, isRoot);
                            }
                        }
                    }
                }
                DamageChanged(damageable, delta, interruptsDoAfters, origin);             
            }

            return delta;
        }

        /// <summary>
        ///     Sets all damage types supported by a <see cref="DamageableComponent"/> to the specified value.
        /// </summary>
        /// <remakrs>
        ///     Does nothing If the given damage value is negative.
        /// </remakrs>
        public void SetAllDamage(DamageableComponent component, FixedPoint2 newValue)
        {
            if (newValue < 0)
            {
                // invalid value
                return;
            }

            foreach (var type in component.Damage.DamageDict.Keys)
            {
                component.Damage.DamageDict[type] = newValue;
            }

            // Setting damage does not count as 'dealing' damage, even if it is set to a larger value, so we pass an
            // empty damage delta.
            DamageChanged(component, new DamageSpecifier());
        }

        public void SetDamageModifierSetId(EntityUid uid, string damageModifierSetId, DamageableComponent? comp = null)
        {
            if (!Resolve(uid, ref comp))
                return;

            comp.DamageModifierSetId = damageModifierSetId;

            Dirty(comp);
        }

        private void DamageableGetState(EntityUid uid, DamageableComponent component, ref ComponentGetState args)
        {
            if (_netMan.IsServer)
            {
                args.State = new DamageableComponentState(component.Damage.DamageDict, component.DamageModifierSetId);
            }
            else
            {
                // avoid mispredicting damage on newly spawned entities.
                args.State = new DamageableComponentState(component.Damage.DamageDict.ShallowClone(), component.DamageModifierSetId);
            }
        }

        private void OnIrradiated(EntityUid uid, DamageableComponent component, OnIrradiatedEvent args)
        {
            if (args.TotalRads <= 0) return;
            var damageValue = FixedPoint2.New(args.TotalRads);

            // Radiation should really just be a damage group instead of a list of types.
            DamageSpecifier damage = new();
            foreach (var typeId in component.RadiationDamageTypeIDs)
            {
                damage.DamageDict.Add(typeId, damageValue);
            }

            TryChangeDamage(uid, damage);
        }

        private void OnRejuvenate(EntityUid uid, DamageableComponent component, RejuvenateEvent args)
        {
            SetAllDamage(component, 0);
        }

        private void DamageableHandleState(EntityUid uid, DamageableComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not DamageableComponentState state)
            {
                return;
            }

            component.DamageModifierSetId = state.ModifierSetId;

            // Has the damage actually changed?
            DamageSpecifier newDamage = new() { DamageDict = new(state.DamageDict) };
            var delta = component.Damage - newDamage;
            delta.TrimZeros();

            if (!delta.Empty)
            {
                component.Damage = newDamage;
                DamageChanged(component, delta);
            }
        }
    }

    /// <summary>
    ///     Raised on an entity when damage is about to be dealt,
    ///     in case anything else needs to modify it other than the base
    ///     damageable component.
    ///
    ///     For example, armor.
    /// </summary>
    public sealed class DamageModifyEvent : EntityEventArgs, IInventoryRelayEvent
    {
        // Whenever locational damage is a thing, this should just check only that bit of armour.
        public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;

        public DamageSpecifier Damage;

        public DamageModifyEvent(DamageSpecifier damage)
        {
            Damage = damage;
        }
    }

    public sealed class DamageChangedEvent : EntityEventArgs
    {
        /// <summary>
        ///     This is the component whose damage was changed.
        /// </summary>
        /// <remarks>
        ///     Given that nearly every component that cares about a change in the damage, needs to know the
        ///     current damage values, directly passing this information prevents a lot of duplicate
        ///     Owner.TryGetComponent() calls.
        /// </remarks>
        public readonly DamageableComponent Damageable;

        /// <summary>
        ///     The amount by which the damage has changed. If the damage was set directly to some number, this will be
        ///     null.
        /// </summary>
        public readonly DamageSpecifier? DamageDelta;

        /// <summary>
        ///     Was any of the damage change dealing damage, or was it all healing?
        /// </summary>
        public readonly bool DamageIncreased = false;

        /// <summary>
        ///     Does this event interrupt DoAfters?
        ///     Note: As provided in the constructor, this *does not* account for DamageIncreased.
        ///     As written into the event, this *does* account for DamageIncreased.
        /// </summary>
        public readonly bool InterruptsDoAfters = false;

        /// <summary>
        ///     Contains the entity which caused the change in damage, if any was responsible.
        /// </summary>
        public readonly EntityUid? Origin;

        public DamageChangedEvent(DamageableComponent damageable, DamageSpecifier? damageDelta, bool interruptsDoAfters, EntityUid? origin)
        {
            Damageable = damageable;
            DamageDelta = damageDelta;
            Origin = origin;

            if (DamageDelta == null)
                return;

            foreach (var damageChange in DamageDelta.DamageDict.Values)
            {
                if (damageChange > 0)
                {
                    DamageIncreased = true;
                    break;
                }
            }
            InterruptsDoAfters = interruptsDoAfters && DamageIncreased;
        }
    }
}
