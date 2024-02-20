using System.Threading;
using Content.Server.DoAfter;
using Content.Server.Medical.Components;
using Content.Server.Temperature.Components;
using Content.Server.Popups;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using static Content.Shared.MedicalScanner.SharedMedicalThermometerComponent;

namespace Content.Server.Medical
{
    public sealed class MedicalThermometerSystem : EntitySystem
    {
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MedicalThermometerComponent, ActivateInWorldEvent>(HandleActivateInWorld);
            SubscribeLocalEvent<MedicalThermometerComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<TargetScanSuccessfulEvent>(OnTargetScanSuccessful);
            SubscribeLocalEvent<ScanCancelledEvent>(OnScanCancelled);
        }

        private void HandleActivateInWorld(EntityUid uid, MedicalThermometerComponent medicalThermometer, ActivateInWorldEvent args)
        {
            OpenUserInterface(args.User, medicalThermometer);
        }

        private void OnAfterInteract(EntityUid uid, MedicalThermometerComponent medicalThermometer, AfterInteractEvent args)
        {
            if (medicalThermometer.CancelToken != null)
            {
                medicalThermometer.CancelToken.Cancel();
                medicalThermometer.CancelToken = null;
                return;
            }

            if (args.Target == null)
                return;

            if (!args.CanReach)
                return;

            if (medicalThermometer.CancelToken != null)
                return;

            medicalThermometer.CancelToken = new CancellationTokenSource();
            _doAfterSystem.DoAfter(new DoAfterEventArgs(args.User, medicalThermometer.ScanDelay, medicalThermometer.CancelToken.Token, target: args.Target)
            {
                BroadcastFinishedEvent = new TargetScanSuccessfulEvent(args.User, args.Target, medicalThermometer),
                BroadcastCancelledEvent = new ScanCancelledEvent(medicalThermometer),
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                BreakOnStun = true,
                NeedHand = true
            });
        }

        private void OnTargetScanSuccessful(TargetScanSuccessfulEvent args)
        {
            args.Component.CancelToken = null;
            UpdateScannedUser(args.Component.Owner, args.User, args.Target, args.Component);
        }

        private void OpenUserInterface(EntityUid user, MedicalThermometerComponent medicalThermometer)
        {
            if (!TryComp<ActorComponent>(user, out var actor))
                return;

            medicalThermometer.UserInterface?.Open(actor.PlayerSession);
        }

        public void UpdateScannedUser(EntityUid uid, EntityUid user, EntityUid? target, MedicalThermometerComponent? medicalThermometer)
        {
            if (!Resolve(uid, ref medicalThermometer))
                return;

            if (target == null || medicalThermometer.UserInterface == null)
                return;

            if (!TryComp<TemperatureComponent>(target.Value, out var temperature))
                return;

            OpenUserInterface(user, medicalThermometer);
            medicalThermometer.UserInterface?.SendMessage(new MedicalThermometerScannedUserMessage(target,temperature.CurrentTemperature));
        }

        private static void OnScanCancelled(ScanCancelledEvent args)
        {
            args.MedicalThermometer.CancelToken = null;
        }

        private sealed class ScanCancelledEvent : EntityEventArgs
        {
            public readonly MedicalThermometerComponent MedicalThermometer;
            public ScanCancelledEvent(MedicalThermometerComponent medicalThermometer)
            {
                MedicalThermometer = medicalThermometer;
            }
        }

        private sealed class TargetScanSuccessfulEvent : EntityEventArgs
        {
            public EntityUid User { get; }
            public EntityUid? Target { get; }
            public MedicalThermometerComponent Component { get; }

            public TargetScanSuccessfulEvent(EntityUid user, EntityUid? target, MedicalThermometerComponent component)
            {
                User = user;
                Target = target;
                Component = component;
            }
        }
    }
}
