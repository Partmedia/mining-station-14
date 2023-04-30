using Content.Server.DoAfter;
using Content.Server.Hands.Components;
using Content.Shared.CombatMode;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Surgery;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using System.Threading;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Organ;

namespace Content.Server.Surgery
{
    public sealed class SurgerySystem : EntitySystem
    {
        [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
        [Dependency] private readonly SharedBodySystem _bodySystem = default!;
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SurgeryComponent, GetVerbsEvent<Verb>>(AddSurgeryVerb);
            SubscribeLocalEvent<SurgeryComponent, GetVerbsEvent<ExamineVerb>>(AddSurgeryExamineVerb);

            // BUI
            SubscribeLocalEvent<SurgeryComponent, SurgerySlotButtonPressed>(OnSurgeryButtonPressed);
        }

        /// <summary>
        /// Get body part slots for a body part (usually starting with then torso, then followed by limbs)
        /// </summary>
        private List<BodyPartSlot> GetBodyPartSlots(EntityUid? bodyPart)
        {

            List<BodyPartSlot> bodyPartSlots = new List<BodyPartSlot>();

            if (TryComp<BodyPartComponent>(bodyPart, out var bodyPartComp))
            {
                if (bodyPartComp.Children != null) {
                    foreach (KeyValuePair<string, BodyPartSlot> partSlot in bodyPartComp.Children)
                    {
                        bodyPartSlots.Add(partSlot.Value);
                    }
                }
            }

            return bodyPartSlots;
        }

        /// <summary>
        /// Get organ slots for a body part (usually the torso and head)
        /// </summary>
        private List<OrganSlot> GetOrganSlots()
        {
            List<OrganSlot> organSlots = new List<OrganSlot>();

            return organSlots;
        }

        /// <summary>
        /// Get all body part slots attached to everybody part attached to the initally submitted part (usually the torso to start)
        /// </summary>
        private List<BodyPartSlot> GetAllBodyPartSlots(EntityUid bodyOwner)
        {

            //using body uid, get root part slot's child (usually the torso)
            if (!TryComp<BodyComponent>(bodyOwner,out var body))
                return new List<BodyPartSlot>();

            if (body.Root == null)
                return new List<BodyPartSlot>();

            var rootPart = body.Root.Child;

            if (rootPart == null)
                return new List<BodyPartSlot>();

            //proceed to get all part slots
            var initialPartList = GetBodyPartSlots(rootPart);
            List<BodyPartSlot> additionalPartList = new List<BodyPartSlot>();
            //then check all parts from that
            for (var i = 0; i<initialPartList.Count; i++)
            {
                void RecursiveGetSlots(BodyPartSlot bodyPartSlot)
                {
                    if (bodyPartSlot.Child == null)
                        return;

                    var subPartList = GetBodyPartSlots(bodyPartSlot.Child);
                    for (var j = 0; j < subPartList.Count; j++)
                    {
                        RecursiveGetSlots(subPartList[j]);
                    }
                    additionalPartList.AddRange(subPartList);
                }
                RecursiveGetSlots(initialPartList[i]);
            }
            initialPartList.AddRange(additionalPartList);

            if(TryComp<BodyPartComponent>(rootPart, out var bodyPartComp) && bodyPartComp.ParentSlot != null)
                initialPartList.Add(bodyPartComp.ParentSlot);

            return initialPartList;
        }

        /// <summary>
        /// Check all submitted body parts, check if they are opened and if they are get all organ slots
        /// </summary>
        private List<OrganSlot> GetOpenPartOrganSlots(List<BodyPartSlot> bodyPartSlots)
        {
            List<OrganSlot> organSlots = new List<OrganSlot>();

            return organSlots;
        }

        private void UpdateUiState(SurgeryComponent component)
        {
            var bodyPartSlots = GetAllBodyPartSlots(component.Owner);
            //GetOpenPartOrganSlots

            var state = new SurgeryBoundUserInterfaceState(bodyPartSlots); //organPartSlots
            _userInterfaceSystem.TrySetUiState(component.Owner, SurgeryUiKey.Key, state);
        }


        private void OnSurgeryButtonPressed(EntityUid uid, SurgeryComponent component, SurgerySlotButtonPressed args)
        {
            if (args.Session.AttachedEntity is not { Valid: true } user ||
                !TryComp<HandsComponent>(user, out var userHands))
                return;

            //check for surgical tool in active hand
            //apply to slot - resulting in attachment of tool on slot, condition change of body/organ, removal of body part/organ, or opening of body container

            //if hand empty check for tool - remove tool if present


            //if (userHands.ActiveHandEntity != null && !hasEnt)
            //    
            //else if (userHands.ActiveHandEntity == null && hasEnt)
            //   
        }

