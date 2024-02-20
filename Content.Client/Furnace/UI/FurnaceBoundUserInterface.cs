using Content.Shared.Mining.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.Furnace.UI
{
    /// <summary>
    /// Initializes a <see cref="FurnaceWindow"/> and updates it when new server messages are received.
    /// </summary>
    [UsedImplicitly]
    public sealed class FurnaceBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        private FurnaceWindow? _window;

        public FurnaceBoundUserInterface(ClientUserInterfaceComponent owner, Enum uiKey) : base(owner, uiKey)
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
            _window = new FurnaceWindow{};

            _window.OpenCentered();
            _window.OnClose += Close;

            // Setup static button actions.
            _window.DoorButton.OnPressed += _ => SendMessage(
                new FurnaceStoreToggleButtonMessage());
            _window.SpigotButton.OnPressed += _ => SendMessage(
                new FurnacePourButtonMessage());

            _window.TargetPower.OnValueChanged += (args) =>
            {
                SendMessage(new SetTargetPowerMessage(args.Value));
            };
        }

        /// <summary>
        /// Update the ui each time new state data is sent from the server.
        /// </summary>
        /// <param name="state">
        /// </param>
        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            var castState = (FurnaceBoundUserInterfaceState) state;

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
