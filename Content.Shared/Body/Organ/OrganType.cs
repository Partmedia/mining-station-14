using Content.Shared.Body.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Organ
{

    [Serializable, NetSerializable]
    public enum OrganType
    {
        Other = 0,
        FreeSpace, //non-organs can be placed here, organs placed here will not function
        Eye,
        Brain,
        Lung,
        Heart,
        Stomach,
        Kidney,
        Liver
    }
}
