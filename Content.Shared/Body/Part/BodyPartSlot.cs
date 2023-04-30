using Content.Shared.Body.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Part;

[Serializable, NetSerializable]
[Access(typeof(SharedBodySystem))]
[DataRecord]
public sealed record BodyPartSlot(string Id, EntityUid Parent, BodyPartType? Type)
{
    /// <summary>
    /// the body part occupying the slot
    /// </summary>
    public EntityUid? Child { get; set; }

    /// <summary>
    /// an attached surgical tool on the body part slot (such as a Hemostat)
    /// </summary>
    public EntityUid? Attachment { get; set; }

    public bool Cauterised = false;

    public float BaseSurgeryTime = 10f;

    // Rider doesn't suggest explicit properties during deconstruction without this
    public void Deconstruct(out EntityUid? child, out EntityUid? attachment, out string id, out EntityUid parent, out BodyPartType? type, out bool cauterised)
    {
        child = Child;
        attachment = Attachment;
        id = Id;
        parent = Parent;
        type = Type;
        cauterised = Cauterised;
    }
}
