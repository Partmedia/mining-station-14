using Content.Server.Power.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Server.Mining;

public enum HeaterState
{
    Off, Low, Medium, High
}

[RegisterComponent]
public class HeaterComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("state")]
    public HeaterState State = HeaterState.Off;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("maxPower")]
    public float MaxPower = 6000f;
}

public sealed class HeaterSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HeaterComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<HeaterComponent, GetVerbsEvent<InteractionVerb>>(AddInsertOtherVerb);
        SubscribeLocalEvent<HeaterComponent, ExaminedEvent>(OnExamined);
    }

    private void OnActivate(EntityUid uid, HeaterComponent component, ActivateInWorldEvent args)
    {
        Toggle(uid, args.User, component);
        args.Handled = true;
    }

    private void AddInsertOtherVerb(EntityUid uid, HeaterComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        InteractionVerb verb = new()
        {
            Act = () => Toggle(component.Owner, args.Target, component),
            Text = "Toggle"
        };
        args.Verbs.Add(verb);
    }

    private void Toggle(EntityUid uid, EntityUid user, HeaterComponent comp)
    {
        comp.State = NextState(comp.State);
        SetState(uid, comp, comp.State);
        _popupSystem.PopupEntity($"You turn the dial to {StateString(comp.State)}.", uid);
    }

    private void OnExamined(EntityUid uid, HeaterComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup($"The dial is set to {StateString(component.State)}.");
    }

    private string StateString(HeaterState state)
    {
        switch (state)
        {
            case HeaterState.Off:
                return "OFF";
            case HeaterState.Low:
                return "LOW";
            case HeaterState.Medium:
                return "MED";
            default:
                return "HIGH";
        }
    }

    private HeaterState NextState(HeaterState state)
    {
        switch (state)
        {
            case HeaterState.Off:
                return HeaterState.Low;
            case HeaterState.Low:
                return HeaterState.Medium;
            case HeaterState.Medium:
                return HeaterState.High;
            default:
                return HeaterState.Off;
        }
    }

    private void SetState(EntityUid uid, HeaterComponent comp, HeaterState state)
    {
        if (!TryComp<ApcPowerReceiverComponent>(uid, out var power))
            return;

        switch (state)
        {
            case HeaterState.Off:
                power.Load = 0f;
                break;
            case HeaterState.Low:
                power.Load = 0.4f * comp.MaxPower;
                break;
            case HeaterState.Medium:
                power.Load = 0.6f * comp.MaxPower;
                break;
            case HeaterState.High:
                power.Load = comp.MaxPower;
                break;
        }
    }
}
