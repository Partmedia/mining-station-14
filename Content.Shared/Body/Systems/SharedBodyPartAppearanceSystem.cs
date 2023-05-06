using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Robust.Shared.GameStates;
using Content.Shared.Humanoid;
using static Content.Shared.Humanoid.HumanoidAppearanceState;
using Content.Shared.Humanoid.Prototypes;

namespace Content.Shared.Body.Systems;

public abstract class SharedBodyPartAppearanceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartAppearanceComponent, ComponentInit>(OnPartAppearanceInit);
        SubscribeLocalEvent<BodyPartAppearanceComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<BodyPartAppearanceComponent, ComponentGetState>(OnGetState);
    }

    private void OnPartAppearanceInit(EntityUid uid, BodyPartAppearanceComponent component, ComponentInit args)
    {

        if (TryComp(uid, out BodyPartComponent? part) && part.OriginalBody != null &&
            TryComp(part.OriginalBody.Value, out HumanoidAppearanceComponent? bodyAppearance))
        {
            component.OriginalBody = part.OriginalBody.Value;
            var symmetry = ((BodyPartSymmetry) part.Symmetry).ToString();
            if (symmetry == "None")
                symmetry = "";
            component.ID = "removed" + symmetry + ((BodyPartType) part.PartType).ToString();
            component.Color = bodyAppearance.SkinColor;
            
        }
        Dirty(component);
        UpdateAppearance(uid, component);
    }

    private void OnHandleState(EntityUid uid, BodyPartAppearanceComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not BodyPartAppearanceComponentState state)
            return;

        component.ID = state.ID;
        component.Color = state.Color;
        component.OriginalBody = state.OriginalBody;

        Dirty(component);
        UpdateAppearance(uid, component);
    }

    private void OnGetState(EntityUid uid, BodyPartAppearanceComponent component, ref ComponentGetState args)
    {
        args.State = new BodyPartAppearanceComponentState(component.ID,component.Color,component.OriginalBody);
    }

    protected abstract void UpdateAppearance(EntityUid uid, BodyPartAppearanceComponent component);
}
