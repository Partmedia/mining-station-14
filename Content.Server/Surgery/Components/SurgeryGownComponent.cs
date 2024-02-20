
using System.ComponentModel.DataAnnotations;

namespace Content.Server.Surgery
{
    [RegisterComponent]
    [Access(typeof(SurgerySystem))]
    public sealed class SurgeryGownComponent : Component
    {
    }
}
