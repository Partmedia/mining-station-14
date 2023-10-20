using System.Threading.Tasks;
using System.Threading;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.Electrocution;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Medical;
using Content.Server.Mind;
using Content.Server.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Timing;
using Content.Shared.Toggleable;
using Robust.Server.Player;
using Robust.Shared.Players;
using Robust.Shared.Timing;
using Content.Server.Body.Systems;
using Content.Server.Body.Components;

namespace Content.Server.Medical;

/// <summary>
/// This handles interactions and logic relating to <see cref="DefibrillatorComponent"/>
/// </summary>
public sealed class DefibrillatorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ChatSystem _chatManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly CirculatoryPumpSystem _pump = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<DefibrillatorComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<DefibrillatorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DefibrillatorComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnUnpaused(EntityUid uid, DefibrillatorComponent component, ref EntityUnpausedEvent args)
    {
        if (component.NextZapTime == null)
            return;

        component.NextZapTime = component.NextZapTime.Value + args.PausedTime;
    }

    private void OnUseInHand(EntityUid uid, DefibrillatorComponent component, UseInHandEvent args)
    {
        if (args.Handled || _useDelay.ActiveDelay(uid))
            return;

        if (!TryToggle(uid, component, args.User))
            return;
        args.Handled = true;
        _useDelay.BeginDelay(uid);
    }

    private async void OnAfterInteract(EntityUid uid, DefibrillatorComponent component, AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;
        args.Handled = await TryStartZap(uid, target, args.User, component);
    }

    private void ZapDoAfter(EntityUid uid, DefibrillatorComponent component, EntityUid target, EntityUid user)
    {
        if (!CanZap(uid, target, user, component))
            return;

        Zap(uid, target, user, component);
    }

    public bool TryToggle(EntityUid uid, DefibrillatorComponent? component = null, EntityUid? user = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return component.Enabled
            ? TryDisable(uid, component)
            : TryEnable(uid, component, user);
    }

    public bool TryEnable(EntityUid uid, DefibrillatorComponent? component = null, EntityUid? user = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (component.Enabled)
            return false;

        component.Enabled = true;
        _appearance.SetData(uid, ToggleVisuals.Toggled, true);
        _audio.PlayPvs(component.PowerOnSound, uid);
        return true;
    }

    public bool TryDisable(EntityUid uid, DefibrillatorComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!component.Enabled)
            return false;

        component.Enabled = false;
        _appearance.SetData(uid, ToggleVisuals.Toggled, false);
        _audio.PlayPvs(component.PowerOffSound, uid);
        return true;
    }

    public bool CanZap(EntityUid uid, EntityUid target, EntityUid? user = null, DefibrillatorComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!component.Enabled)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("defibrillator-not-on"), uid, user.Value);
            return false;
        }

        if (_timing.CurTime < component.NextZapTime)
            return false;

        if (!TryComp<MobStateComponent>(target, out var mobState))
            return false;

        return true;
    }

    public async Task<bool> TryStartZap(EntityUid uid, EntityUid target, EntityUid user, DefibrillatorComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!CanZap(uid, target, user, component))
            return false;

        var doAfterArgs = new DoAfterEventArgs(user, component.DoAfterDuration, CancellationToken.None, target)
        {
            BreakOnStun = false,
            BreakOnDamage = false,
            BreakOnTargetMove = true,
            BreakOnUserMove = true,
        };

        _audio.PlayPvs(component.ChargeSound, uid);

        var result = await _doAfter.WaitDoAfter(doAfterArgs);

        if (result != DoAfterStatus.Finished) return false;

        ZapDoAfter(uid, component, target, user);

        return true;
    }

    public void Zap(EntityUid uid, EntityUid target, EntityUid user, DefibrillatorComponent? component = null, MobStateComponent? mob = null, MobThresholdsComponent? thresholds = null)
    {
        if (!Resolve(uid, ref component) || !Resolve(target, ref mob, ref thresholds, false))
            return;

        if (!TryComp<CirculatoryPumpComponent>(target, out var pump))
            return;

        _audio.PlayPvs(component.ZapSound, uid);
        _electrocution.TryDoElectrocution(target, null, component.ZapDamage, component.WritheDuration, true, ignoreInsulation: true);
        component.NextZapTime = _timing.CurTime + component.ZapDelay;
        _appearance.SetData(uid, DefibrillatorVisuals.Ready, false);

        IPlayerSession? session = null;
        if (TryComp<MindComponent>(target, out var mindComp) &&
            mindComp.Mind?.UserId != null &&
            _playerManager.TryGetSessionById(mindComp.Mind.UserId.Value, out session))
        {
            // notify them they're being revived.
            if (mindComp.Mind != null && mindComp.Mind.CurrentEntity != target)
            {
                _chatManager.TrySendInGameICMessage(uid, Loc.GetString("defibrillator-ghosted"),
                    InGameICChatType.Speak, true, true);
                _euiManager.OpenEui(new ReturnToBodyEui(mindComp.Mind), session);
            }
        }
        else
        {
            _chatManager.TrySendInGameICMessage(uid, Loc.GetString("defibrillator-no-mind"),
                InGameICChatType.Speak, true, true);
        }

        if (_mobState.IsDead(target, mob))
            _damageable.TryChangeDamage(target, component.ZapHeal, true, origin: uid);

        _pump.StartPump(target, pump);

        var sound = pump.Working
            ? component.SuccessSound
            : component.FailureSound;
        _audio.PlayPvs(sound, uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var defib in EntityManager.EntityQuery<DefibrillatorComponent>())
        {
            var uid = defib.Owner;
            if (defib.NextZapTime == null || _timing.CurTime < defib.NextZapTime)
                continue;

            _audio.PlayPvs(defib.ReadySound, uid);
            _appearance.SetData(uid, DefibrillatorVisuals.Ready, true);
            defib.NextZapTime = null;
        }
    }
}
