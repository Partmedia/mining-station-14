using Content.Server.Body.Systems;

namespace Content.Server.Body.Components
{
    [RegisterComponent, Access(typeof(EyeSightSystem))]
    public sealed class EyeSightComponent : Component //TODO rename this to EyeComponent when/if the name is not used by the engine...
    {
    }
}
