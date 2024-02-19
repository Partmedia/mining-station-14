using System.Threading.Tasks;
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
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Server.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Organ;
using Robust.Shared.Containers;
using Content.Shared.Buckle.Components;
using Content.Shared.Standing;
using Content.Shared.Damage;
using Content.Shared.Bed.Sleep;
using Content.Server.Bed.Sleep;
using Content.Server.Speech.Components;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

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
        [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
        [Dependency] private readonly SleepingSystem _sleepingSystem = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SurgeryComponent, ComponentInit>(OnSurgeryInit);
            SubscribeLocalEvent<SurgeryComponent, GetVerbsEvent<Verb>>(AddSurgeryVerb);

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

        public override void Update(float frameTime)
        {
            base.Update(frameTime);


            foreach (var surgery in EntityManager.EntityQuery<SurgeryComponent>(false))
            {
                //Opened Check
                if (surgery.Opened)
                {
                    surgery.OpenedLastChecked += frameTime;
                    if (surgery.OpenedLastChecked >= surgery.OpenedCheckInterval)
                    {
                        CheckOpenedBuckled(surgery.Owner, surgery);
                        surgery.OpenedLastChecked = 0f;
                    }
                }
                //Bleed Check
                if (surgery.OrganBleeding || surgery.PartBleeding)
                {
                    surgery.BleedLastChecked += frameTime;
                    if (surgery.BleedLastChecked >= surgery.BleedCheckInterval)
                    {
                        CalculateBleed(surgery.Owner, surgery);
                    }
                }

                //Clamped Check
                if (surgery.Clamped)
                {
                    surgery.ClampLastChecked += frameTime;
                    if (surgery.ClampLastChecked >= surgery.ClampCheckInterval)
                    {
                        var clampedEntities = surgery.ClampedTimes.Keys.ToArray();
                        foreach (var entity in clampedEntities)
                        {
                            if (surgery.ClampedTimes.ContainsKey(entity))
                            {
                                surgery.ClampedTimes[entity] += surgery.ClampLastChecked;
                            }
                        }

                        var clampedTimes = surgery.ClampedTimes.Values.ToArray();

                        CheckClampNecrosis(surgery.Owner, surgery, clampedTimes);

                        surgery.ClampLastChecked = 0;
                    }
                }

                //Cellular Rejection Check
                if (surgery.RejectionLastChecked >= surgery.RejectionCheckInterval)
                {
                    CheckCellularRejection(surgery.Owner, surgery);
                    surgery.RejectionLastChecked = 0;
                } else {
                    surgery.RejectionLastChecked += frameTime;
                }

                //check if entity has the forced sleep status effect, if they do set Sedated = true, else false
                //TODO forced sleep =/= sedation, will include a dedicated status effect at some point for non-sleeping sedatives (also narcolepsy should not be as useful as anaesthetic)
                if (TryComp<ForcedSleepingComponent>(surgery.Owner, out var sleep))
                    surgery.Sedated = true;
                else
                    surgery.Sedated = false;
            }   
        }

        public void CheckCellularRejection(EntityUid uid, SurgeryComponent surgery)
        {
            //get all slots (part and organ)
            var bodyPartSlots = GetAllBodyPartSlots(uid);
            var organSlots = GetOpenPartOrganSlots(bodyPartSlots);

            //check if slot species matches with part/organ species
            List<BodyPartComponent> misMatchedParts = new List<BodyPartComponent>();
            List<OrganComponent> misMatchedOrgans = new List<OrganComponent>();

            foreach (var slot in bodyPartSlots)
            {
                if (slot.Child is not null && TryComp<BodyPartComponent>(slot.Child, out var part) && slot.Species != part.Species)
                {
                    misMatchedParts.Add(part);
                }
            }
            foreach (var slot in organSlots)
            {
                if (slot.Child is not null && TryComp<OrganComponent>(slot.Child, out var organ) && slot.Species != organ.Species)
                {
                    misMatchedOrgans.Add(organ);
                }
            }

            //check if mismatches in surgery compatible species
            //if rejected, deal damage and increment rejection counter IF counter < rounds
            foreach (var part in misMatchedParts)
            {
                if (!surgery.CompatibleSpecies.Contains(part.Species) && part.RejectionCounter < part.RejectionRounds)
                {
                    _damageableSystem.TryChangeDamage(uid, surgery.CellularRejectionDamage, ignoreResistances: true);
                    _popupSystem.PopupEntity(Loc.GetString("surgery-warn-species-incompatible"), uid, uid);
                    _bodySystem.IncrementRejectionCounter(part);
                }
            }

            foreach (var organ in misMatchedOrgans)
            {
                if (!surgery.CompatibleSpecies.Contains(organ.Species) && organ.RejectionCounter < organ.RejectionRounds)
                {
                    _damageableSystem.TryChangeDamage(uid, surgery.CellularRejectionDamage, ignoreResistances: true);
                    _popupSystem.PopupEntity(Loc.GetString("surgery-warn-organ-incompatible"), uid, uid);
                    _bodySystem.IncrementRejectionCounter(organ);
                }
            }

        }

        public void CheckClampNecrosis(EntityUid uid, SurgeryComponent surgery, float[] clampTimes)
        {
            var necrosis = false;
            foreach (var time in clampTimes)
            {
                if (time > surgery.BaseNecrosisTimeThreshold)
                {
                    necrosis = true;
                    break;
                }

            }
            if (necrosis)
            {
                _damageableSystem.TryChangeDamage(uid, surgery.NecrosisDamage, ignoreResistances: true);
                if (!surgery.Necrosis)
                {
                    surgery.Necrosis = true;
                    _popupSystem.PopupEntity(Loc.GetString("surgery-warn-necrosis"), uid, uid);
                }
            }
        }

        public void CheckOpenedBuckled(EntityUid uid, SurgeryComponent surgery)
        {
            //is the body "opened"?
            if (!surgery.Opened)
                return;

            //is the entity buckled?
            if (!TryComp<BuckleComponent>(uid, out var buckle) || buckle.Buckled)
                return;

            //is the entity standing?
            if (!TryComp<StandingStateComponent>(uid, out var standing) || !standing.Standing)
                return;

            _damageableSystem.TryChangeDamage(uid, surgery.OpenedDamage, ignoreResistances: true);
        }

        /// <summary>
        /// Handle Bleeding to occur (eventually replace this when either Woundmed or something like it is implemented)
        /// Routinely run this function via update
        /// Keep track of surgery incurred bleeding via surgery component
        /// Ensure bleed is equal to b + s where b is the entity bleed outside of surgery and s is surgery bleed (part or organ)
        /// If the surgery bleeding is stopped and all surgical wounds sealed, reduce bleed by s
        /// </summary>
        private void CalculateBleed(EntityUid uid, SurgeryComponent body)
        {

            //Get entity bleed damage bloodstream component
            if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
                return;

            //Get last SurgeryBleed value from surgery component
            var currentBleed = body.SurgeryBleed;

            //Negate SurgeryBleed from EntityBleed to determine Bleed independent of surgery (minium 0)
            var nonSurgeryBleed = Math.Max(bloodstream.BleedAmount - body.SurgeryBleed, 0f);

            //Add that value with organ bleed or part bleed, which ever is greater (and applicable)
            var newBleed = nonSurgeryBleed;
            if (body.PartBleeding)
                newBleed += body.BasePartBleed;
            else if (body.OrganBleeding)
                newBleed += body.BaseOrganBleed;


            //Modify Bleed for bloodstream component to be equal to that value
            if (_bloodstreamSystem.TryModifyBleedAmount(uid, newBleed - bloodstream.BleedAmount, bloodstream))
                body.SurgeryBleed = newBleed;

            //The result should ensure that entity bleeding is allowed to occur normally while surgery bleed is continuously maintained
            //Entity bleed heals over time on its own or via other methods, but surgery bleed can only be stopped by direct surgical interaction - effectively being constant
            //But once addressed, surgery bleed is taken away immediately
        }

        private void CheckPainShockDamage(EntityUid uid, ToolUsage toolUsage, SurgeryComponent body)
        {
            //check if entity is sedacted
            if (body.Sedated)
                return;

            //check if body has a brain (no brain, no pain)
            var organs = GetAllBodyOrgans(uid);
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
                return;

            //if not, deal damage based on tool usage
            if (body.UsageShock.ContainsKey(toolUsage) && body.UsageShock[toolUsage].Total > 0)
                _damageableSystem.TryChangeDamage(uid, body.UsageShock[toolUsage], ignoreResistances: true);
            else
                return;

            //force the entity awake
            if (TryComp<SleepingComponent>(uid, out var sleeping))
                _sleepingSystem.TryWaking(uid, sleeping);

            //make them scream
            if (TryComp<VocalComponent>(uid, out var vocal))
                RaiseLocalEvent(uid, new ScreamActionEvent());

        }

        public void SetBleedStatus(EntityUid uid, SurgeryComponent surgery, List<BodyPartSlot> bodyPartSlots, List<OrganSlot> organSlots)
        {
            if (TryComp<BloodstreamComponent>(uid, out var bloodstream))
            {
                //check clamp/cauterised/occupied status of all slots - if any are empty and not cauterised/clamped set either part or organ bleeding to true
                var partNotClamped = false;
                var currentPartBleed = surgery.PartBleeding;
                var currentOrganBleed = surgery.OrganBleeding;
                foreach (var slot in bodyPartSlots)
                {
                    if (slot.Child is null && !slot.Cauterised && (slot.Attachment is null || !TryComp<SurgeryToolComponent>(slot.Attachment, out var tool) || !tool.LargeClamp))
                    {
                        partNotClamped = true;
                        break;
                    }
                }

                surgery.PartBleeding = partNotClamped;
                //if the patient has gone from not bleeding to bleeding, run an initial bloodloss
                if (!currentPartBleed && surgery.PartBleeding)
                {
                    //if the part bleed is new but there was a bleeding organ, only make up the difference
                    if (!currentOrganBleed)
                        _bloodstreamSystem.TryModifyBloodLevel(uid, -surgery.InitialPartBloodloss, bloodstream);
                    else
                        _bloodstreamSystem.TryModifyBloodLevel(uid, -(Math.Abs(surgery.InitialPartBloodloss - surgery.InitialOrganBloodloss)), bloodstream);
                }

                var organNotClamped = false;

                foreach (var slot in organSlots)
                {
                    if (slot.Child is null && !slot.Cauterised && (slot.Attachment is null || !TryComp<SurgeryToolComponent>(slot.Attachment, out var tool) || !tool.SmallClamp))
                    {
                        organNotClamped = true;
                        break;
                    }
                }
                surgery.OrganBleeding = organNotClamped;
                //if the patient has gone from not bleeding to bleeding, run an initial bloodloss
                if (!currentPartBleed && !currentOrganBleed && surgery.OrganBleeding)
                    _bloodstreamSystem.TryModifyBloodLevel(uid, -surgery.InitialOrganBloodloss, bloodstream);
            }
        }

        /// <summary>
        /// Handles status effects such as bleeding and opened for when a part/organ/slot is affected
        /// Applies "shock" (airloss) damage is the body is not sedated
        /// </summary>
        private void SetBodyStatusFromChange(EntityUid uid, ToolUsage toolUsage)
        {

            //get surgery component
            if (!TryComp<SurgeryComponent>(uid, out var surgery))
                return;

            //if not sedated, apply airloss based on tool usage (use PainShockDamage())
            CheckPainShockDamage(uid, toolUsage, surgery);

            //get all entity body part slots and organ slots (and constituent parts and organs)
            var bodyPartSlots = GetAllBodyPartSlots(uid);
            var organSlots = GetOpenPartOrganSlots(bodyPartSlots);

            //check if any are opened - set surgery Opened to true if any are
            var open = false;
            foreach (var slot in bodyPartSlots)
            {
                if (slot.Child is not null && TryComp<BodyPartComponent>(slot.Child, out var bodyPart))
                {
                    if ((bodyPart.Incisable && bodyPart.Incised) || (bodyPart.EndoSkeleton && bodyPart.EndoOpened) || (bodyPart.ExoSkeleton && bodyPart.ExoOpened) || bodyPart.Opened)
                    {
                        surgery.OpenedLastChecked = 0f;
                        open = true;
                        break;
                    } 
                }
            }
            surgery.Opened = open;

            //check if any are clamped - set surgery Clamped to true if any are and update ClampedTime dict to include Clamped parts/organs
            //remove any that are no longer clamped if they are in the clamped dict
            List<EntityUid> newClampedEntities = new List<EntityUid>();
            foreach (var slot in bodyPartSlots)
            {
                if (slot.Child is not null && (slot.Attachment is not null && TryComp<SurgeryToolComponent>(slot.Attachment, out var tool) && tool.LargeClamp))
                {
                    newClampedEntities.Add(slot.Child.Value);
                }
            }
            foreach (var slot in organSlots)
            {
                if (slot.Child is not null && (slot.Attachment is not null && TryComp<SurgeryToolComponent>(slot.Attachment, out var tool) && tool.SmallClamp))
                {
                    newClampedEntities.Add(slot.Child.Value);
                }
            }

            if (newClampedEntities.Count() > 0)
                surgery.Clamped = true;
            else
            {
                surgery.Clamped = false;
                surgery.Necrosis = false;
            }

            if (surgery.Clamped)
            {
                //add new clamped entities
                foreach (var entity in newClampedEntities)
                {
                    if (!surgery.ClampedTimes.ContainsKey(entity))
                        surgery.ClampedTimes[entity] = 0f;
                }
                //remove no longer clamped entities
                foreach (var entity in surgery.ClampedTimes.Keys.ToArray())
                    if (!newClampedEntities.Contains(entity))
                        surgery.ClampedTimes.Remove(entity);
            }

            //SetBleedStatus(uid, surgery, bodyPartSlots, organSlots);
        }

        /// <summary>
        /// Sets the status of the part, whether it is open or not
        /// TODO later this may include other status effects or part-directed damage
        /// </summary>
        private void SetPartStatus(BodyPartComponent part)
        {
            if (!part.Container)
                return;

            if ((part.ExoSkeleton && !part.ExoOpened) || (part.Incisable && !part.Incised) || (part.EndoSkeleton && !part.EndoOpened))
            {
                _bodySystem.SetBodyPartOpen(part, false);
                return;
            }

            if ((part.Attachment != null && TryComp<SurgeryToolComponent>(part.Attachment, out var tool) && tool.Retractor) || !part.Incisable)
            {
                _bodySystem.SetBodyPartOpen(part, true);
                return;
            }
        }

        /// <summary>
        /// Get body part slots for a body part (usually starting with then torso, then followed by limbs)
        /// </summary>
        private List<BodyPartSlot> GetBodyPartSlots(EntityUid? bodyPart)
        {

            List<BodyPartSlot> bodyPartSlots = new List<BodyPartSlot>();

            if (TryComp<BodyPartComponent>(bodyPart, out var bodyPartComp))
            {
                if (bodyPartComp.Children != null)
                {
                    foreach (KeyValuePair<string, BodyPartSlot> partSlot in bodyPartComp.Children)
                    {
                        if (partSlot.Value.Wearable is null || !partSlot.Value.Wearable.Value) //don't track if the part is just a wearable
                            bodyPartSlots.Add(partSlot.Value);
                    }
                }
            }

            return bodyPartSlots;
        }

        /// <summary>
        /// Get all body part slots attached to everybody part attached to the initally submitted part (usually the torso to start)
        /// </summary>
        public List<BodyPartSlot> GetAllBodyPartSlots(EntityUid bodyOwner)
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

            }
            else if (TryComp<BodyPartComponent>(bodyOwner, out var bodyPart))
            {
                rootPart = bodyOwner;
            }
            else
                return new List<BodyPartSlot>();

            //proceed to get all part slots
            var initialPartList = GetBodyPartSlots(rootPart);
            List<BodyPartSlot> additionalPartList = new List<BodyPartSlot>();
            //then check all parts from that
            for (var i = 0; i < initialPartList.Count; i++)
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
                var tempSelfSlot = new BodyPartSlot("self", rootPart.Value, bodyPartComp.PartType, bodyPartComp.Species);
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

            for (var i = 0; i < bodyPartSlots.Count(); i++)
            {
                if (bodyPartSlots[i].Child is not null && TryComp<BodyPartComponent>(bodyPartSlots[i].Child, out var bodyPart))
                    foreach (KeyValuePair<string, OrganSlot> entry in bodyPart.Organs)
                    {
                        if (bodyPart.Opened || !entry.Value.Internal)
                            organSlots.Add(entry.Value);
                    }
            }
            return organSlots;
        }

        public List<OrganSlot> GetAllPartOrganSlots(List<BodyPartSlot> bodyPartSlots)
        {
            List<OrganSlot> organSlots = new List<OrganSlot>();

            for (var i = 0; i < bodyPartSlots.Count(); i++)
            {
                if (bodyPartSlots[i].Child is not null && TryComp<BodyPartComponent>(bodyPartSlots[i].Child, out var bodyPart))
                    foreach (KeyValuePair<string, OrganSlot> entry in bodyPart.Organs)
                    {
                        organSlots.Add(entry.Value);
                    }
            }
            return organSlots;
        }

        /// <summary>
        /// Get all organs in a body, useful for checking if a body has a certain organ or for any kind of body scanners
        /// </summary>
        public List<EntityUid> GetAllBodyOrgans(EntityUid uid)
        {
            var bodyPartSlots = GetAllBodyPartSlots(uid);
            var organSlots = GetAllPartOrganSlots(bodyPartSlots);

            List<EntityUid> organs = new List<EntityUid>();

            foreach (var slot in organSlots)
            {
                if (slot.Child is not null)
                    organs.Add(slot.Child.Value);
            }

            return organs;
        }

        private void UpdateUiState(EntityUid uid)
        {
            var bodyPartSlots = GetAllBodyPartSlots(uid);
            var organSlots = GetOpenPartOrganSlots(bodyPartSlots);

            Dictionary<EntityUid, SharedPartStatus> slotParts = new Dictionary<EntityUid, SharedPartStatus>();
            foreach (var slot in bodyPartSlots)
                if (slot.Child is not null)
                    if (TryComp<BodyPartComponent>(slot.Child, out var bodyPart))
                    {
                        var retracted = (bodyPart.Attachment != null && TryComp<SurgeryToolComponent>(bodyPart.Attachment, out var tool) && tool.Retractor);
                        slotParts.Add(slot.Child.Value, new SharedPartStatus(bodyPart.PartType, retracted, bodyPart.Incised, bodyPart.Opened, bodyPart.EndoOpened, bodyPart.ExoOpened));
                    }

            var state = new SurgeryBoundUserInterfaceState(bodyPartSlots, organSlots, slotParts);
            _userInterfaceSystem.TrySetUiState(uid, SurgeryUiKey.Key, state);
        }

        private void OnBodyPartAdded(EntityUid uid, SurgeryComponent component, ref BodyPartAddedEvent args)
        {
            //SetBodyStatusFromChange();
            //SetBodyStatusFromChange();
            var bodyPartSlots = GetAllBodyPartSlots(uid);
            var organSlots = GetOpenPartOrganSlots(bodyPartSlots);
            SetBleedStatus(uid, component, bodyPartSlots, organSlots);
            UpdateUiState(uid);
        }

        private void OnBodyPartRemoved(EntityUid uid, SurgeryComponent component, ref BodyPartRemovedEvent args)
        {
            //SetBodyStatusFromChange(uid, );
            //SetBodyStatusFromChange();
            var bodyPartSlots = GetAllBodyPartSlots(uid);
            var organSlots = GetOpenPartOrganSlots(bodyPartSlots);
            SetBleedStatus(uid, component, bodyPartSlots, organSlots);
            UpdateUiState(uid);
        }

        private async void RemoveToolFromPart(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartComponent bodyPart, HandsComponent userHands)
        {
            if (bodyPart.Attachment == null)
                return;

            if (!(await ProcedureDoAfter(user, target, tool.RetractorTime * tool.RetractorTimeMod, tool))) return;

            var attachmentContainer = _containerSystem.EnsureContainer<Container>(bodyPart.Owner, "attachment");
            attachmentContainer.Insert(tool.Owner);

            bodyPart.Attachment = null;

            _handsSystem.PickupOrDrop(user, tool.Owner);

            _bodySystem.RemovePartAttachment(bodyPart);

            SetBodyStatusFromChange(target, ToolUsage.Retractor);

            UpdateUiState(target);
        }

        private async void RemoveToolFromPartSlot(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartSlot bodyPartSlot, HandsComponent userHands)
        {
            if (bodyPartSlot.Attachment == null)
                return;

            if (!(await ProcedureDoAfter(user, target, tool.LargeClampTime * tool.LargeClampTimeMod, tool))) return;

            var attachmentContainer = _containerSystem.EnsureContainer<Container>(bodyPartSlot.Parent, "slotAttachment");
            attachmentContainer.Insert(tool.Owner);

            _bodySystem.RemovePartSlotAttachment(bodyPartSlot);

            _handsSystem.PickupOrDrop(user, tool.Owner);

            SetBodyStatusFromChange(target, ToolUsage.LargeClamp);

            UpdateUiState(target);
        }

        private async void RemoveToolFromOrganSlot(EntityUid user, SurgeryToolComponent tool, EntityUid target, OrganSlot organSlot, HandsComponent userHands)
        {
            if (organSlot.Attachment == null)
                return;

            if (!(await ProcedureDoAfter(user, target, tool.SmallClampTime * tool.SmallClampTimeMod, tool))) return;

            var attachmentContainer = _containerSystem.EnsureContainer<Container>(organSlot.Parent, "slotAttachment");
            attachmentContainer.Insert(tool.Owner);

            _bodySystem.RemoveOrganSlotAttachment(organSlot);

            _handsSystem.PickupOrDrop(user, tool.Owner);

            SetBodyStatusFromChange(target, ToolUsage.SmallClamp);

            UpdateUiState(target);
        }

        private void AttachToolToPart(EntityUid user, SurgeryToolComponent tool, BodyPartComponent bodyPart, HandsComponent userHands)
        {
            if (userHands.ActiveHand?.HeldEntity is { } held
                 && _handsSystem.TryDrop(user, userHands.ActiveHand, handsComp: userHands))
            {
                var attachmentContainer = _containerSystem.EnsureContainer<Container>(bodyPart.Owner, "attachment");
                attachmentContainer.Insert(tool.Owner);
                _bodySystem.AttachPartAttachment(tool.Owner, bodyPart);
            }
        }

        private void AttachToolToPartSlot(EntityUid user, SurgeryToolComponent tool, BodyPartSlot bodyPartSlot, HandsComponent userHands)
        {
            if (userHands.ActiveHand?.HeldEntity is { } held
                 && _handsSystem.TryDrop(user, userHands.ActiveHand, handsComp: userHands))
            {
                var attachmentContainer = _containerSystem.EnsureContainer<Container>(bodyPartSlot.Parent, "slotAttachment");
                attachmentContainer.Insert(tool.Owner);
                _bodySystem.AttachPartSlotAttachment(tool.Owner, bodyPartSlot);
            }
        }

        private void AttachToolToOrganSlot(EntityUid user, SurgeryToolComponent tool, OrganSlot organSlot, HandsComponent userHands)
        {
            if (userHands.ActiveHand?.HeldEntity is { } held
                 && _handsSystem.TryDrop(user, userHands.ActiveHand, handsComp: userHands))
            {
                var attachmentContainer = _containerSystem.EnsureContainer<Container>(organSlot.Parent, "slotAttachment");
                attachmentContainer.Insert(tool.Owner);
                _bodySystem.AttachOrganSlotAttachment(tool.Owner, organSlot);
            }
        }

        private async Task<bool> ProcedureDoAfter(EntityUid user, EntityUid target, float time, SurgeryToolComponent tool)
        {

            if (tool.Applying)
                return false;

            tool.Applying = true;

            var doAfterArgs = new DoAfterEventArgs(user, time, CancellationToken.None, target)
            {
                BreakOnStun = true,
                BreakOnDamage = true,
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
            };

            var result = await _doAfterSystem.WaitDoAfter(doAfterArgs);

            tool.Applying = false;

            if (result != DoAfterStatus.Finished) return false;
            return true;
        }

        private async Task<bool> AttachRetractor(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartComponent bodyPart, HandsComponent userHands, bool timeOverride)
        {
            //check for incisable (technically everything is incisable, but we only care about organ containers for now)
            if (!bodyPart.Incisable)
                return false;

            //check for attachment entity
            if (bodyPart.Attachment != null)
            {
                _popupSystem.PopupEntity(Loc.GetString("surgery-part-already-has-attachment"), user, user);
                return false;
            }

            //check for incised
            if (!bodyPart.Incised)
            {
                _popupSystem.PopupEntity(Loc.GetString("surgery-part-not-incised-retractor"), user, user);
                return false;
            }

            //attach retractor
            if (!timeOverride)
                if (!(await ProcedureDoAfter(user, target, tool.RetractorTime * tool.RetractorTimeMod, tool))) return false;

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            AttachToolToPart(user, tool, bodyPart, userHands);

            _popupSystem.PopupEntity(Loc.GetString("surgery-retractor-applied"), user, user);

            SetBodyStatusFromChange(target, ToolUsage.Retractor);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> AttachLargeClamp(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartSlot bodyPartSlot, HandsComponent userHands, bool timeOverride)
        {

            //check for isRoot - large clamps should only be attached to the slots of parts that can be removed
            if (bodyPartSlot.IsRoot)
                return false;

            //check for attachment entity
            if (bodyPartSlot.Attachment != null)
            {
                _popupSystem.PopupEntity(Loc.GetString("surgery-part-already-has-attachment"), user, user);
                return false;
            }

            //attach clamp
            if (!timeOverride)
                if (!(await ProcedureDoAfter(user, target, tool.LargeClampTime * tool.LargeClampTimeMod, tool))) return false;

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            _popupSystem.PopupEntity(Loc.GetString("surgery-large-clamp-attached"), user, user);

            AttachToolToPartSlot(user, tool, bodyPartSlot, userHands);

            SetBodyStatusFromChange(target, ToolUsage.LargeClamp);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> AttachSmallClamp(EntityUid user, SurgeryToolComponent tool, EntityUid target, OrganSlot organSlot, HandsComponent userHands, bool timeOverride)
        {

            //check for attachment entity
            if (organSlot.Attachment != null)
            {
                _popupSystem.PopupEntity(Loc.GetString("surgery-organ-already-has-attachment"), user, user);
                return false;
            }

            //attach clamp
            if (!timeOverride)
                if (!(await ProcedureDoAfter(user, target, tool.SmallClampTime * tool.SmallClampTimeMod, tool))) return false;

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            _popupSystem.PopupEntity(Loc.GetString("surgery-small-clamp-attached"), user, user);

            AttachToolToOrganSlot(user, tool, organSlot, userHands);

            SetBodyStatusFromChange(target, ToolUsage.SmallClamp);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> IncisePart(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartComponent bodyPart, HandsComponent userHands, bool timeOverride)
        {

            //if it is not incisable, or has already been incised do not do it again
            if (!(bodyPart.Incisable) || bodyPart.Incised)
                return false;

            if (bodyPart.ExoSkeleton && !bodyPart.ExoOpened)
            {
                _popupSystem.PopupEntity(Loc.GetString("surgery-exo-skeleton-blocking"), user, user);
                return false;
            }

            if (!timeOverride)
                if (!(await ProcedureDoAfter(user, target, tool.IncisorTime * tool.IncisorTimeMod, tool))) return false;

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            _popupSystem.PopupEntity(Loc.GetString("surgery-incision-made"), user, user);

            _bodySystem.SetBodyPartIncised(bodyPart, true);

            SetBodyStatusFromChange(target, ToolUsage.Incisor);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> StitchPart(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartComponent bodyPart, HandsComponent userHands, bool hardStitch, bool timeOverride)
        {

            //do not allow the same stich procedure to occur twice (i.e. this may only work when called from HardStitchPart if it is a HardSuture)
            if (tool.HardSuture && !hardStitch)
                return true;

            //check if actually incised
            if (!bodyPart.Incised)
                return false;

            //don't close the incision until their bones are back together
            if (bodyPart.EndoSkeleton && bodyPart.EndoOpened)
            {
                _popupSystem.PopupEntity(Loc.GetString("surgery-endo-skeleton-opened"), user, user);
                return false;
            }

            //check for obstructing retractor
            if (bodyPart.Attachment is not null && TryComp<SurgeryToolComponent>(bodyPart.Attachment, out var attachedTool) && attachedTool.Retractor)
            {
                _popupSystem.PopupEntity(Loc.GetString("surgery-retractor-block-stitch"), user, user);
                return false;
            }

            if (!timeOverride)
            {
                if (hardStitch)
                {
                    if (!(await ProcedureDoAfter(user, target, tool.HardSutureTime * tool.HardSutureTimeMod, tool))) return false;
                }
                else
                {
                    if (!(await ProcedureDoAfter(user, target, tool.SutureTime * tool.SutureTimeMod, tool))) return false;
                }
            }

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            _popupSystem.PopupEntity(Loc.GetString("surgery-incision-closed"), user, user);

            _bodySystem.SetBodyPartIncised(bodyPart, false);

            if (hardStitch)
            {
                SetBodyStatusFromChange(target, ToolUsage.HardSuture);
            }
            else
            {
                SetBodyStatusFromChange(target, ToolUsage.Suture);
            }

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> HardStitchPart(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartComponent bodyPart, HandsComponent userHands, bool timeOverride)
        {
            bool endo;

            //first check for an endoskeleton
            if (bodyPart.EndoOpened)
                endo = true;
            //then an incision
            else if (bodyPart.Incised)
                return await StitchPart(user, tool, target, bodyPart, userHands, true, timeOverride);
            //then for an exoskeleton
            else if (bodyPart.ExoSkeleton)
                endo = false;
            else
                return false;        

            if (endo)
            {
                if (bodyPart.Incisable && (bodyPart.Attachment is null || !TryComp<SurgeryToolComponent>(bodyPart.Attachment, out var attachedTool) || !attachedTool.Retractor))
                {
                    _popupSystem.PopupEntity(Loc.GetString("surgery-no-retractor"), user, user);
                    return false;
                }
                if (!timeOverride)
                    if (!(await ProcedureDoAfter(user, target, tool.HardSutureTime * tool.HardSutureTimeMod, tool))) return false;
                _audio.PlayPvs(tool.ToolSound, tool.Owner);
                _bodySystem.SetBodyPartEndoOpen(bodyPart, false);
                _popupSystem.PopupEntity(Loc.GetString("surgery-endoskeleton-closed"), user, user);
            }
            else if (!endo)
            {
                if (!timeOverride)
                    if (!(await ProcedureDoAfter(user, target, tool.HardSutureTime * tool.HardSutureTimeMod, tool))) return false;
                _audio.PlayPvs(tool.ToolSound, tool.Owner);
                _bodySystem.SetBodyPartExoOpen(bodyPart, false);
                _popupSystem.PopupEntity(Loc.GetString("surgery-exoskeleton-closed"), user, user);
            }

            SetBodyStatusFromChange(target, ToolUsage.HardSuture);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> CauterisePartSlot(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartSlot bodyPartSlot, HandsComponent userHands, bool timeOverride)
        {
            //root slots cannot be cauterised
            //part slots can only be cauterised if the slot is empty
            if (bodyPartSlot.IsRoot || bodyPartSlot.Child is not null)
                return false;


            if (bodyPartSlot.Cauterised)
            {
                _popupSystem.PopupEntity(Loc.GetString("surgery-slot-already-cauterised"), user, user);
                return false;
            }

            if (!timeOverride)
                if (!(await ProcedureDoAfter(user, target, tool.CauterizerTime * tool.CauterizerTimeMod, tool))) return false;

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            _popupSystem.PopupEntity(Loc.GetString("surgery-wound-cauterised"), user, user);

            _bodySystem.SetCauterisedPartSlot(bodyPartSlot, true);

            SetBodyStatusFromChange(target, ToolUsage.Cauterizer);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> CauteriseOrganSlot(EntityUid user, SurgeryToolComponent tool, EntityUid target, OrganSlot organSlot, HandsComponent userHands, bool timeOverride)
        {
            //part slots can only be cauterised if the slot is empty
            if (organSlot.Child is not null)
                return false;

            if (organSlot.Cauterised)
            {
                _popupSystem.PopupEntity(Loc.GetString("surgery-slot-already-cauterised"), user, user);
                return false;
            }

            if (!timeOverride)
                if (!(await ProcedureDoAfter(user, target, tool.CauterizerTime * tool.CauterizerTimeMod, tool))) return false;

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            _popupSystem.PopupEntity(Loc.GetString("surgery-wound-cauterised"), user, user);

            _bodySystem.SetCauterisedOrganSlot(organSlot, true);

            SetBodyStatusFromChange(target, ToolUsage.Cauterizer);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> RemovePart(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartComponent bodyPart, HandsComponent userHands, bool timeOverride)
        {
            //check if part is actually attached to a slot
            if (bodyPart.ParentSlot is null || bodyPart.Owner == target)
                return false;

            if (!timeOverride)
                if (!(await ProcedureDoAfter(user, target, tool.SawTime * tool.SawTimeMod, tool))) return false;

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            if (!(_bodySystem.DropPart(bodyPart.Owner, bodyPart))) return false;

            _handsSystem.PickupOrDrop(user, bodyPart.Owner);

            _popupSystem.PopupEntity(Loc.GetString("surgery-body-part-removed"), user, user);

            SetBodyStatusFromChange(target, ToolUsage.Saw);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> RemoveOrgan(EntityUid user, SurgeryToolComponent tool, EntityUid target, OrganComponent organ, HandsComponent userHands, bool timeOverride)
        {
            //check if part is actually attached to a slot
            if (organ.ParentSlot is null)
                return false;

            if (!timeOverride)
                if (!(await ProcedureDoAfter(user, target, tool.ManipulatorTime * tool.ManipulatorTimeMod, tool))) return false;

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            if (!(_bodySystem.DropOrgan(organ.Owner, organ))) return false;

            _handsSystem.PickupOrDrop(user, organ.Owner);

            _popupSystem.PopupEntity(Loc.GetString("surgery-organ-removed"), user, user);

            SetBodyStatusFromChange(target, ToolUsage.Manipulator);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> AttachPart(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartSlot bodyPartSlot, BodyPartComponent bodyPart, HandsComponent userHands, bool timeOverride)
        {

            //check if part slot is empty
            if (bodyPartSlot.Child is not null)
                return false;

            //ensure part type matches the slot type
            if (bodyPart.PartType != bodyPartSlot.Type)
                return false;

            if (!timeOverride)
                if (!(await ProcedureDoAfter(user, target, tool.DrillTime * tool.DrillTimeMod, tool))) return false;

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            if (!(_bodySystem.AttachPart(bodyPart.Owner, bodyPartSlot, bodyPart))) return false;

            _popupSystem.PopupEntity(Loc.GetString("surgery-body-part-attached"), user, user);

            SetBodyStatusFromChange(target, ToolUsage.Drill);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> AttachOrgan(EntityUid user, SurgeryToolComponent tool, EntityUid target, OrganSlot organSlot, OrganComponent organ, HandsComponent userHands, bool timeOverride)
        {

            //check if part slot is empty
            if (organSlot.Child is not null)
                return false;

            //ensure part type matches the slot type
            if (organ.OrganType != organSlot.Type)
                return false;

            if (!timeOverride)
            {
                if (tool.Suture)
                {
                    if (!(await ProcedureDoAfter(user, target, tool.SutureTime * tool.SutureTimeMod, tool))) return false;
                }
                else if (tool.HardSuture)
                {
                    if (!(await ProcedureDoAfter(user, target, tool.HardSutureTime * tool.HardSutureTimeMod, tool))) return false;
                }
            }

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            if (!(_bodySystem.InsertOrgan(organ.Owner, organSlot, organ))) return false;

            _popupSystem.PopupEntity(Loc.GetString("surgery-organ-attached"), user, user);

            if (tool.Suture)
            {
                SetBodyStatusFromChange(target, ToolUsage.Suture);
            }
            else if (tool.HardSuture)
            {
                SetBodyStatusFromChange(target, ToolUsage.HardSuture);
            }

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> OpenEndo(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartComponent bodyPart, HandsComponent userHands, bool timeOverride)
        {
            if (bodyPart.EndoOpened)
                return false;

            if (!timeOverride)
                if (!(await ProcedureDoAfter(user, target, tool.SawTime * tool.SawTimeMod, tool))) return false;

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            _bodySystem.SetBodyPartEndoOpen(bodyPart, true);

            _popupSystem.PopupEntity(Loc.GetString("surgery-endoskeleton-opened"), user, user);

            SetBodyStatusFromChange(target, ToolUsage.Saw);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> OpenExo(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartComponent bodyPart, HandsComponent userHands, bool timeOverride)
        {
            if (bodyPart.ExoOpened)
                return false;

            if (!timeOverride)
                if (!(await ProcedureDoAfter(user, target, tool.SawTime * tool.SawTimeMod, tool))) return false;

            _audio.PlayPvs(tool.ToolSound, tool.Owner);

            _bodySystem.SetBodyPartExoOpen(bodyPart, true);

            _popupSystem.PopupEntity(Loc.GetString("surgery-exoskeleton-opened"), user, user);

            SetBodyStatusFromChange(target, ToolUsage.Saw);

            UpdateUiState(target);

            return true;
        }

        private async Task<bool> SawPart(EntityUid user, SurgeryToolComponent tool, EntityUid target, BodyPartComponent bodyPart, HandsComponent userHands, bool timeOverride)
        {

            //we always check for an exo first
            //then we check if an incision has been made (if inciseable) - if not we take the part away
            //otherwise we open the endo skeleton (if required retractions have been made)

            //if has a closed exoskeleton, open it up - exo should be opened before removing or opening parts
            if (bodyPart.ExoSkeleton && !bodyPart.ExoOpened)
            {
                return await OpenExo(user, tool, target, bodyPart, userHands, timeOverride);
            }
            //if its not incised, has no exo or an open exo, is not a root part, then remove the part from its slot
            else if (!bodyPart.Incised && (!bodyPart.ExoSkeleton || bodyPart.ExoOpened) && bodyPart.ParentSlot is not null && !bodyPart.ParentSlot.IsRoot)
            {
                return await RemovePart(user, tool, target, bodyPart, userHands, timeOverride);
            }
            //else if it's incised (or not inciseable), crack open that endoskeleton (if they have one)
            else if ((!bodyPart.Incisable || bodyPart.Incised) && bodyPart.EndoSkeleton && !bodyPart.EndoOpened)
            {
                if (bodyPart.Incisable && (bodyPart.Attachment is null || !TryComp<SurgeryToolComponent>(bodyPart.Attachment, out var attachedTool) || !attachedTool.Retractor))
                {
                    _popupSystem.PopupEntity(Loc.GetString("surgery-no-retractor"), user, user);
                    return false;
                }

                return await OpenEndo(user, tool, target, bodyPart, userHands, timeOverride);
            }
            else
            {
                return false;
            }
        }

        private bool CheckBlockingInventory(EntityUid uid, SurgeryComponent component)
        {
            if (TryComp(uid, out InventoryComponent? inv)
            && _prototypeManager.TryIndex<InventoryTemplatePrototype>(inv.TemplateId, out var prototype))
            {
                foreach (var slotDef in prototype.Slots)
                {
                    if (component.BlockingSlots.Contains(slotDef.Name))
                    {
                        if (_inventory.TryGetSlotEntity(uid, slotDef.Name, out var item))
                        {
                            if (!TryComp<SurgeryGownComponent>(item.Value, out var gown))
                                return true;
                        }                       
                    }
                }
            }
            return false;
        }

        private async void OnSurgeryButtonPressed(EntityUid uid, SurgeryComponent component, SurgerySlotButtonPressed args)
        {
            if (args.Session.AttachedEntity is not { Valid: true } user ||
                !TryComp<HandsComponent>(user, out var userHands))
                return;

            //TODO check if patient is standing

            if (CheckBlockingInventory(uid,component))
            {
                _popupSystem.PopupEntity(Loc.GetString("surgery-blocked"), user, user);
                return;
            }

            //check for surgical tool in active hand   
            if (userHands.ActiveHandEntity != null && TryComp<SurgeryToolComponent>(userHands.ActiveHandEntity, out var tool))
            {
                //apply tool to slot (run relevant function) //it is possible for a tool to do two or more things at once (e.g. an energy sword should be able to saw and cauterise at the same time)
                //some combinations could be interesting - a bear trap could be a clamp and saw for example
                //just make sure not to have something silly like an Incisor-Suture...

                //tools with have an order of operation when used, attachment functions are run first, then anything that opens or removes parts, then closes, then lastly cauterises
                //this is operating on the logic that attachments only work when attached, and that opening occurs before closing (and the cautery element occurs lastly after main application if any prior)
                //it's a little jank maybe, and it may be better to refactor this as an ordered list of some kind, but it'll do for now

                var timeOverride = false; //if a tool has multiple functions, the timing should reflect that - use the time of whatever function completes first and override for remainder

                //track number of utilities applied, if the time override is false but utilityCounter is greater than 0, cancel - if one function fails they all fail
                var utilityCounter = 0;

                //retractor - used to open an organ container part after it has been incised (used as an attachment to the part)
                if (tool.Retractor)
                {
                    if (args.Slot.Child != null && TryComp<BodyPartComponent>(args.Slot.Child, out var part))
                    {
                        timeOverride = await AttachRetractor(user, tool, uid, part, userHands, timeOverride);
                        utilityCounter++;
                    }
                }

                //large clamp - intended to prevent bleeding on part removal (used as an attachment to the slot)
                if (tool.LargeClamp && (timeOverride || (!timeOverride && utilityCounter == 0)))
                {
                    timeOverride = await AttachLargeClamp(user, tool, uid, args.Slot, userHands, timeOverride); //does not work on slots with IsRoot
                    utilityCounter++;
                }

                //incisor - incisors part if incisable (or removes organ if used with a manipulator)
                if (tool.Incisor && (timeOverride || (!timeOverride && utilityCounter == 0)))
                {
                    if (args.Slot.Child != null && TryComp<BodyPartComponent>(args.Slot.Child, out var part))
                    {
                        timeOverride = await IncisePart(user, tool, uid, part, userHands, timeOverride);
                        utilityCounter++;
                    }
                }

                //saw - removes body parts and opens skeletons (exo or endo) - if the part is incised DON'T remove it
                if (tool.Saw && (timeOverride || (!timeOverride && utilityCounter == 0)))
                {
                    if (args.Slot.Child != null && TryComp<BodyPartComponent>(args.Slot.Child, out var part))
                    {
                        timeOverride = await SawPart(user, tool, uid, part, userHands, timeOverride); //will either remove a part or open a skeleton depending on status of selected part - will not detach root parts (but will open their skeletons)
                        utilityCounter++;
                    }
                }

                //drill - used to reattach part to body
                if (tool.Drill && (timeOverride || (!timeOverride && utilityCounter == 0)))
                {
                    foreach (var hand in userHands.Hands.Values)
                    {
                        if (hand.HeldEntity != null && TryComp<BodyPartComponent>(hand.HeldEntity, out var heldPart))
                        {
                            timeOverride = await AttachPart(user, tool, uid, args.Slot, heldPart, userHands, timeOverride);
                            utilityCounter++;
                            break;
                        }
                    }
                }

                //hard suture - used to close skeletons (exo or endo)
                if (tool.HardSuture && (timeOverride || (!timeOverride && utilityCounter == 0)))
                {
                    if (args.Slot.Child != null && TryComp<BodyPartComponent>(args.Slot.Child, out var part))
                    {
                        timeOverride = await HardStitchPart(user, tool, uid, part, userHands, timeOverride); //can call closeIncision(); depending on the status of the selected part
                        utilityCounter++;
                    }
                }

                //suture - used to close incisions and re-attach organs
                if (tool.Suture && (timeOverride || (!timeOverride && utilityCounter == 0)))
                {
                    if (args.Slot.Child != null && TryComp<BodyPartComponent>(args.Slot.Child, out var part))
                    {
                        timeOverride = await StitchPart(user, tool, uid, part, userHands, false, timeOverride);
                        utilityCounter++;
                    }
                }

                //cauterizer - used to stop bleeding
                if (tool.Cauterizer && (timeOverride || (!timeOverride && utilityCounter == 0)))
                {
                    timeOverride = await CauterisePartSlot(user, tool, uid, args.Slot, userHands, timeOverride); //will only work if said slot is empty
                }

                if (args.Slot.Child != null && TryComp<BodyPartComponent>(args.Slot.Child, out var operatedPart))
                    SetPartStatus(operatedPart);

            }
            else if (userHands.ActiveHandEntity == null && args.Slot != null)
            {
                //if hand empty check for attached tool on either the part or slot - remove tool if present (prioritise part over slot)
                if (args.Slot.Child != null && TryComp<BodyPartComponent>(args.Slot.Child, out var part) && part.Attachment != null)
                {
                    //remove part attachment
                    if (TryComp<SurgeryToolComponent>(part.Attachment, out var attachedTool))
                        RemoveToolFromPart(user, attachedTool, component.Owner, part, userHands);
                }
                else if (args.Slot.Attachment != null)
                {
                    //remove part slot attachment
                    if (TryComp<SurgeryToolComponent>(args.Slot.Attachment, out var attachedTool))
                        RemoveToolFromPartSlot(user, attachedTool, component.Owner, args.Slot, userHands);
                }

                if (args.Slot.Child != null && TryComp<BodyPartComponent>(args.Slot.Child, out var operatedPart))
                    SetPartStatus(operatedPart);

            }
            else if (userHands.ActiveHandEntity != null && TryComp<BodyPartComponent>(userHands.ActiveHandEntity, out var part))
            {
                //if there is a part or organ in hand, check if it can be placed in slot and check if there is a tool in the other hand to attach it
                foreach (var hand in userHands.Hands.Values)
                {
                    if (hand.HeldEntity != null && TryComp<SurgeryToolComponent>(hand.HeldEntity, out var offTool) && offTool.Drill && args.Slot != null)
                    {
                        var timeOverride = await AttachPart(user, offTool, uid, args.Slot, part, userHands, false);
                        break;
                    }
                }
            }
            UpdateUiState(component.Owner);
        }

        private async void OnOrganButtonPressed(EntityUid uid, SurgeryComponent component, OrganSlotButtonPressed args)
        {
            if (args.Session.AttachedEntity is not { Valid: true } user ||
                !TryComp<HandsComponent>(user, out var userHands))
                return;

            if (CheckBlockingInventory(uid, component))
            {
                _popupSystem.PopupEntity(Loc.GetString("surgery-blocked"), user, user);
                return;
            }

            //check for surgical tool in active hand   
            if (userHands.ActiveHandEntity != null && TryComp<SurgeryToolComponent>(userHands.ActiveHandEntity, out var tool))
            {
                //apply tool to slot (run relevant function) //it is possible for a tool to do two or more thing at once (e.g. an energy sword should be able to saw and cauterise at the same time)

                //for organs checks are ordered so manipulation and removal occurs first, then attachment, and then closing
                //the hemostat will therefore clamp the organ slot that had just had its organ removed, so that's handy

                var timeOverride = false; //if a tool has multiple functions, the timing should reflect that - use the time of whatever function completes first and override for remainder

                //track number of utilities applied, if the time override is false but utilityCounter is greater than 0, cancel
                //could also lets us cancel certain utilities that should not occur after others if necessary
                var utilityCounter = 0;

                //incisor - incisors part if incisable (or removes organ if used with a manipulator)
                //manipulator - used to remove organs (must be used with incisor in other hand) (will obviously not go in hand)
                if (tool.Incisor || tool.Manipulator)
                {
                    if (args.Slot.Child != null && TryComp<OrganComponent>(args.Slot.Child, out var organ))
                    {
                        if (tool.Manipulator && tool.Incisor)
                        {
                            timeOverride = await RemoveOrgan(user, tool, uid, organ, userHands, timeOverride);
                        }
                        foreach (var hand in userHands.Hands.Values)
                        {
                            if (hand.HeldEntity != null && TryComp<SurgeryToolComponent>(hand.HeldEntity, out var secondTool)
                                && ((secondTool.Manipulator && tool.Incisor) || (tool.Manipulator && secondTool.Incisor)))
                            {
                                if (tool.Manipulator)
                                    timeOverride = await RemoveOrgan(user, tool, uid, organ, userHands, timeOverride);
                                else if (secondTool.Manipulator)
                                    timeOverride = await RemoveOrgan(user, secondTool, uid, organ, userHands, timeOverride);
                                break;
                            }
                        }
                    }
                }

                //small clamp - intended to prevent bleeding upon organ removal (used as an attachment to the slot)
                if (tool.SmallClamp && (timeOverride || (!timeOverride && utilityCounter == 0)))
                {
                    timeOverride = await AttachSmallClamp(user, tool, uid, args.Slot, userHands, timeOverride);
                }

                //suture - used to close incisions and re-attach organs
                if (tool.Suture || tool.HardSuture && (timeOverride || (!timeOverride && utilityCounter == 0)))
                {
                    foreach (var hand in userHands.Hands.Values)
                    {
                        if (hand.HeldEntity != null && TryComp<OrganComponent>(hand.HeldEntity, out var organ))
                        {
                            timeOverride = await AttachOrgan(user, tool, uid, args.Slot, organ, userHands, timeOverride);
                            break;
                        }
                    }
                }

                //cauterizer - used to stop bleeding
                if (tool.Cauterizer && (timeOverride || (!timeOverride && utilityCounter == 0)))
                {
                    timeOverride = await CauteriseOrganSlot(user, tool, uid, args.Slot, userHands, timeOverride);
                }

            }
            else if (userHands.ActiveHandEntity == null)
            {
                //if hand empty check for attached tool on the slot - remove tool if present
                var checkAttachment = false;
                if (args.Slot.Attachment != null && args.Slot != null)
                {
                    //remove organ slot attachment
                    if (TryComp<SurgeryToolComponent>(args.Slot.Attachment, out var attachedTool))
                        RemoveToolFromOrganSlot(user, attachedTool, component.Owner, args.Slot, userHands);
                }

            }
            else if (userHands.ActiveHandEntity != null && TryComp<OrganComponent>(userHands.ActiveHandEntity, out var organ))
            {
                //if there is a part or organ in hand, check if it can be placed in slot and check if there is a tool in the other hand to attach it
                foreach (var hand in userHands.Hands.Values)
                {
                    if (hand.HeldEntity != null && TryComp<SurgeryToolComponent>(hand.HeldEntity, out var offTool) && (offTool.Suture || offTool.HardSuture) && args.Slot != null)
                    {
                        var timeOverride = await AttachOrgan(user, offTool, uid, args.Slot, organ, userHands, false);
                        break;
                    }
                }
            }
            UpdateUiState(component.Owner);

        }

        /// <summary>
        /// Opens main surgery interface
        /// </summary>
        public void StartOpeningSurgery(EntityUid user, SurgeryComponent component, bool openInCombat = false)
        {
            if (TryComp<CombatModeComponent>(user, out var mode) && mode.IsInCombatMode && !openInCombat)
                return;

            if (TryComp<ActorComponent>(user, out var actor))
            {
                if (_userInterfaceSystem.SessionHasOpenUi(component.Owner, SurgeryUiKey.Key, actor.PlayerSession))
                    return;
                _userInterfaceSystem.TryOpen(component.Owner, SurgeryUiKey.Key, actor.PlayerSession);
                UpdateUiState(component.Owner);
            }
        }

        private void AddSurgeryVerb(EntityUid uid, SurgeryComponent component, GetVerbsEvent<Verb> args)
        {
            if (args.Hands == null || !args.CanAccess || !args.CanInteract)
                return;

            if (!EntityManager.TryGetComponent(args.User, out ActorComponent? actor))
                return;

            Verb verb = new()
            {
                Text = Loc.GetString("surgery-perform"),
                Act = () => StartOpeningSurgery(args.User, component, true),
            };
            args.Verbs.Add(verb);
        }

        /*private void AddSurgeryExamineVerb(EntityUid uid, SurgeryComponent component, GetVerbsEvent<ExamineVerb> args)
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
        }*/

        private void OnSurgeryInit(EntityUid uid, SurgeryComponent body, ComponentInit args)
        {
            body.UsageShock[ToolUsage.Incisor] = body.IncisorShockDamage;
            body.UsageShock[ToolUsage.SmallClamp] = body.SmallClampShockDamage;
            body.UsageShock[ToolUsage.LargeClamp] = body.LargeClampShockDamage;
            body.UsageShock[ToolUsage.Saw] = body.SawShockDamage;
            body.UsageShock[ToolUsage.Drill] = body.DrillShockDamage;
            body.UsageShock[ToolUsage.Suture] = body.SutureShockDamage;
            body.UsageShock[ToolUsage.HardSuture] = body.HardSutureShockDamage;
            body.UsageShock[ToolUsage.Cauterizer] = body.CauterizerShockDamage;
            body.UsageShock[ToolUsage.Manipulator] = body.ManipulatorShockDamage;
            body.UsageShock[ToolUsage.Retractor] = body.RetractorShockDamage;
        }
    }
}
