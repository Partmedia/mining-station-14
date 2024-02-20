using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Mining.Components
{
    /// <summary>
    /// This class holds constants that are shared between client and server.
    /// </summary>
    public sealed class SharedFurnaceComponent
    {
    }

    [Serializable, NetSerializable]
    public sealed class FurnacePourButtonMessage : BoundUserInterfaceMessage
    {
        public FurnacePourButtonMessage() { }
    }

    [Serializable, NetSerializable]
    public sealed class FurnaceStoreToggleButtonMessage : BoundUserInterfaceMessage
    {
        public FurnaceStoreToggleButtonMessage(){}
    }

    [Serializable, NetSerializable]
    public sealed class SetTargetPowerMessage : BoundUserInterfaceMessage
    {
        public float TargetPower;

        public SetTargetPowerMessage(float targetPower)
        {
            TargetPower = targetPower;
        }
    }

    [Serializable, NetSerializable]
    public sealed class FurnaceBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly bool Opened;
        public readonly float Temperature;
        public readonly float Power;

        public FurnaceBoundUserInterfaceState(bool opened, float temp, float power)
        {
            Opened = opened;
            Temperature = temp;
            Power = power;
        }
    }

    [Serializable, NetSerializable]
    public enum FurnaceUiKey
    {
        Key
    }
}
