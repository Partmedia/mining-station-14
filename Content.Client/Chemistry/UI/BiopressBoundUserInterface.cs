using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.Chemistry.UI
{
    /// <summary>
    /// Initializes a <see cref="BiopressWindow"/> and updates it when new server messages are received.
    /// </summary>
    [UsedImplicitly]
    public sealed class BiopressBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        private BiopressWindow? _window;

        public BiopressBoundUserInterface(ClientUserInterfaceComponent owner, Enum uiKey) : base(owner, uiKey)
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
            _window = new BiopressWindow
            {
                Title = _entityManager.GetComponent<MetaDataComponent>(Owner.Owner).EntityName,
            };

            _window.OpenCentered();
            _window.OnClose += Close;

            // Setup static button actions.sd
            _window.OutputEjectButton.OnPressed += _ => SendMessage(
                new ItemSlotButtonPressedEvent(SharedBiopress.OutputSlotName));

            _window.BufferTransferButton.OnPressed += _ => SendMessage(
                new BiopressSetModeMessage(BiopressMode.Transfer));

            _window.BufferDiscardButton.OnPressed += _ => SendMessage(
                new BiopressSetModeMessage(BiopressMode.Discard));

            _window.ActivateButton.OnPressed += _ => SendMessage(
                new BiopressActivateButtonMessage(true));

            _window.StopButton.OnPressed += _ => SendMessage(
                new BiopressStopButtonMessage(true));

            _window.StoreToggle.OnPressed += _ => SendMessage(
                new BiopressStoreToggleButtonMessage(true));

            _window.OnBiopressReagentButtonPressed += (args, button) => SendMessage(new BiopressReagentAmountButtonMessage(button.Id, button.Amount, button.IsBuffer));
        }

        /// <summary>
        /// Update the ui each time new state data is sent from the server.
        /// </summary>
        /// <param name="state">
        /// </param>
        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            var castState = (BiopressBoundUserInterfaceState) state;

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
