using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Biopress;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Chemistry.Components
{
    [RegisterComponent]
    public sealed class BiopressHarvestComponent : Component
    {
        //reagent group type (prototype)
        [DataField("id", customTypeSerializer: typeof(PrototypeIdSerializer<BioReagentGroupPrototype>))]
        public readonly string? BioReagentGroupId;

        //total reagent units
        [DataField("totalReagentUnits")] //TODO change name to something more like "proportions"
        [ViewVariables(VVAccess.ReadWrite)]
        public FixedPoint2 TotalReagentUnits;
    }
}
