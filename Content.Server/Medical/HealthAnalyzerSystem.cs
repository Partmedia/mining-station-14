
using Content.Server.DoAfter;
using Content.Server.Medical.Components;
using Content.Server.Disease;
using Content.Server.Temperature.Components;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Content.Server.Body.Systems;
using Content.Shared.Eye.Blinding;
using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Server.Surgery;
using Content.Shared.Body.Organ;
using static Content.Shared.MedicalScanner.SharedHealthAnalyzerComponent;

namespace Content.Server.Medical
{
    public sealed class HealthAnalyzerSystem : EntitySystem
    {
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly DiseaseSystem _disease = default!;
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly BodySystem _bodySystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<HealthAnalyzerComponent, ActivateInWorldEvent>(HandleActivateInWorld);
            SubscribeLocalEvent<HealthAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<HealthAnalyzerComponent, DoAfterEvent>(OnDoAfter);
            SubscribeLocalEvent<TargetScanSuccessfulEvent>(OnTargetScanSuccessful);
            SubscribeLocalEvent<ScanCancelledEvent>(OnScanCancelled);
        }

        public Dictionary<string, string> GetOrganFunctions(EntityUid uid)
        {
            var organFunctionConditions = new Dictionary<string, string>();
            if (!TryComp<BodyComponent>(uid, out var body))
                return organFunctionConditions;

            //organ conditions - these are tied to organs directly
            //bodies can have multiple of these
            var lungs = _bodySystem.GetBodyOrganComponents<LungComponent>(uid, body);
            var stomaches = _bodySystem.GetBodyOrganComponents<StomachComponent>(uid, body);

            //get all organ functions and their respective operating conditions
            //TODO LOC on all of these (keys used as is)

            //eyes
            if (TryComp<BlindableComponent>(uid, out var eyes))
                organFunctionConditions["Eyes (or equiv)"] = eyes.Condition.ToString(); //TODO

            //lungs
            if (lungs.Count > 1)
            {
                for (var i = 0; i < lungs.Count; i++)
                {
                    organFunctionConditions["Lung "+(i+1)+" (or equiv)"] = lungs[i].Comp.Condition.ToString();
                }
            }
            else if (lungs.Count == 0)
                organFunctionConditions["Lungs (or equiv)"] = OrganCondition.Missing.ToString();
            else
                organFunctionConditions["Lungs (or equiv)"] = lungs[0].Comp.Condition.ToString();

            //stomach
            if (stomaches.Count > 1)
            {
                for (var i = 0; i < stomaches.Count; i++)
                {
                    organFunctionConditions["Stomach " + (i + 1) + " (or equiv)"] = stomaches[i].Comp.Condition.ToString();
                }
            }
            else if (stomaches.Count == 0)
                organFunctionConditions["Stomach (or equiv)"] = OrganCondition.Missing.ToString();
            else
                organFunctionConditions["Stomach (or equiv)"] = stomaches[0].Comp.Condition.ToString();

            //heart
            if (!TryComp<CirculatoryPumpComponent>(uid, out var heart))
                organFunctionConditions["Heart (or equiv)"] = OrganCondition.Missing.ToString();
            else
                organFunctionConditions["Heart (or equiv)"] = heart.Condition.ToString(); 

            //liver
            if (!TryComp<ToxinFilterComponent>(uid, out var liver))
                organFunctionConditions["Liver (or equiv)"] = OrganCondition.Missing.ToString();
            else
                organFunctionConditions["Liver (or equiv)"] = liver.Condition.ToString();

            //kidneys
            if (!TryComp<ToxinRemoverComponent>(uid, out var kidneys))
                organFunctionConditions["Kidneys (or equiv)"] = OrganCondition.Missing.ToString();
            else
                organFunctionConditions["Kidneys (or equiv)"] = kidneys.Condition.ToString(); 

            return organFunctionConditions;
        }

        private void HandleActivateInWorld(EntityUid uid, HealthAnalyzerComponent healthAnalyzer, ActivateInWorldEvent args)
        {
            OpenUserInterface(args.User, healthAnalyzer);
        }

