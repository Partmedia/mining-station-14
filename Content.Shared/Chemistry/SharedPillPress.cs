using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Chemistry
{
    /// <summary>
    /// This class holds constants that are shared between client and server.
    /// </summary>
    public sealed class SharedPillPress
    {
        public const uint PillTypes = 20;
        public const string InputSlotName = "beakerSlot";
        public const string OutputSlotName = "outputSlot";
        public const string PillSolutionName = "food";
        public const uint LabelMaxLength = 50;

        [Serializable, NetSerializable]
        public enum PillPressVisualState : byte
        {
            BeakerAttached,
            OutputAttached
        }
    }

    [Serializable, NetSerializable]
    public sealed class PillPressSetPillTypeMessage : BoundUserInterfaceMessage
    {
        public readonly uint PillType;

        public PillPressSetPillTypeMessage(uint pillType)
        {
            PillType = pillType;
        }
    }

    [Serializable, NetSerializable]
    public sealed class PillPressCreatePillsMessage : BoundUserInterfaceMessage
    {
        public readonly uint Dosage;
        public readonly uint Number;
        public readonly string Label;

        public PillPressCreatePillsMessage(uint dosage, uint number, string label)
        {
            Dosage = dosage;
            Number = number;
            Label = label;
        }
    }

    /// <summary>
    /// Information about the capacity and contents of a container for display in the UI
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class PillPressContainerInfo
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

        public PillPressContainerInfo(
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
    public sealed class PillPressBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly PillPressContainerInfo? InputContainerInfo;
        public readonly PillPressContainerInfo? OutputContainerInfo;

        /// <summary>
        /// A list of the reagents and their amounts within the buffer, if applicable.
        /// </summary>
        /// 
        public readonly FixedPoint2? BufferCurrentVolume;
        public readonly uint SelectedPillType;

        public readonly uint PillDosageLimit;

        public readonly bool UpdateLabel;

        public PillPressBoundUserInterfaceState(
            PillPressContainerInfo? inputContainerInfo, PillPressContainerInfo? outputContainerInfo,
            uint selectedPillType, uint pillDosageLimit, bool updateLabel)
        {
            InputContainerInfo = inputContainerInfo;
            OutputContainerInfo = outputContainerInfo;
            SelectedPillType = selectedPillType;
            PillDosageLimit = pillDosageLimit;
            UpdateLabel = updateLabel;
        }
    }

    [Serializable, NetSerializable]
    public enum PillPressUiKey
    {
        Key
    }
}
