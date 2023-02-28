using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Chemistry
{
    /// <summary>
    /// This class holds constants that are shared between client and server.
    /// </summary>
    public sealed class SharedBiopress
    {
        public const string BufferSolutionName = "buffer";
        public const string OutputSlotName = "beakerSlot";

        //[Serializable, NetSerializable]
        //public enum BiopressVisualState : byte
        //{
        //    OutputAttached
        //}
    }

    [Serializable, NetSerializable]
    public sealed class BiopressSetModeMessage : BoundUserInterfaceMessage
    {
        public readonly BiopressMode BiopressMode;

        public BiopressSetModeMessage(BiopressMode mode)
        {
            BiopressMode = mode;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BiopressReagentAmountButtonMessage : BoundUserInterfaceMessage
    {
        public readonly string ReagentId;
        public readonly BiopressReagentAmount Amount;
        public readonly bool FromBuffer;

        public BiopressReagentAmountButtonMessage(string reagentId, BiopressReagentAmount amount, bool fromBuffer)
        {
            ReagentId = reagentId;
            Amount = amount;
            FromBuffer = fromBuffer;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BiopressActivateButtonMessage : BoundUserInterfaceMessage
    {
        public readonly bool Activated;

        public BiopressActivateButtonMessage(bool activated)
        {
            Activated = activated;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BiopressStopButtonMessage : BoundUserInterfaceMessage
    {
        public readonly bool Activated;

        public BiopressStopButtonMessage(bool activated)
        {
            Activated = activated;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BiopressStoreToggleButtonMessage : BoundUserInterfaceMessage
    {
        public readonly bool Activated;

        public BiopressStoreToggleButtonMessage(bool activated)
        {
            Activated = activated;
        }
    }

    public enum BiopressMode
    {
        Transfer,
        Discard,
    }

    public enum BiopressStage
    {
        Initial,
        SmallMatter,
        LargeMatter,
        Final,
    }

    public enum BiopressReagentAmount
    {
        U1 = 1,
        U5 = 5,
        U10 = 10,
        U25 = 25,
        All,
    }

    public static class BiopressReagentAmountToFixedPoint
    {
        public static FixedPoint2 GetFixedPoint(this BiopressReagentAmount amount)
        {
            if (amount == BiopressReagentAmount.All)
                return FixedPoint2.MaxValue;
            else
                return FixedPoint2.New((int)amount);
        }
    }

    /// <summary>
    /// Information about the capacity and contents of a container for display in the UI
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BiopressContainerInfo
    {
        /// <summary>
        /// The container name to show to the player
        /// </summary>
        public readonly string DisplayName;
        /// <summary>
        /// Whether the container holds reagents or entities
        /// </summary>
        public readonly bool HoldsReagents;
        /// <summary>
        /// The currently used volume of the container
        /// </summary>
        public readonly FixedPoint2 CurrentVolume;
        /// <summary>
        /// The maximum volume of the container
        /// </summary>
        public readonly FixedPoint2 MaxVolume;
        /// <summary>
        /// A list of the reagents/entities and their sizes within the container
        /// </summary>
        // todo: this causes NetSerializer exceptions if it's an IReadOnlyList (which would be preferred)
        public readonly List<(string Id, FixedPoint2 Quantity)> Contents;

        public BiopressContainerInfo(
            string displayName, bool holdsReagents,
            FixedPoint2 currentVolume, FixedPoint2 maxVolume,
            List<(string, FixedPoint2)> contents)
        {
            DisplayName = displayName;
            HoldsReagents = holdsReagents;
            CurrentVolume = currentVolume;
            MaxVolume = maxVolume;
            Contents = contents;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BiopressBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly BiopressContainerInfo? OutputContainerInfo;

        /// <summary>
        /// A list of the reagents and their amounts within the buffer, if applicable.
        /// </summary>
        public readonly IReadOnlyList<Solution.ReagentQuantity> BufferReagents;

        public readonly BiopressMode Mode;

        public readonly FixedPoint2? BufferCurrentVolume;

        public BiopressBoundUserInterfaceState(
            BiopressMode mode, BiopressContainerInfo? outputContainerInfo,
            IReadOnlyList<Solution.ReagentQuantity> bufferReagents, FixedPoint2 bufferCurrentVolume)
        {
            OutputContainerInfo = outputContainerInfo;
            BufferReagents = bufferReagents;
            Mode = mode;
            BufferCurrentVolume = bufferCurrentVolume;
        }
    }

    [Serializable, NetSerializable]
    public enum BiopressUiKey
    {
        Key
    }
}