        private void OnAfterInteract(EntityUid uid, HealthAnalyzerComponent healthAnalyzer, AfterInteractEvent args)
        {
            if (healthAnalyzer.CancelToken != null)
            {
                healthAnalyzer.CancelToken.Cancel();
                healthAnalyzer.CancelToken = null;
                return;
            }

            if (args.Target == null || !args.CanReach || !HasComp<MobStateComponent>(args.Target))
                return;

            _audio.PlayPvs(healthAnalyzer.ScanningBeginSound, uid);

            _doAfterSystem.DoAfter(new DoAfterEventArgs(args.User, healthAnalyzer.ScanDelay, target: args.Target, used:uid)
            {
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
                BreakOnStun = true,
                NeedHand = true
            });
        }

        private void OnDoAfter(EntityUid uid, HealthAnalyzerComponent component, DoAfterEvent args)
        {
            if (args.Handled || args.Cancelled || args.Args.Target == null)
                return;

            _audio.PlayPvs(component.ScanningEndSound, args.Args.User);

            UpdateScannedUser(uid, args.Args.User, args.Args.Target.Value, component);
            // Below is for the traitor item
            // Piggybacking off another component's doafter is complete CBT so I gave up
            // and put it on the same component
            if (string.IsNullOrEmpty(component.Disease))
            {
                args.Handled = true;
                return;
            }

            _disease.TryAddDisease(args.Args.Target.Value, component.Disease);

            if (args.Args.User == args.Args.Target)
            {
                _popupSystem.PopupEntity(Loc.GetString("disease-scanner-gave-self", ("disease", component.Disease)),
                    args.Args.User, args.Args.User);
            }


            else
            {
                _popupSystem.PopupEntity(Loc.GetString("disease-scanner-gave-other", ("target", Identity.Entity(args.Args.Target.Value, EntityManager)),
                    ("disease", component.Disease)), args.Args.User, args.Args.User);
            }

            args.Handled = true;
        }

        private void OpenUserInterface(EntityUid user, HealthAnalyzerComponent healthAnalyzer)
        {
            if (!TryComp<ActorComponent>(user, out var actor) || healthAnalyzer.UserInterface == null)
                return;

            _uiSystem.OpenUi(healthAnalyzer.UserInterface ,actor.PlayerSession);
        }

        public void UpdateScannedUser(EntityUid uid, EntityUid user, EntityUid? target, HealthAnalyzerComponent? healthAnalyzer)
        {
            if (!Resolve(uid, ref healthAnalyzer))
                return;

            if (target == null || healthAnalyzer.UserInterface == null)
                return;

            if (!HasComp<DamageableComponent>(target))
                return;

            TryComp<TemperatureComponent>(target, out var temp);
            TryComp<BloodstreamComponent>(target, out var bloodstream);
            TryComp<SurgeryComponent>(target, out var surgery);

            var organFunctionConditions = GetOrganFunctions(target.Value);

            OpenUserInterface(user, healthAnalyzer);
            _uiSystem.SendUiMessage(healthAnalyzer.UserInterface, new HealthAnalyzerScannedUserMessage(target, temp != null ? temp.CurrentTemperature : 0, organFunctionConditions, surgery != null ? surgery.Sedated : false,
                bloodstream != null ? bloodstream.BloodSolution.FillFraction : float.NaN));
        }

        private static void OnScanCancelled(ScanCancelledEvent args)
        {
            args.HealthAnalyzer.CancelToken = null;
        }

        private sealed class ScanCancelledEvent : EntityEventArgs
        {
            public readonly HealthAnalyzerComponent HealthAnalyzer;
            public ScanCancelledEvent(HealthAnalyzerComponent healthAnalyzer)
            {
                HealthAnalyzer = healthAnalyzer;
            }
        }

        private sealed class TargetScanSuccessfulEvent : EntityEventArgs
        {
            public EntityUid User { get; }
            public EntityUid? Target { get; }
            public HealthAnalyzerComponent Component { get; }

            public TargetScanSuccessfulEvent(EntityUid user, EntityUid? target, HealthAnalyzerComponent component)
            {
                User = user;
                Target = target;
                Component = component;
            }
        }
    }
}
