
namespace Content.Server.Surgery
{
    [RegisterComponent]
    [Access(typeof(SurgerySystem))]
    public sealed class SurgeryToolComponent : Component
    {

        public bool Applying = false;

        [DataField("incisor")]
        public bool Incisor = false;

        [DataField("smallClamp")]
        public bool SmallClamp = false;

        [DataField("largeClamp")]
        public bool LargeClamp = false;

        [DataField("saw")]
        public bool Saw = false;

        [DataField("drill")]
        public bool Drill = false;

        [DataField("suture")]
        public bool Suture = false;

        [DataField("hardSuture")]
        public bool HardSuture = false;

        [DataField("cauterizer")]
        public bool Cauterizer = false;

        [DataField("manipulator")]
        public bool Manipulator = false;

        [DataField("retractor")]
        public bool Retractor = false;

        public float IncisorTime = 5f;

        public float SmallClampTime = 10f;

        public float LargeClampTime = 5f;

        public float SawTime = 15f;

        public float DrillTime = 10f;

        public float SutureTime = 15f;

        public float HardSutureTime = 15f;

        public float CauterizerTime = 2.5f;

        public float ManipulatorTime = 2.5f;

        public float RetractorTime = 10f;

        [DataField("incisorTimeMod")]
        public float IncisorTimeMod = 1f;

        [DataField("smallClampTimeMod")]
        public float SmallClampTimeMod = 1f;

        [DataField("largeClampTimeMod")]
        public float LargeClampTimeMod = 1f;

        [DataField("sawTimeMod")]
        public float SawTimeMod = 1f;

        [DataField("drillTimeMod")]
        public float DrillTimeMod = 1f;

        [DataField("sutureTimeMod")]
        public float SutureTimeMod = 1f;

        [DataField("hardSutureTimeMod")]
        public float HardSutureTimeMod = 1f;

        [DataField("cauterizerTimeMod")]
        public float CauterizerTimeMod = 1f;

        [DataField("manipulatorTimeMod")]
        public float ManipulatorTimeMod = 1f;

        [DataField("retractorTimeMod")]
        public float RetractorTimeMod = 1f;
    }
}
