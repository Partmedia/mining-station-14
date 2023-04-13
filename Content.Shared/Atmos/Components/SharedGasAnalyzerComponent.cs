using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Components
{
    [NetworkedComponent()]
    public abstract class SharedGasAnalyzerComponent : Component
    {

        [Serializable, NetSerializable]
        public enum GasAnalyzerUiKey
        {
            Key,
        }

        /// <summary>
        /// Atmospheric data is gathered in the system and sent to the user
        /// </summary>
        [Serializable, NetSerializable]
        public sealed class GasAnalyzerUserMessage : BoundUserInterfaceMessage
        {
            public string DeviceName;
            public EntityUid DeviceUid;
            public bool DeviceFlipped;
            public string? Error;
            public GasMixEntry[] NodeGasMixes;
            public GasAnalyzerUserMessage(GasMixEntry[] nodeGasMixes, string deviceName, EntityUid deviceUid, bool deviceFlipped, string? error = null)
            {
                NodeGasMixes = nodeGasMixes;
                DeviceName = deviceName;
                DeviceUid = deviceUid;
                DeviceFlipped = deviceFlipped;
                Error = error;
            }
        }

        /// <summary>
        /// Contains information on a gas mix entry, turns into a tab in the UI
        /// </summary>
        [Serializable, NetSerializable]
        public struct GasMixEntry
        {
            /// <summary>
            /// Name of the tab in the UI
            /// </summary>
            public readonly string Name;
            public readonly float Pressure;
            public readonly float Temperature;
            public readonly float LiquidHeight;
            public readonly GasEntry[]? Gases;
            public readonly LiquidEntry[]? Liquids;

            public GasMixEntry(string name, float pressure, float temperature, float liquidHeight, GasEntry[]? gases = null, LiquidEntry[]? liquids = null)
            {
                Name = name;
                Pressure = pressure;
                Temperature = temperature;
                LiquidHeight = liquidHeight;
                Gases = gases;
                Liquids = liquids;
            }
        }

        /// <summary>
        /// Individual gas entry data for populating the UI
        /// </summary>
        [Serializable, NetSerializable]
        public struct GasEntry
        {
            public readonly string Name;
            public readonly float Amount;
            public readonly string Color;

            public GasEntry(string name, float amount, string color)
            {
                Name = name;
                Amount = amount;
                Color = color;
            }

            public override string ToString()
            {
                // e.g. "Plasma: 2000 mol"
                return Loc.GetString(
                    "gas-entry-info",
                     ("gasName", Name),
                     ("gasAmount", Amount));
            }
        }

        /// <summary>
        /// Individual liquid entry data for populating the UI
        /// </summary>
        [Serializable, NetSerializable]
        public struct LiquidEntry
        {
            public readonly string Name;
            public readonly float Amount;
            public readonly string Color;

            public LiquidEntry(string name, float amount, string color)
            {
                Name = name;
                Amount = amount;
                Color = color;
            }

            public override string ToString()
            {
                // e.g. "Plasma: 1200u"
                return Loc.GetString(
                    "liquid-entry-info",
                     ("liquidName", Name),
                     ("liquidAmount", Amount));
            }
        }

        [Serializable, NetSerializable]
        public sealed class GasAnalyzerDisableMessage : BoundUserInterfaceMessage
        {
            public GasAnalyzerDisableMessage() {}
        }
    }

    [Serializable, NetSerializable]
    public enum GasAnalyzerVisuals : byte
    {
        Enabled,
    }
}
