using Content.Server.Body.Systems;

namespace Content.Server.Body.Components
{
    [RegisterComponent]
    public sealed class ToxinRemoverComponent : Component
    {
        [DataField("toxinRemovalRate")]
        public float ToxinRemovalRate = 1.0f;

        //if the ToxinRemover is embedded, do not remove it from its host whenever an organ is removed
        [DataField("embedded")]
        public bool Embedded = false;
    }
}
