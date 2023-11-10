using Robust.Shared.Serialization;
using Content.Shared.Chemistry;

namespace Content.Shared.Mining.Components
{
    public abstract class SharedOreAnalyzerComponent : Component
    {
        /// <summary>
        ///     On interacting with an entity retrieves the entity UID for use with getting the materials within the Ore.
        /// </summary>
        [Serializable, NetSerializable]
        public sealed class OreAnalyzerScannedUserMessage : BoundUserInterfaceMessage
        {
            public readonly EntityUid? TargetEntity;
            public readonly ContainerInfo? SolutionContainer;
            public readonly float? TargetMeltingTemp;

            public OreAnalyzerScannedUserMessage(EntityUid? targetEntity, ContainerInfo? solutionContainer, float? targetMeltingTemp)
            {
                TargetEntity = targetEntity;
                SolutionContainer = solutionContainer;
                TargetMeltingTemp = targetMeltingTemp;
            }
        }

        [Serializable, NetSerializable]
        public enum OreAnalyzerUiKey : byte
        {
            Key
        }
    }
}
