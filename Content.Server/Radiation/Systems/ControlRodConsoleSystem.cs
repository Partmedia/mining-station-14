using System.Linq;
using JetBrains.Annotations;
using Content.Server.MachineLinking.Components;
using Content.Server.Power.Components;
using Content.Server.MachineLinking.System;
using Content.Server.MachineLinking.Events;
using Content.Server.UserInterface;
using Content.Server.Power.EntitySystems;
using Robust.Server.GameObjects;
using Content.Shared.MachineLinking.Events;
using Content.Server.Radiation.Components;
using Content.Shared.Radiation.Components;

namespace Content.Server.Radiation.Systems
{
    [UsedImplicitly]
    public sealed class ControlRodConsoleSystem : EntitySystem
    {
        [Dependency] private readonly SignalLinkerSystem _signalSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly PowerReceiverSystem _powerReceiverSystem = default!;
        [Dependency] private readonly ControlRodSystem _controlRodSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ControlRodConsoleComponent, UiButtonPressedMessage>(OnButtonPressed);
            SubscribeLocalEvent<ControlRodConsoleComponent, AfterActivatableUIOpenEvent>(OnUIOpen);
            SubscribeLocalEvent<ControlRodConsoleComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<ControlRodConsoleComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<ControlRodConsoleComponent, NewLinkEvent>(OnNewLink);
            SubscribeLocalEvent<ControlRodConsoleComponent, PortDisconnectedEvent>(OnPortDisconnected);
            SubscribeLocalEvent<ControlRodConsoleComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        }

        private void OnButtonPressed(EntityUid uid, ControlRodConsoleComponent consoleComponent, UiButtonPressedMessage args)
        {
            if (!_powerReceiverSystem.IsPowered(uid))
                return;

            switch (args.Button)
            {
                case UiButton.Extend:
                    //extend rod
                    _controlRodSystem.ExtendRodCommand(args.Rod);
                    break;
                case UiButton.Retract:
                    //retract rod
                    _controlRodSystem.RetractRodCommand(args.Rod);
                    break;
                case UiButton.Emergency:
                    //extend all rods
                    _controlRodSystem.ExtendAllRodsCommand(consoleComponent.ControlRods);
                    break;
                case UiButton.Stop:
                    _controlRodSystem.StopRodCommand(args.Rod);
                    break;
            }
            UpdateUserInterface(consoleComponent);
        }

        private void OnPowerChanged(EntityUid uid, ControlRodConsoleComponent component, ref PowerChangedEvent args)
        {
            UpdateUserInterface(component);
        }

        private void OnMapInit(EntityUid uid, ControlRodConsoleComponent component, MapInitEvent args)
        {
            if (!TryComp<SignalTransmitterComponent>(uid, out var receiver))
                return;

            foreach (var port in receiver.Outputs.Values.SelectMany(ports => ports))
            {
                if (TryComp<ControlRodComponent>(port.Uid, out var rod))
                {
                    _signalSystem.EnsureTransmitterPorts(uid, "ControlRodSender"); //add port as we go
                    component.ControlRods.Add(port.Uid);
                    component.RodsInRange.Add(component.ControlRods.Count()-1,true);
                    if (rod.ConnectedConsole != null)
                    {
                        if (TryComp<ControlRodConsoleComponent>(rod.ConnectedConsole, out var oldConsole))
                        {
                            for (var i = 0; i < oldConsole.ControlRods.Count; i++)
                            {
                                if (oldConsole.ControlRods[i] == port.Uid)
                                {
                                    oldConsole.ControlRods.RemoveAt(i);
                                    oldConsole.RodsInRange.Remove(i);
                                    break;
                                }
                            }
                        }
                    }
                    rod.ConnectedConsole = uid;
                }
            }
        }

        private void OnNewLink(EntityUid uid, ControlRodConsoleComponent component, NewLinkEvent args)
        {

            if (TryComp<ControlRodComponent>(args.Receiver, out var rod))
            {
                _signalSystem.EnsureTransmitterPorts(uid, "ControlRodSender"); //add port as we go
                component.ControlRods.Add(args.Receiver);
                component.RodsInRange.Add(component.ControlRods.Count() - 1, true);
                if (rod.ConnectedConsole != null)
                {
                    if (TryComp<ControlRodConsoleComponent>(rod.ConnectedConsole, out var oldConsole))
                    {
                        for (var i = 0; i < oldConsole.ControlRods.Count; i++)
                        {
                            if (oldConsole.ControlRods[i] == args.Receiver)
                            {
                                oldConsole.ControlRods.RemoveAt(i);
                                oldConsole.RodsInRange.Remove(i);
                                break;
                            }
                        }
                        UpdateUserInterface(oldConsole);
                    }
                }
                rod.ConnectedConsole = uid;
            }

            RecheckConnections(uid, component.ControlRods, component);
        }

        private void OnPortDisconnected(EntityUid uid, ControlRodConsoleComponent component, PortDisconnectedEvent args)
        {
            UpdateUserInterface(component);
        }

        private void OnUIOpen(EntityUid uid, ControlRodConsoleComponent component, AfterActivatableUIOpenEvent args)
        {
            UpdateUserInterface(component);
        }

        private void OnAnchorChanged(EntityUid uid, ControlRodConsoleComponent component, ref AnchorStateChangedEvent args)
        {
            if (args.Anchored)
            {
                RecheckConnections(uid, component.ControlRods, component);
                return;
            }
            UpdateUserInterface(component);
        }

        public void UpdateUserInterface(ControlRodConsoleComponent consoleComponent)
        {
            var ui = _uiSystem.GetUiOrNull(consoleComponent.Owner, ControlRodConsoleUiKey.Key);
            if (ui == null)
                return;
            if (!_powerReceiverSystem.IsPowered(consoleComponent.Owner))
            {
                _uiSystem.CloseAll(ui);
                return;
            }

            var newState = GetUserInterfaceState(consoleComponent);
            _uiSystem.SetUiState(ui, newState);
        }


        public void RecheckConnections(EntityUid console, List<EntityUid> ControlRods, ControlRodConsoleComponent? consoleComp = null)
        {
            if (!Resolve(console, ref consoleComp))
                return;

            for (var i = 0; i < ControlRods.Count; i++)
            {
                Transform(ControlRods[i]).Coordinates.TryDistance(EntityManager, Transform((console)).Coordinates, out float distance);
                consoleComp.RodsInRange[i] = distance <= consoleComp.MaxDistance;
            }

            UpdateUserInterface(consoleComp);
        }

        private ControlRodConsoleBoundUserInterfaceState GetUserInterfaceState(ControlRodConsoleComponent consoleComponent)
        {

            List<ControlRodInfo> controlRodInfos = new List<ControlRodInfo>();

            for (var i = 0; i < consoleComponent.ControlRods.Count; i++)
            {
                if (TryComp<ControlRodComponent>(consoleComponent.ControlRods[i], out var rod))
                {
                    var controlRodInfo = new ControlRodInfo(consoleComponent.ControlRods[i], consoleComponent.RodsInRange[i], rod.CurrentExtension);
                    controlRodInfos.Add(controlRodInfo);
                }
            }

            return new ControlRodConsoleBoundUserInterfaceState(controlRodInfos);
        }

    }
}
