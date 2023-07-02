using Robust.Shared.Serialization;
using Content.Shared.Body.Part;
using Content.Shared.Body.Organ;

namespace Content.Shared.Surgery
{
    /// <summary>
    /// This class holds constants that are shared between client and server.
    /// </summary>
    public sealed class SharedSurgery
    {
  
    }

    [Serializable, NetSerializable]
    public sealed class SharedPartStatus
    {

        public BodyPartType PartType;
        public bool Retracted;
        public bool Incised;
        public bool Opened;
        public bool EndoOpened;
        public bool ExoOpened;

        public SharedPartStatus(BodyPartType partType, bool retracted, bool incised, bool opened, bool endoOpened, bool exoOpened)
        {
            PartType = partType;
            Retracted = retracted;
            Incised = incised;
            Opened = opened;
            EndoOpened = endoOpened;
            ExoOpened = exoOpened;
        }
    }

    [Serializable, NetSerializable]
    public sealed class SurgeryBoundUserInterfaceState : BoundUserInterfaceState
    {
        public List<BodyPartSlot> BodyPartSlots;
        public List<OrganSlot> OrganSlots;
        public Dictionary<EntityUid, SharedPartStatus> SlotPartsStatus;

        public SurgeryBoundUserInterfaceState(List<BodyPartSlot> bodyPartSlots, List<OrganSlot> organSlots, Dictionary<EntityUid, SharedPartStatus> slotPartsStatus)
        {
            BodyPartSlots = bodyPartSlots;
            OrganSlots = organSlots;
            SlotPartsStatus = slotPartsStatus;
        }
    }

    [Serializable, NetSerializable]
    public enum SurgeryUiKey
    {
        Key
    }

    [NetSerializable, Serializable]
    public sealed class SurgerySlotButtonPressed : BoundUserInterfaceMessage
    {
        public readonly BodyPartSlot Slot;

        public SurgerySlotButtonPressed(BodyPartSlot slot)
        {
            Slot = slot;
        }
    }

    [NetSerializable, Serializable]
    public sealed class OrganSlotButtonPressed : BoundUserInterfaceMessage
    {
        public readonly OrganSlot Slot;

        public OrganSlotButtonPressed(OrganSlot slot)
        {
            Slot = slot;
        }
    }

    public abstract class BaseBeforeSurgeryEvent : EntityEventArgs
    {
        public readonly float InitialTime;
        public float Time => MathF.Max(InitialTime * Multiplier + Additive, 0f);
        public float Additive = 0;
        public float Multiplier = 1f;

        public BaseBeforeSurgeryEvent(float initialTime)
        {
            InitialTime = initialTime;
        }
    }

    /// <summary>
    /// Used to modify surgery times. Raised directed at the user.
    /// </summary>
    public sealed class BeforeSurgeryEvent : BaseBeforeSurgeryEvent
    {
        public BeforeSurgeryEvent(float initialTime) : base(initialTime) { }
    }

    /// <summary>
    /// Used to modify surgery times. Raised directed at the target.
    /// </summary>
    public sealed class BeforeGettingSurgeryEvent : BaseBeforeSurgeryEvent
    {
        public BeforeGettingSurgeryEvent(float initialTime) : base(initialTime) { }
    }
}
