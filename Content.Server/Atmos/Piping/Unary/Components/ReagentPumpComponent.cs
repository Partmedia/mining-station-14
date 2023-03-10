using Content.Shared.Atmos;
using Content.Shared.FixedPoint;
using Content.Shared.Atmos.Piping.Unary.Components;
using Robust.Shared.Audio;

namespace Content.Server.Atmos.Piping.Unary.Components
{
    [RegisterComponent]
    public sealed class ReagentPumpComponent : Component
    {
        [DataField("clickSound")] public SoundSpecifier ClickSound { get; set; } = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");
        public SoundSpecifier PumpSound { get; set; } = new SoundPathSpecifier("/Audio/Machines/machine_vend_hot_drink.ogg"); //will try to find something a bit better
        public SoundSpecifier FinishSound { get; set; } = new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg"); 

        [DataField("mode"), ViewVariables(VVAccess.ReadWrite)]
        public ReagentPumpMode Mode = ReagentPumpMode.Transfer;

        [DataField("extractionAmount"), ViewVariables(VVAccess.ReadWrite)]
        public FixedPoint2 ExtractionAmount = 300f;

        public float AccumulatedUpdatetime = 2.01f;
        /// <summary>
        /// How often to update the reagent pump pipe reagent list
        /// </summary>
        public float UpdateInterval = 1f;

        public bool InjectBusy = false;
        public float InjectRunTime = 5f;
        public float AccumulatedInjectRuntime = 0f;

        public bool ExtractBusy = false;
        public float ExtractRunTime = 5f;
        public float AccumulatedExtractRuntime = 0f;
    }
}
