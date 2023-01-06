using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.Containers;

namespace Content.Server.Chemistry.Components
{
    [RegisterComponent]
    public sealed class PillPressComponent : Component
    {
        [DataField("pillType"), ViewVariables(VVAccess.ReadWrite)]
        public uint PillType = 0;

        [DataField("pillDosageLimit", required: true), ViewVariables(VVAccess.ReadWrite)]
        public uint PillDosageLimit;

        [DataField("clickSound"), ViewVariables(VVAccess.ReadWrite)]
        public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");
    }
}
