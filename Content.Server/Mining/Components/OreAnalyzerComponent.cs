using System.Threading;
using Content.Server.UserInterface;
using Content.Shared.Mining;
using Robust.Server.GameObjects;

namespace Content.Server.Mining.Components
{
    /// <summary>
    ///    After scanning, retrieves the target Uid to use with its related UI.
    /// </summary>
    [RegisterComponent]
    [ComponentReference(typeof(SharedOreAnalyzerComponent))]
    public sealed class OreAnalyzerComponent : SharedOreAnalyzerComponent
    {
        /// <summary>
        /// How long it takes to scan someone.
        /// </summary>
        [DataField("scanDelay")]
        [ViewVariables]
        public float ScanDelay = 0.8f;
        /// <summary>
        ///     Token for interrupting scanning do after.
        /// </summary>
        public CancellationTokenSource? CancelToken;
        public BoundUserInterface? UserInterface => Owner.GetUIOrNull(OreAnalyzerUiKey.Key);
    }
}
