using Content.Server.Objectives.Interfaces;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Server.Objectives.Conditions;

[DataDefinition]
public sealed class UntrackedCondition : IObjectiveCondition, ISerializationHooks
{
    private Mind.Mind? _mind;
    [DataField("title")] string TitleString;
    [DataField("description")] string DescriptionString;
    [DataField("prototype")] private string _prototypeId = string.Empty;

    public IObjectiveCondition GetAssigned(Mind.Mind mind)
    {
        return new UntrackedCondition
        {
            _mind = mind,
            _prototypeId = _prototypeId,
            TitleString = TitleString,
            DescriptionString = DescriptionString,
        };
    }

    public string Title => Loc.GetString(TitleString);

    public string Description => Loc.GetString(DescriptionString);

    public SpriteSpecifier Icon => new SpriteSpecifier.EntityPrototype(_prototypeId);

    public float Difficulty => 0;

    public float Progress { get; } = 0;

    public bool Equals(IObjectiveCondition? other)
    {
        return other is UntrackedCondition cond &&
               Equals(_mind, cond._mind) &&
               _prototypeId == cond._prototypeId &&
               TitleString == cond.TitleString &&
               DescriptionString == cond.DescriptionString;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((UntrackedCondition) obj);
    }
}
