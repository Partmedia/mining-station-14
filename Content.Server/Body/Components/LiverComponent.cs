using Content.Server.Body.Systems;

namespace Content.Server.Body.Components
{
    [RegisterComponent, Access(typeof(LiverSystem))]
    public sealed class LiverComponent : Component
    {
    }
}
