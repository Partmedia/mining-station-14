using Robust.Shared.Serialization;

namespace Content.Shared.Chemistry
{
    /// <summary>
    /// This class holds constants that are shared between client and server.
    /// </summary>
    public sealed class SharedChemAnalyzer
    {
        public const string OutputSlotName = "beakerSlot";
    }

    [Serializable, NetSerializable]
    public sealed class ChemAnalyzerBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly ContainerInfo? OutputContainer;

        public ChemAnalyzerBoundUserInterfaceState(ContainerInfo? outputContainer)
        {
            OutputContainer = outputContainer;
        }
    }

    [Serializable, NetSerializable]
    public enum ChemAnalyzerUiKey
    {
        Key
    }
}