        /// <summary>
        /// Opens main surgery interface
        /// </summary>
        public void StartOpeningSurgery(EntityUid user, SurgeryComponent component, bool openInCombat = false)
        {
            if (TryComp<SharedCombatModeComponent>(user, out var mode) && mode.IsInCombatMode && !openInCombat)
                return;

            if (TryComp<ActorComponent>(user, out var actor))
            {
                if (_userInterfaceSystem.SessionHasOpenUi(component.Owner, SurgeryUiKey.Key, actor.PlayerSession))
                    return;
                _userInterfaceSystem.TryOpen(component.Owner, SurgeryUiKey.Key, actor.PlayerSession);
                UpdateUiState(component);
            }
        }

        /// <summary>
        /// Adds additional surgery buttons for when an organ container is opened
        /// </summary>
        public void StartOpeningOrganContainer(EntityUid user, SurgeryComponent component, SurgerySlotButtonPressed args)
        {

        }

        /// <summary>
        /// Removes surgery buttons for when an organ container is closed
        /// </summary>
        public void StartClosingOrganContainer(EntityUid user, SurgeryComponent component, SurgerySlotButtonPressed args)
        {

        }

        private void AddSurgeryVerb(EntityUid uid, SurgeryComponent component, GetVerbsEvent<Verb> args)
        {
            if (args.Hands == null || !args.CanAccess || !args.CanInteract || args.Target == args.User)
                return;

            if (!EntityManager.TryGetComponent(args.User, out ActorComponent? actor))
                return;

            Verb verb = new()
            {
                Text = Loc.GetString("Perform Surgery"), //TODO loc
                Act = () => StartOpeningSurgery(args.User, component, true),
            };
            args.Verbs.Add(verb);
        }

        private void AddSurgeryExamineVerb(EntityUid uid, SurgeryComponent component, GetVerbsEvent<ExamineVerb> args)
        {
            if (args.Hands == null || !args.CanAccess || !args.CanInteract || args.Target == args.User)
                return;

            if (!HasComp<ActorComponent>(args.User))
                return;

            ExamineVerb verb = new()
            {
                Text = Loc.GetString("surgery-verb-get-data-text"),
                IconTexture = "/Textures/Interface/VerbIcons/outfit.svg.192dpi.png",
                Act = () => StartOpeningSurgery(args.User, component, true),
                Category = VerbCategory.Examine,
            };

            args.Verbs.Add(verb);
        }

        /// <summary>
        ///     Places item in user's active hand to a surgery slot.
        ///     Used for placing parts and surgical tools on parts
        /// </summary>
        private async void PlaceActiveHandItemInBodyPartSlot(EntityUid user, BodyPartSlot slot, SurgeryComponent component)
        {
            var userHands = Comp<HandsComponent>(user);

            bool Check()
            {
                if (userHands.ActiveHand?.HeldEntity is not { } held)
                {
                    user.PopupMessageCursor(Loc.GetString("surgery-component-not-holding-anything"));
                    return false;
                }

                if (!_handsSystem.CanDropHeld(user, userHands.ActiveHand))
                {
                    user.PopupMessageCursor(Loc.GetString("surgery-component-cannot-drop"));
                    return false;
                }

                //check if held entity is a surgical tool or a body part (or something else entirely)

                //if (!_bodySystem.BodyHasSlot(component.Owner, slot))
                //    return false;

                //if (!tool) {
                //if (_bodySystem.TryGetSlotEntity(component.Owner, slot, out _))
                //{
                //    user.PopupMessageCursor(Loc.GetString("surgery-component-item-slot-occupied", ("owner", component.Owner)));
                //    return false;
                //}

                //if (!_bodySystem.CanAttachPart(user, component.Owner, held, slot, out _))
                //{
                //    user.PopupMessageCursor(Loc.GetString("surgery-component-cannot-equip-message", ("owner", component.Owner)));
                //    return false;
                //}
                //} else {
                //          
                //    if(has part attachment component && _bodySystem.TryGetPartAttachment(component.Owner, slot, out _))
                //        return false;
                //
                //    if(has slot attachment component && _bodySystem.TryGetSlotAttachment(component.Owner, slot, out _))
                //        return false;
                //}

                return true;
            }

            var userEv = new BeforeSurgeryEvent(slot.BaseSurgeryTime);
            RaiseLocalEvent(user, userEv);
            var ev = new BeforeGettingSurgeryEvent(userEv.Time);
            RaiseLocalEvent(component.Owner, ev);

            var doAfterArgs = new DoAfterEventArgs(user, ev.Time, CancellationToken.None, component.Owner)
            {
                ExtraCheck = Check,
                BreakOnStun = true,
                BreakOnDamage = true,
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                NeedHand = true,
            };

            var result = await _doAfterSystem.WaitDoAfter(doAfterArgs);
            if (result != DoAfterStatus.Finished) return;

            if (userHands.ActiveHand?.HeldEntity is { } held
                && _handsSystem.TryDrop(user, userHands.ActiveHand, handsComp: userHands))
            {
                //check if held entity is a surgical tool or a body part (or something else entirely)
                //if (!tool) {
                //require attachment device in off hand?
                //AttachPart
                //} else {
                //interact tool? (cauterise, incise, attach)

                //if attach...
                //part or part slot attachment?
                //AttachPartAttachment
                //AttachPartSlotAttachment
                //}
            }
        }

