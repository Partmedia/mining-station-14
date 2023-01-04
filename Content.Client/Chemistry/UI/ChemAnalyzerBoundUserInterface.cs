using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.Chemistry.UI
{
    /// <summary>
    /// Initializes a <see cref="ChemAnalyzerWindow"/> and updates it when new server messages are received.
    /// </summary>
    [UsedImplicitly]
    public sealed class ChemAnalyzerBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        private ChemAnalyzerWindow? _window;
        private ChemAnalyzerBoundUserInterfaceState? _lastState;

        public ChemAnalyzerBoundUserInterface(ClientUserInterfaceComponent owner, Enum uiKey) : base(owner, uiKey)
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
            _window = new()
            {
                Title = _entityManager.GetComponent<MetaDataComponent>(Owner.Owner).EntityName,
            };

            _window.OpenCentered();
            _window.OnClose += Close;

            // Setup static button actions.
            _window.EjectButton.OnPressed += _ => SendMessage(new ItemSlotButtonPressedEvent(SharedChemAnalyzer.OutputSlotName));
        }

        /// <summary>
        /// Update the UI each time new state data is sent from the server.
        /// </summary>
        /// <param name="state">
        /// Data of the <see cref="ChemAnalyzerComponent"/> that this UI represents.
        /// Sent from the server.
        /// </param>
        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            var castState = (ChemAnalyzerBoundUserInterfaceState)state;
            _lastState = castState;

            _window?.UpdateState(castState); //Update window state
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
