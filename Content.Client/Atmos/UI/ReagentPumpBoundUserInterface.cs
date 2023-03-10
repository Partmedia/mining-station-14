using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Dispenser;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Atmos.Piping.Unary.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.Atmos.UI
{
    /// <summary>
    /// Initializes a <see cref="ReagentPumpWindow"/> and updates it when new server messages are received.
    /// </summary>
    [UsedImplicitly]
    public sealed class ReagentPumpBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        private ReagentPumpWindow? _window;

        public ReagentPumpBoundUserInterface(ClientUserInterfaceComponent owner, Enum uiKey) : base(owner, uiKey)
        {

        }

        /// <summary>
        /// Called each time a UI instance is opened. Generates the window and fills it with
        /// relevant info. Sets the actions for static buttons.
        /// </summary>
        protected override void Open()
        {
            base.Open();

            // Setup window layout/elements
            _window = new ReagentPumpWindow
            {
                Title = _entityManager.GetComponent<MetaDataComponent>(Owner.Owner).EntityName,
            };

            _window.OpenCentered();
            _window.OnClose += Close;

            // Setup static button actions.
            _window.OutputEjectButton.OnPressed += _ => SendMessage(
                new ItemSlotButtonPressedEvent(SharedReagentPump.OutputSlotName));

            _window.BufferTransferButton.OnPressed += _ => SendMessage(
                new ReagentPumpSetModeMessage(ReagentPumpMode.Transfer));

            _window.BufferDiscardButton.OnPressed += _ => SendMessage(
                new ReagentPumpSetModeMessage(ReagentPumpMode.Discard));

            _window.InjectButton.OnPressed += _ => SendMessage(
                new InjectMessage());

            _window.ExtractButton.OnPressed += _ => SendMessage(
                new ExtractMessage());

            _window.OnReagentPumpReagentButtonPressed += (args, button) => SendMessage(new ReagentPumpReagentAmountButtonMessage(button.Id, button.Amount, button.IsBuffer));
        }

        /// <summary>
        /// Update the ui each time new state data is sent from the server.
        /// </summary>
        /// <param name="state">
        /// </param>
        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            var castState = (ReagentPumpBoundUserInterfaceState) state;

            _window?.UpdateState(castState); // Update window state
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _window?.Dispose();
            }
        }
    }
}
