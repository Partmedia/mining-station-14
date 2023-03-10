using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner
{
    public abstract class SharedHealthAnalyzerComponent : Component
    {
        /// <summary>
        ///     On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
        /// </summary>
        [Serializable, NetSerializable]
        public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
        {
            public readonly EntityUid? TargetEntity;
            public float Temperature;

            public HealthAnalyzerScannedUserMessage(EntityUid? targetEntity, float temperature)
            {
                TargetEntity = targetEntity;
                Temperature = temperature;
            }
        }

        [Serializable, NetSerializable]
        public enum HealthAnalyzerUiKey : byte
        {
            Key
        }
    }
}
