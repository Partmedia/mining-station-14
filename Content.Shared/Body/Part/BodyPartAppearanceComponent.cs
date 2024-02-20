using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.GameStates;

namespace Content.Shared.Body.Part;

[RegisterComponent, NetworkedComponent]
public sealed class BodyPartAppearanceComponent : Component
{

    /// <summary>
    ///     ID of this custom base layer. Must be a <see cref="HumanoidSpeciesSpriteLayer"/>.
    /// </summary>
    [DataField("id", customTypeSerializer: typeof(PrototypeIdSerializer<HumanoidSpeciesSpriteLayer>))]
    public string? ID { get; set; }

    /// <summary>
    ///     Color of this custom base layer. Null implies skin colour if the corresponding <see cref="HumanoidSpeciesSpriteLayer"/> is set to match skin.
    /// </summary>
    [DataField("color")]
    public Color? Color { get; set; }

    public EntityUid? OriginalBody { get; set; }

    //TODO add other custom variables such as species and markings - in case someone decides to attach a lizard arm to a human for example
}
