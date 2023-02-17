using JetBrains.Annotations;
using Robust.Client.GameObjects;

using static Content.Shared.MedicalScanner.SharedMedicalThermometerComponent;

namespace Content.Client.MedicalThermometer.UI
{
    [UsedImplicitly]
    public sealed class MedicalThermometerBoundUserInterface : BoundUserInterface
    {
        private MedicalThermometerWindow? _window;

        public MedicalThermometerBoundUserInterface(ClientUserInterfaceComponent owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();
            _window = new MedicalThermometerWindow
            {
                Title = IoCManager.Resolve<IEntityManager>().GetComponent<MetaDataComponent>(Owner.Owner).EntityName,
            };
            _window.OnClose += Close;
            _window.OpenCentered();
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            if (_window == null)
                return;

            if (message is not MedicalThermometerScannedUserMessage cast)
                return;

            _window.Populate(cast);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            if (_window != null)
                _window.OnClose -= Close;

            _window?.Dispose();
        }
    }
}
