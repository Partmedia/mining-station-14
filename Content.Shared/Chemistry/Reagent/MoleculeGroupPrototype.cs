using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.Chemistry.Reagent;

/// <summary>
/// 
/// </summary>
[Serializable, NetSerializable, Prototype("moleculeGroup")]
public sealed class MoleculeGroupPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; } = default!;

    //base units of reagents present in molecule
    [DataField("reagentProportions")]
    public Dictionary<string, FixedPoint2>? ReagentProportions = null;

}