        /// <summary>
        ///     Takes a tool or organ/part from the surgery slot
        /// </summary>
        private async void TakeItemFromBodyPartSlot(EntityUid user, BodyPartSlot slot, SurgeryComponent component)
        {
            bool Check()
            {
                //if (!_bodySystem.BodyHasSlot(component.Owner, slot))
                //    return false;

                //check if hand has tool

                //if no tool, check if slot has attachment

                //if tool, check if it is a removal tool and if there is a part (and it can be removed)


                //if (tool) {
                //!tool has part removal component
                //  return false
                //if (!_bodySystem.TryGetSlotEntity(component.Owner, slot, out _))
                //    return false;
                //}
                //if (!_bodySystem.CanRemovePart(user, component.Owner, held, slot, out _))
                //{
                //    user.PopupMessageCursor(Loc.GetString("surgery-component-cannot-unequip-message", ("owner", component.Owner)));
                //    return false;
                //}
                //else {
                //    if(!_bodySystem.TryGetSlotAttachment(component.Owner, slot, out _) && (!_bodySystem.TryGetPartAttachment(component.Owner, slot, out _))
                //        return false;
                //}


                return true;
            }

            var userEv = new BeforeSurgeryEvent(slot.BaseSurgeryTime);
            RaiseLocalEvent(user, userEv);
            var ev = new BeforeGettingSurgeryEvent(userEv.Time);
            RaiseLocalEvent(component.Owner, ev);

            var doAfterArgs = new DoAfterEventArgs(user, ev.Time, CancellationToken.None, component.Owner)
            {
                ExtraCheck = Check,
                BreakOnStun = true,
                BreakOnDamage = true,
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
            };

            var result = await _doAfterSystem.WaitDoAfter(doAfterArgs);
            if (result != DoAfterStatus.Finished) return;

            //check if hand has tool
            //if no tool, check for attachment
            //if tool, check if it is a removal tool and if there is a part (and it can be removed)

            //if (!tool) {
            //first check for part tool
            //RemovePartAttachment()
            //else check for part slot tool
            //RemovePartSlotAttachment
            //} else {
            //  RemovePart() or DropPart()?
            //}

        }

        /// <summary>
        ///     Places item in user's active hand to a surgery slot.
        ///     Used for placing organs
        /// </summary>
        private async void PlaceActiveHandItemInOrganSlot(EntityUid user, OrganSlot slot, SurgeryComponent component)
        {
           
        }

        /// <summary>
        ///     Takes an organ from the surgery slot
        /// </summary>
        private async void TakeItemFromBodyOrganSlot(EntityUid user, OrganSlot slot, SurgeryComponent component)
        {
            
        }

        private sealed class OpenSurgeryCompleteEvent
        {
            public readonly EntityUid User;

            public OpenSurgeryCompleteEvent(EntityUid user)
            {
                User = user;
            }
        }

        private sealed class OpenOrganContainerCompleteEvent
        {
            public readonly EntityUid User;

            public OpenOrganContainerCompleteEvent(EntityUid user)
            {
                User = user;
            }
        }

        private sealed class CloseOrganContainerCompleteEvent
        {
            public readonly EntityUid User;

            public CloseOrganContainerCompleteEvent(EntityUid user)
            {
                User = user;
            }
        }

        private sealed class OpenSurgeryCancelledEvent
        {
            public readonly EntityUid User;

            public OpenSurgeryCancelledEvent(EntityUid user)
            {
                User = user;
            }
        }
    }
}
