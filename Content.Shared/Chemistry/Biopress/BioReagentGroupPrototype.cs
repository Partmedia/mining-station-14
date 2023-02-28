using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.Chemistry.Biopress
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable, NetSerializable, Prototype("bioReagentGroup")]
    public sealed class BioReagentGroupPrototype : IPrototype
    {
        [ViewVariables]
        [IdDataField]
        public string ID { get; } = default!;

        //base units of reagents present in biological entity
        [DataField("reagentProportions")]
        public Dictionary<string, FixedPoint2>? ReagentProportions = null;

    }
}
