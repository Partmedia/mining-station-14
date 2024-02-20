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
            public Dictionary<string, string> OrganConditions;
            public float BloodLevel;
            public bool Sedated;

            public HealthAnalyzerScannedUserMessage(EntityUid? targetEntity, float temperature, Dictionary<string,string> organConditions, bool sedated, float bloodLevel)
            {
                TargetEntity = targetEntity;
                Temperature = temperature;
                OrganConditions = organConditions;
                Sedated = sedated;
                BloodLevel = bloodLevel;
            }
        }

        [Serializable, NetSerializable]
        public enum HealthAnalyzerUiKey : byte
        {
            Key
        }
    }
}
