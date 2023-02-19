using System.Threading;

namespace Content.Shared.Storage.Components
{
    /// <summary>
    /// Lets you dump this container on the ground using a verb,
    /// or when interacting with it on a disposal unit or placeable surface.
    /// </summary>
    [RegisterComponent]
    public sealed class DumpableComponent : Component
    {
        /// <summary>
        /// Time it takes to dump the whole bag.
        /// </summary>
        [DataField("delay")]
        public TimeSpan Delay = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Cancellation token for the doafter.
        /// </summary>
        public CancellationTokenSource? CancelToken;
    }
}
