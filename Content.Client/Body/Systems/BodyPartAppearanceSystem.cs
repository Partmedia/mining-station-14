using Content.Shared.Body.Components;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Part;
using Robust.Client.GameObjects;

namespace Content.Client.Body.Systems;

public sealed class BodyPartAppearanceSystem : SharedBodyPartAppearanceSystem
{
    protected override void UpdateAppearance(EntityUid uid, BodyPartAppearanceComponent? appearance = null)
    {
        if (appearance != null && TryComp(uid, out SpriteComponent? sprite))
        {
            if (appearance.Color != null)
            {
                sprite.Color = appearance.Color.Value;
            }
        }
    }
}
