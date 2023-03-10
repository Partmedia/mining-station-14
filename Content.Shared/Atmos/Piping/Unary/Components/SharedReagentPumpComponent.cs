using Content.Shared.FixedPoint;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Piping.Unary.Components;

public sealed class SharedReagentPump
{
    public const string BufferSolutionName = "buffer";
    public const string OutputSlotName = "beakerSlot";
}

[Serializable, NetSerializable]
public sealed class ReagentPumpSetModeMessage : BoundUserInterfaceMessage
{
    public readonly ReagentPumpMode ReagentPumpMode;

    public ReagentPumpSetModeMessage(ReagentPumpMode mode)
    {
        ReagentPumpMode = mode;
    }
}

[Serializable, NetSerializable]
public sealed class InjectMessage : BoundUserInterfaceMessage
{
    public InjectMessage()
    {

    }
}

[Serializable, NetSerializable]
public sealed class ExtractMessage : BoundUserInterfaceMessage
{
    public ExtractMessage()
    {

    }
}

[Serializable, NetSerializable]
public sealed class ReagentPumpReagentAmountButtonMessage : BoundUserInterfaceMessage
{
    public readonly string ReagentId;
    public readonly ReagentPumpReagentAmount Amount;
    public readonly bool FromBuffer;

    public ReagentPumpReagentAmountButtonMessage(string reagentId, ReagentPumpReagentAmount amount, bool fromBuffer)
    {
        ReagentId = reagentId;
        Amount = amount;
        FromBuffer = fromBuffer;
    }
}

[Serializable]
[NetSerializable]
public enum ReagentPumpUiKey
{
    Key
}


public enum ReagentPumpMode
{
    Transfer,
    Discard,
}

public enum ReagentPumpReagentAmount
{
    U1 = 1,
    U5 = 5,
    U10 = 10,
    U25 = 25,
    All,
}

public static class ReagentPumpReagentAmountToFixedPoint
{
    public static FixedPoint2 GetFixedPoint(this ReagentPumpReagentAmount amount)
    {
        if (amount == ReagentPumpReagentAmount.All)
            return FixedPoint2.MaxValue;
        else
            return FixedPoint2.New((int) amount);
    }
}

/// <summary>
/// Information about the capacity and contents of a container for display in the UI
/// </summary>
[Serializable, NetSerializable]
public sealed class ReagentPumpContainerInfo
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

    public ReagentPumpContainerInfo(
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

[Serializable]
[NetSerializable]
public sealed class ReagentPumpBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly ReagentPumpContainerInfo? OutputContainerInfo;

    /// <summary>
    /// A list of the reagents and their amounts within the buffer, if applicable.
    /// </summary>
    public readonly IReadOnlyList<Solution.ReagentQuantity> BufferReagents;

    public readonly IReadOnlyList<Solution.ReagentQuantity> PipeReagents;

    public readonly ReagentPumpMode Mode;

    public readonly FixedPoint2? BufferCurrentVolume; 

    public ReagentPumpBoundUserInterfaceState(ReagentPumpMode mode, ReagentPumpContainerInfo? outputContainerInfo,
            IReadOnlyList<Solution.ReagentQuantity> bufferReagents, IReadOnlyList<Solution.ReagentQuantity> pipeReagents, FixedPoint2 bufferCurrentVolume)
    {

        OutputContainerInfo = outputContainerInfo;
        BufferReagents = bufferReagents;
        PipeReagents = pipeReagents;
        Mode = mode;
        BufferCurrentVolume = bufferCurrentVolume;
    }
}
