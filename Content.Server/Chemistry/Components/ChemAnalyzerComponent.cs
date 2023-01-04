using Content.Shared.Containers.ItemSlots;
using Content.Shared.Chemistry;
using Content.Server.Chemistry.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Chemistry.Components
{
    /// <summary>
    /// A machine that dispenses reagents into a solution container.
    /// </summary>
    [RegisterComponent]
    [Access(typeof(ChemAnalyzerSystem))]
    public sealed class ChemAnalyzerComponent : Component
    {

        [DataField("clickSound"), ViewVariables(VVAccess.ReadWrite)]
        public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    }
}
