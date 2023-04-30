using Robust.Shared.Serialization;
using Content.Shared.Body.Part;
//using Content.Shared.Body.Organ;

namespace Content.Shared.Surgery
{
    /// <summary>
    /// This class holds constants that are shared between client and server.
    /// </summary>
    public sealed class SharedSurgery
    {
  
    }

    [Serializable, NetSerializable]
    public sealed class SurgeryBoundUserInterfaceState : BoundUserInterfaceState
    {
        public List<BodyPartSlot> BodyPartSlots;
        //organSlots

        public SurgeryBoundUserInterfaceState(List<BodyPartSlot> bodyPartSlots)
        {
            BodyPartSlots = bodyPartSlots;
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
