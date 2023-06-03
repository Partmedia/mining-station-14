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
using Content.Shared.Body.Events;
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

            SubscribeLocalEvent<SurgeryComponent, AddedToBodyEvent>((_, _, args) => UpdateUiState(args.Body));
            SubscribeLocalEvent<SurgeryComponent, AddedToPartEvent>((_, _, args) => UpdateUiState(args.Part));
            SubscribeLocalEvent<SurgeryComponent, AddedToPartInBodyEvent>((_, _, args) => UpdateUiState(args.Body));
            SubscribeLocalEvent<SurgeryComponent, RemovedFromBodyEvent>((_, _, args) => UpdateUiState(args.Old));
            SubscribeLocalEvent<SurgeryComponent, RemovedFromPartEvent>((_, _, args) => UpdateUiState(args.Old));
            SubscribeLocalEvent<SurgeryComponent, RemovedFromPartInBodyEvent>((_, _, args) => UpdateUiState(args.OldBody));

            SubscribeLocalEvent<SurgeryComponent, BodyPartAddedEvent>(OnBodyPartAdded);
            SubscribeLocalEvent<SurgeryComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);

            // BUI
            SubscribeLocalEvent<SurgeryComponent, SurgerySlotButtonPressed>(OnSurgeryButtonPressed);
            SubscribeLocalEvent<SurgeryComponent, OrganSlotButtonPressed>(OnOrganButtonPressed);
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

            EntityUid? rootPart; // = body.Root.Child;

            //using body uid, get root part slot's child (usually the torso)
            if (TryComp<BodyComponent>(bodyOwner, out var body))
            {
                
                if (body.Root == null)
                    return new List<BodyPartSlot>();

                rootPart = body.Root.Child;

                if (rootPart == null)
                    return new List<BodyPartSlot>();

            } else if (TryComp<BodyPartComponent>(bodyOwner, out var bodyPart)) {
                rootPart = bodyOwner;
            } else
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

            if (TryComp<BodyPartComponent>(rootPart, out var bodyPartComp) && bodyPartComp.ParentSlot != null)
            {
                initialPartList.Add(bodyPartComp.ParentSlot);
            }
            else if (bodyPartComp != null && rootPart != null)
            {
                var tempSelfSlot = new BodyPartSlot("self", rootPart.Value, bodyPartComp.PartType);
                tempSelfSlot.Child = rootPart;
                tempSelfSlot.IsRoot = true;
                initialPartList.Add(tempSelfSlot);
            }

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

        private void UpdateUiState(EntityUid uid)
        {
            var bodyPartSlots = GetAllBodyPartSlots(uid);
            //GetOpenPartOrganSlots

            var state = new SurgeryBoundUserInterfaceState(bodyPartSlots); //organPartSlots
            _userInterfaceSystem.TrySetUiState(uid, SurgeryUiKey.Key, state);
        }

        private void OnBodyPartAdded(EntityUid uid, SurgeryComponent component, ref BodyPartAddedEvent args)
        {
            UpdateUiState(uid);
        }

        private void OnBodyPartRemoved(EntityUid uid, SurgeryComponent component, ref BodyPartRemovedEvent args)
        {
            UpdateUiState(uid);
        }

        private void OnSurgeryButtonPressed(EntityUid uid, SurgeryComponent component, SurgerySlotButtonPressed args)
        {
            if (args.Session.AttachedEntity is not { Valid: true } user ||
                !TryComp<HandsComponent>(user, out var userHands))
                return;

            Logger.Debug(user.ToString());

            //check for surgical tool in active hand   
            if (userHands.ActiveHandEntity != null && TryComp<SurgeryToolComponent>(userHands.ActiveHandEntity, out var tool))
            {
                //apply tool to slot (run relevant function) //it is possible for a tool to do two or more thing at once (e.g. an energy sword should be able to saw and cauterise at the same time)
                //some combinations could be interesting - a bear trap could be a saw clamp for example
                //just make sure not to have something silly like an Incisor-Suture...

                var timeOverride = false; //if a tool has multiple functions, the timing should reflect that - use the time of whatever function completes first and override for remainder

                //checks are ordered so attachment occurs first, followed by opening, and then closing

                //retractor - used to open an organ container part after it has been incised (used as an attachment to the part)
                if (tool.Retractor)
                {
                    //timeOverride = attachRetractor();
                    Logger.Debug("attachRetractor");
                }

                //large clamp - intended to prevent bleeding on part removal (used as an attachment to the slot)
                if (tool.LargeClamp)
                {
                    //timeOverride = attachLargeClamp(); //does not work on slots with IsRoot
                    Logger.Debug("attachLargeClamp");
                }

                //incisor - incisors part if incisable (or removes organ if used with a manipulator)
                if (tool.Incisor)
                {
                    //timeOverride = incisePart();
                    Logger.Debug("incisePart");
                }

                //saw - removes body parts and opens skeletons (exo or endo) - if the part is incised DON'T remove it
                if (tool.Saw)
                {
                    //timeOverride = sawPart(); //will either remove a part or open a skeleton depending on status of selected part - will not detach root parts (but will open their skeletons)
                    Logger.Debug("sawPart");
                }

                //drill - used to reattach part to body
                if (tool.Drill)
                {
                    foreach (var hand in userHands.Hands.Values)
                    {
                        if (hand.HeldEntity != null && TryComp<BodyPartComponent>(hand.HeldEntity, out var part))
                        {
                            //timeOverride = attachPart();
                            Logger.Debug("attachPart");
                            break;
                        }
                    }
                }

                //suture - used to close incisions and re-attach organs
                if (tool.Suture)
                {
                    //timeOverride = closeIncision();
                    Logger.Debug("closeIncision");
                }

                //hard suture - used to close skeletons (exo or endo)
                if (tool.HardSuture)
                {
                    //timeOverride = closeHardIncision() //can call closeIncision(); depending on the status of the selected part
                    Logger.Debug("closeHardIncision");
                }

                //cauterizer - used to stop bleeding
                if (tool.Cauterizer)
                {
                    //timeOverride = cauterizePartSlot(); //will only work if said slot is empty
                    Logger.Debug("cauterizePartSlot");
                }


            }
            else if (userHands.ActiveHandEntity == null && args.Slot != null)
            {
                //if hand empty check for attached tool on either the part or slot - remove tool if present (prioritise part over slot)
                var checkAttachment = false;
                if (args.Slot.Child != null && TryComp<BodyPartComponent>(args.Slot.Child, out var part) && part.Attachment != null)
                {
                    //remove part attachment
                    //removePartAttachment();
                    Logger.Debug("removePartAttachment");
                } else if (args.Slot.Attachment != null)
                {
                    //remove part slot attachment
                    //removePartSlotAttachment();
                    Logger.Debug("removePartSlotAttachment");
                }

            }
            else if (userHands.ActiveHandEntity != null && TryComp<BodyPartComponent>(userHands.ActiveHandEntity, out var part))
            {
                //if there is a part or organ in hand, check if it can be placed in slot and check if there is a tool in the other hand to attach it
                foreach (var hand in userHands.Hands.Values)
                {
                    if (hand.HeldEntity != null && TryComp<SurgeryToolComponent>(hand.HeldEntity, out var offTool) && offTool.Drill)
                    {
                        //attachPart();
                        Logger.Debug("attachPart");
                        break;
                    }
                }
            }

        }

        private void OnOrganButtonPressed(EntityUid uid, SurgeryComponent component, OrganSlotButtonPressed args)
        {
            if (args.Session.AttachedEntity is not { Valid: true } user ||
                !TryComp<HandsComponent>(user, out var userHands))
                return;

            Logger.Debug(user.ToString());

            //check for surgical tool in active hand   
            if (userHands.ActiveHandEntity != null && TryComp<SurgeryToolComponent>(userHands.ActiveHandEntity, out var tool))
            {
                //apply tool to slot (run relevant function) //it is possible for a tool to do two or more thing at once (e.g. an energy sword should be able to saw and cauterise at the same time)

                //for organs checks are ordered so manipulation and removal occurs first, then attachment, and then closing
                //the hemostat will therefore clamp the organ slot that had just had its organ removed, so that's handy

                var timeOverride = false; //if a tool has multiple functions, the timing should reflect that - use the time of whatever function completes first and override for remainder

                //incisor - incisors part if incisable (or removes organ if used with a manipulator)
                //manipulator - used to remove organs (must be used with incisor in other hand) (will obviously not go in hand)
                if (tool.Incisor || tool.Manipulator)
                {
                    if (tool.Manipulator && tool.Incisor) {
                        //timeOverride = removeOrgan();
                    }
                    foreach (var hand in userHands.Hands.Values)
                    {
                        if (hand.HeldEntity != null && TryComp<SurgeryToolComponent>(hand.HeldEntity, out var secondTool)
                            && ((secondTool.Manipulator && tool.Incisor) || (tool.Manipulator && secondTool.Incisor)))
                        {
                            //timeOverride = removeOrgan();
                            break;
                        }
                    }
                }

                //small clamp - intended to prevent bleeding upon organ removal (used as an attachment to the slot)
                if (tool.SmallClamp)
                {
                    //timeOverride = attachSmallClamp();
                }

                //suture - used to close incisions and re-attach organs
                if (tool.Suture || tool.HardSuture)
                {
                    foreach (var hand in userHands.Hands.Values)
                    {
                        if (hand.HeldEntity != null && TryComp<OrganComponent>(hand.HeldEntity, out var organ))
                        {
                            //timeOverride = attachOrgan();
                            break;
                        }
                    }
                }

                //cauterizer - used to stop bleeding
                if (tool.Cauterizer)
                {
                    //timeOverride = cauterizeOrganSlot();
                }


            }
            else if (userHands.ActiveHandEntity == null)
            {
                //if hand empty check for attached tool on the slot - remove tool if present
                var checkAttachment = false;
                if (args.Slot.Attachment != null && args.Slot != null)
                {
                    //remove organ slot attachment
                    //removeOrganSlotAttachment();
                }

            }
            else if (userHands.ActiveHandEntity != null && TryComp<OrganComponent>(userHands.ActiveHandEntity, out var part))
            {
                //if there is a part or organ in hand, check if it can be placed in slot and check if there a tool in the other hand to attach it
                foreach (var hand in userHands.Hands.Values)
                {
                    if (hand.HeldEntity != null && TryComp<SurgeryToolComponent>(hand.HeldEntity, out var offTool) && (offTool.Suture || offTool.HardSuture))
                    {
                        //attachOrgan();
                        break;
                    }
                }
            }

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
                UpdateUiState(component.Owner);
            }
        }

        /// <summary>
        /// Adds additional surgery buttons for when an organ container is opened
        /// </summary>
        public void StartOpeningOrganContainer(EntityUid user, SurgeryComponent component, SurgerySlotButtonPressed args)
        {

            //get all organs in body part

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
        /*private async void PlaceActiveHandItemInBodyPartSlot(EntityUid user, BodyPartSlot slot, SurgeryComponent component)
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

        }*/

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
