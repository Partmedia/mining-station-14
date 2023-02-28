using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Content.Shared.Damage;

namespace Content.Server.Chemistry.Components
{
    [RegisterComponent]
    public sealed class BiopressComponent : Component
    {
        /// <summary>
        /// Is the machine actively doing something and can't be used right now?
        /// </summary>
        public bool Active;

        //YAML serialization vars
        [ViewVariables(VVAccess.ReadWrite)] [DataField("workTime")] public int WorkTime = 3500; //3.5 seconds, completely arbitrary for now.
        [DataField("clickSound")] public SoundSpecifier ClickSound { get; set; } = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");
        [DataField("grindSound")] public SoundSpecifier GrindSound { get; set; } = new SoundPathSpecifier("/Audio/Machines/blender.ogg");
        [DataField("hydraulicSound")] public SoundSpecifier HydraulicSound { get; set; } = new SoundPathSpecifier("/Audio/Machines/hydraulic.ogg");

        //TODO this is way too wimpy, replace it with something better
        [DataField("incinerateSound")] public SoundSpecifier IncinerateSound { get; set; } = new SoundPathSpecifier("/Audio/Effects/burning.ogg");

        [DataField("mode"), ViewVariables(VVAccess.ReadWrite)]
        public BiopressMode Mode = BiopressMode.Transfer;

        [DataField("intervalTime"), ViewVariables(VVAccess.ReadWrite)]
        public float IntervalTime =  5f;

        public BiopressStage Stage = BiopressStage.Initial;

        /// <summary>
        /// tracks time between processes
        /// </summary>
        [ViewVariables]
        public float ProcessingTimer = default;

        [DataField("smallDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier SmallDamage = default!;

        [DataField("largeDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier LargeDamage = default!;

        //how much ash to produce per junk (rounded)
        public float AshFactor = 0.1F;
    }
}
