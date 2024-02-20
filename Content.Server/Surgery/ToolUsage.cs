using Content.Server.Surgery;
using Robust.Shared.Serialization;

namespace Content.Server.Surgery
{

    [Serializable]
    public enum ToolUsage
    {
        Incisor = 0,
        SmallClamp,
        LargeClamp,
        Saw,
        Drill,
        Suture,
        HardSuture,
        Cauterizer,
        Manipulator,
        Retractor
    }
}
