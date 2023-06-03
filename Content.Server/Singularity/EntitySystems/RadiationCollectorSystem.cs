using Content.Server.Singularity.Components;
using Content.Shared.Interaction;
using Content.Shared.Singularity.Components;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Shared.Radiation.Events;
using Robust.Shared.Timing;
using Robust.Shared.Player;

namespace Content.Server.Singularity.EntitySystems
{
    public sealed class RadiationCollectorSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RadiationCollectorComponent, InteractHandEvent>(OnInteractHand);
            SubscribeLocalEvent<RadiationCollectorComponent, OnIrradiatedEvent>(OnRadiation);
        }

        private void OnInteractHand(EntityUid uid, RadiationCollectorComponent component, InteractHandEvent args)
        {
            var curTime = _gameTiming.CurTime;

            if(curTime < component.CoolDownEnd)
                return;

            ToggleCollector(uid, args.User, component);
            component.CoolDownEnd = curTime + component.Cooldown;
        }

        private void OnRadiation(EntityUid uid, RadiationCollectorComponent component, OnIrradiatedEvent args)
        {
            if (TryComp<PowerSupplierComponent>(uid, out var comp))
            {
                var charge = args.TotalRads * component.ChargeModifier;
                if (component.Enabled)
                    comp.MaxSupply = charge;
                else
                    comp.MaxSupply = 0;
            }
        }

        public void ToggleCollector(EntityUid uid, EntityUid? user = null, RadiationCollectorComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;
            SetCollectorEnabled(uid, !component.Enabled, user, component);
        }

        public void SetCollectorEnabled(EntityUid uid, bool enabled, EntityUid? user = null, RadiationCollectorComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;
            component.Enabled = enabled;

            // Show message to the player
            if (user != null)
            {
                var msg = component.Enabled ? "radiation-collector-component-use-on" : "radiation-collector-component-use-off";
                _popupSystem.PopupEntity(Loc.GetString(msg), uid);

            }

            // Update appearance
            UpdateAppearance(uid, component);
        }

        private void UpdateAppearance(EntityUid uid, RadiationCollectorComponent? component, AppearanceComponent? appearance = null)
        {
            if (!Resolve(uid, ref component, ref appearance))
                return;

            var state = component.Enabled ? RadiationCollectorVisualState.Active : RadiationCollectorVisualState.Deactive;
            appearance.SetData(RadiationCollectorVisuals.VisualState, state);
        }
    }
}
