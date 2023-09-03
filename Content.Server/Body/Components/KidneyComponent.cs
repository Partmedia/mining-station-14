using Content.Server.Body.Systems;

namespace Content.Server.Body.Components
{
    [RegisterComponent, Access(typeof(KidneySystem))]
    public sealed class KidneyComponent : Component
    {
    }
}
