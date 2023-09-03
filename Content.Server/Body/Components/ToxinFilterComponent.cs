using Content.Server.Body.Systems;

namespace Content.Server.Body.Components
{
    [RegisterComponent]
    public sealed class ToxinFilterComponent : Component
    {
        //if the ToxinFilter is embedded, do not remove it from its host whenever an organ is removed
        [DataField("embedded")]
        public bool Embedded = false;
    }
}
