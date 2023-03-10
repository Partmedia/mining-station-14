using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Dispenser;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.Chemistry.UI
{
    /// <summary>
    /// Initializes a <see cref="PillPressWindow"/> and updates it when new server messages are received.
    /// </summary>
    [UsedImplicitly]
    public sealed class PillPressBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        private PillPressWindow? _window;

        public PillPressBoundUserInterface(ClientUserInterfaceComponent owner, Enum uiKey) : base(owner, uiKey)
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
            _window = new PillPressWindow
            {
                Title = _entityManager.GetComponent<MetaDataComponent>(Owner.Owner).EntityName,
            };

            _window.OpenCentered();
            _window.OnClose += Close;

            // Setup static button actions.
            _window.InputEjectButton.OnPressed += _ => SendMessage(
                new ItemSlotButtonPressedEvent(SharedPillPress.InputSlotName));
            _window.OutputEjectButton.OnPressed += _ => SendMessage(
                new ItemSlotButtonPressedEvent(SharedPillPress.OutputSlotName));
            
            _window.CreatePillButton.OnPressed += _ => SendMessage(
                new PillPressCreatePillsMessage(
                    (uint)_window.PillDosage.Value, (uint)_window.PillNumber.Value, _window.LabelLine));

            for (uint i = 0; i < _window.PillTypeButtons.Length; i++)
            {
                var pillType = i;
                _window.PillTypeButtons[i].OnPressed += _ => SendMessage(new PillPressSetPillTypeMessage(pillType));
            }
        }

        /// <summary>
        /// Update the ui each time new state data is sent from the server.
        /// </summary>
        /// <param name="state">
        /// </param>
        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            var castState = (PillPressBoundUserInterfaceState) state;

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
