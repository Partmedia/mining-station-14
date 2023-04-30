using System.Threading;
using Content.Shared.DragDrop;

namespace Content.Server.Surgery
{
    [RegisterComponent]
    [Access(typeof(SurgerySystem))]
    public sealed class SurgeryComponent : Component
    {

        public Dictionary<EntityUid, CancellationTokenSource> CancelTokens = new();
    }
}
