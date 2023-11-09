using System.Linq;
using System.Threading;
using Content.Server.DoAfter;
using Content.Server.Popups;
//using Content.Shared.IdentityManagement;
using Content.Shared.Chemistry;
using Content.Server.Chemistry.Components.SolutionManager;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Content.Server.Mining.Components;
using static Content.Shared.Mining.SharedOreAnalyzerComponent;

namespace Content.Server.Mining
{
    public sealed class OreAnalyzerSystem : EntitySystem
    {
        [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly TagSystem _tagSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<OreAnalyzerComponent, ActivateInWorldEvent>(HandleActivateInWorld);
            SubscribeLocalEvent<OreAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<TargetScanSuccessfulEvent>(OnTargetScanSuccessful);
            SubscribeLocalEvent<ScanCancelledEvent>(OnScanCancelled);
        }

        private void HandleActivateInWorld(EntityUid uid, OreAnalyzerComponent oreAnalyzer, ActivateInWorldEvent args)
        {
            OpenUserInterface(args.User, oreAnalyzer);
        }

        private void OnAfterInteract(EntityUid uid, OreAnalyzerComponent oreAnalyzer, AfterInteractEvent args)
        {
            if (oreAnalyzer.CancelToken != null)
            {
                oreAnalyzer.CancelToken.Cancel();
                oreAnalyzer.CancelToken = null;
                return;
            }

            if (args.Target == null)
                return;

            if (!args.CanReach)
                return;

            if (oreAnalyzer.CancelToken != null)
                return;

            oreAnalyzer.CancelToken = new CancellationTokenSource();
            _doAfterSystem.DoAfter(new DoAfterEventArgs(args.User, oreAnalyzer.ScanDelay, oreAnalyzer.CancelToken.Token, target: args.Target)
            {
                BroadcastFinishedEvent = new TargetScanSuccessfulEvent(args.User, args.Target, oreAnalyzer),
                BroadcastCancelledEvent = new ScanCancelledEvent(oreAnalyzer),
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

        private void OpenUserInterface(EntityUid user, OreAnalyzerComponent oreAnalyzer)
        {
            if (!TryComp<ActorComponent>(user, out var actor))
                return;

            oreAnalyzer.UserInterface?.Open(actor.PlayerSession);
        }

        public void UpdateScannedUser(EntityUid uid, EntityUid user, EntityUid? target, OreAnalyzerComponent? oreAnalyzer)
        {
            if (!Resolve(uid, ref oreAnalyzer))
                return;

            if (target == null || oreAnalyzer.UserInterface == null)
                return;

            OpenUserInterface(user, oreAnalyzer);
            var containerInfo = BuildContainerInfo(target);
            oreAnalyzer.UserInterface?.SendMessage(new OreAnalyzerScannedUserMessage(target,containerInfo));
        }

        private ContainerInfo? BuildContainerInfo(EntityUid? container)
        {
            if (container is not { Valid: true })
                return null;

            if (TryComp<SolutionContainerManagerComponent>(container, out var solutions))
                foreach (var solution in (solutions.Solutions)) //will only work on the first iter val
                {
                    var reagents = solution.Value.Contents.Select(reagent => (reagent.ReagentId, reagent.Quantity)).ToList();
                    return new ContainerInfo(Name(container.Value), true, solution.Value.Volume, solution.Value.MaxVolume, reagents);
                }

            return null;
        }

        private static void OnScanCancelled(ScanCancelledEvent args)
        {
            args.OreAnalyzer.CancelToken = null;
        }

        private sealed class ScanCancelledEvent : EntityEventArgs
        {
            public readonly OreAnalyzerComponent OreAnalyzer;
            public ScanCancelledEvent(OreAnalyzerComponent oreAnalyzer)
            {
                OreAnalyzer = oreAnalyzer;
            }
        }

        private sealed class TargetScanSuccessfulEvent : EntityEventArgs
        {
            public EntityUid User { get; }
            public EntityUid? Target { get; }
            public OreAnalyzerComponent Component { get; }

            public TargetScanSuccessfulEvent(EntityUid user, EntityUid? target, OreAnalyzerComponent component)
            {
                User = user;
                Target = target;
                Component = component;
            }
        }
    }
}
