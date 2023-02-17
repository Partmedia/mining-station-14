using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner
{
    public abstract class SharedMedicalThermometerComponent : Component
    {
        /// <summary>
        ///     On interacting with an entity retrieves the entity UID for use with getting the current temperature of the mob.
        /// </summary>
        [Serializable, NetSerializable]
        public sealed class MedicalThermometerScannedUserMessage : BoundUserInterfaceMessage
        {
            public readonly EntityUid? TargetEntity;
            public readonly float EntityTemperature;

            public MedicalThermometerScannedUserMessage(EntityUid? targetEntity, float entityTemperature)
            {
                TargetEntity = targetEntity;
                EntityTemperature = entityTemperature;
            }
        }

        [Serializable, NetSerializable]
        public enum MedicalThermometerUiKey : byte
        {
            Key
        }
    }
}
