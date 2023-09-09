using Content.Shared.Body.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Part;

[Serializable, NetSerializable]
[DataRecord]
public sealed record BodyPartSlot(string Id, EntityUid Parent, BodyPartType? Type, String Species)
{
    /// <summary>
    /// the body part occupying the slot
    /// </summary>
    public EntityUid? Child { get; set; }

    /// <summary>
    /// an attached surgical tool on the body part slot (such as a Torniquet)
    /// </summary>
    public EntityUid? Attachment { get; set; }

    public bool Cauterised = false;

    /// <summary>
    /// whether or not the body part slot is the root slot
    /// for when the part should not be removed from the slot
    /// </summary>
    public bool IsRoot = false;

    // Rider doesn't suggest explicit properties during deconstruction without this
    public void Deconstruct(out EntityUid? child, out EntityUid? attachment, out string id, out EntityUid parent, out BodyPartType? type, out bool cauterised, out string species)
    {
        child = Child;
        attachment = Attachment;
        id = Id;
        parent = Parent;
        type = Type;
        cauterised = Cauterised;
        species = Species;
    }
}
