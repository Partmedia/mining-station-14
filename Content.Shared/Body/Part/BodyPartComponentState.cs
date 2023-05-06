using Content.Shared.Body.Organ;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Part;

[Serializable, NetSerializable]
public sealed class BodyPartComponentState : ComponentState
{
    public readonly EntityUid? Body;
    public readonly EntityUid? OriginalBody;
    public readonly BodyPartSlot? ParentSlot;
    public readonly Dictionary<string, BodyPartSlot> Children;
    public readonly Dictionary<string, OrganSlot> Organs;
    public readonly BodyPartType PartType;
    public readonly bool IsVital;
    public readonly BodyPartSymmetry Symmetry;

    public readonly EntityUid? Attachment;
    public readonly bool Container;
    public readonly bool Incisable;
    public readonly bool Incised;
    public readonly bool Opened;
    public readonly bool EndoSkeleton;
    public readonly bool ExoSkeleton;
    public readonly bool EndoOpened;
    public readonly bool ExoOpened;

    public BodyPartComponentState(
        EntityUid? body,
        EntityUid? originalBody,
        BodyPartSlot? parentSlot,
        Dictionary<string, BodyPartSlot> children,
        Dictionary<string, OrganSlot> organs,
        BodyPartType partType,
        bool isVital,
        BodyPartSymmetry symmetry,
        EntityUid? attachment,
        bool container,
        bool incisable,
        bool incised,
        bool opened,
        bool endoSkeleton,
        bool exoSkeleton,
        bool endoOpened,
        bool exoOpened)
    {
        ParentSlot = parentSlot;
        Children = children;
        Organs = organs;
        PartType = partType;
        IsVital = isVital;
        Symmetry = symmetry;
        OriginalBody = originalBody;
        Body = body;
        Attachment = attachment;
        Container = container;
        Incisable = incisable;
        Incised = incised;
        Opened = opened;
        EndoSkeleton = endoSkeleton;
        ExoSkeleton = exoSkeleton;
        EndoOpened = endoOpened;
        ExoOpened = exoOpened;
    }
}
