using Content.Server.Body.Systems;

namespace Content.Server.Body.Components
{
    [RegisterComponent, Access(typeof(HeartSystem))]
    public sealed class HeartComponent : Component
    {
    }
}
