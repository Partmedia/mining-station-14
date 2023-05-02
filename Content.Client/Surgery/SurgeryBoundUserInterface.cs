using Content.Shared.Surgery;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.Surgery
{
    /// <summary>
    /// Initializes a <see cref="SurgeryWindow"/> and updates it when new server messages are received.
    /// </summary>
    [UsedImplicitly]
    public sealed class SurgeryBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        private SurgeryWindow? _window;

        public SurgeryBoundUserInterface(ClientUserInterfaceComponent owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        /// <summary>
        ///
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

            // Setup surgery slot button actions.
            _window.OnSurgerySlotButtonPressed += (args, button) => SendMessage(new SurgerySlotButtonPressed(button.Slot));
            
        }

        /// <summary>
        /// Update the UI each time new state data is sent from the server.
        /// </summary>
        /// <param name="state">
        /// Data of the <see cref="SurgeryComponent"/> that this UI represents.
        /// Sent from the server.
        /// </param>
        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            var castState = (SurgeryBoundUserInterfaceState)state;

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
