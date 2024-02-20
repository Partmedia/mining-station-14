using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Part;

[Serializable, NetSerializable]
public sealed class BodyPartAppearanceComponentState : ComponentState
{
    public readonly string? ID;
    public readonly Color? Color;
    public readonly EntityUid? OriginalBody;


    public BodyPartAppearanceComponentState(
        string? id,
        Color? color,
        EntityUid? originalBody)
    {
        ID = id;
        Color = color;
        OriginalBody = originalBody;
    }
}
