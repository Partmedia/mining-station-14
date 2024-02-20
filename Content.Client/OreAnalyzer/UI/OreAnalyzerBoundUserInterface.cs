using JetBrains.Annotations;
using Robust.Client.GameObjects;

using static Content.Shared.Mining.Components.SharedOreAnalyzerComponent;

namespace Content.Client.OreAnalyzer.UI
{
    [UsedImplicitly]
    public sealed class OreAnalyzerBoundUserInterface : BoundUserInterface
    {
        private OreAnalyzerWindow? _window;

        public OreAnalyzerBoundUserInterface(ClientUserInterfaceComponent owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();
            _window = new OreAnalyzerWindow
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

            if (message is not OreAnalyzerScannedUserMessage cast)
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
