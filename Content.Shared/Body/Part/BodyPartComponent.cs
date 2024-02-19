using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Robust.Shared.GameStates;
using System.ComponentModel.DataAnnotations;

namespace Content.Shared.Body.Part;

[RegisterComponent, NetworkedComponent]
public sealed class BodyPartComponent : Component
{
    [DataField("body")]
    public EntityUid? Body;

    [DataField("originalBody")]
    public EntityUid? OriginalBody;

    [DataField("parent")]
    public BodyPartSlot? ParentSlot;

    [DataField("children")]
    public Dictionary<string, BodyPartSlot> Children = new();

    [DataField("organs")]
    public Dictionary<string, OrganSlot> Organs = new();

    [DataField("partType")]
    public BodyPartType PartType = BodyPartType.Other;

    [DataField("species", required: true)]
    public string Species = "";

    //number of times cellular damage should be dealt because of species mismatch
    [DataField("rejectionRounds")]
    public int RejectionRounds = 3;

    //number of times cellular damage has been dealt because of species mismatch - should be reset to 0 on removal
    [DataField("rejectionCounter")]
    public int RejectionCounter = 0;

    // TODO BODY Replace with a simulation of organs
    /// <summary>
    ///     Whether or not the owning <see cref="Body"/> will die if all
    ///     <see cref="BodyComponent"/>s of this type are removed from it.
    /// </summary>
    [DataField("vital")]
    public bool IsVital;

    //TODO consider moving to or duplicating for slots
    [DataField("symmetry")]
    public BodyPartSymmetry Symmetry = BodyPartSymmetry.None;

    /// <summary>
    /// an attached surgical tool on the body part (such as a retractor)
    /// </summary>
    [DataField("attachment")]
    public EntityUid? Attachment { get; set; }

    [DataField("container")]
    public bool Container = false;

    [DataField("incisable")]
    public bool Incisable = false; //can this part be cut open?

    [ViewVariables]
    public bool Incised = false; //whether or not an incision has been made

    [ViewVariables]
    public bool Opened = false; //whether or not the body part has been opened up (any obstructing endoskeleton not yet factored)

    /// <summary>
    /// though a part may be opened, the organs may be behind bones!
    /// instead it may have an exoskeleton (see below) or no skeleton at all (slimes for example)!
    /// </summary>
    [DataField("endoSkeleton")]
    public bool EndoSkeleton = false;

    /// <summary>
    /// but what if its skelly is on the outside?
    /// if the part has an exoskeleton it must be opened prior to any incision
    /// </summary>
    [DataField("exoSkeleton")]
    public bool ExoSkeleton = false;

    [ViewVariables]
    public bool EndoOpened = false; //gotta get through the bones (if they have them)

    [ViewVariables]
    public bool ExoOpened = false; //gotta get through the shell or whatever it is (if they have them)

    //when integrity reaches zero, the part will eject from its slot
    [DataField("integrity")]
    public float Integrity = 100;

    [DataField("maxIntegrity")]
    public float MaxIntegrity = 100;

    [DataField("healingTime")]
    public float HealingTime = 30;

    public float HealingTimer = 0;

    [DataField("selfHealingAmount")]
    public float SelfHealingAmount = 5;

    //change relative to other part that this part gets hit
    [DataField("hitChance")]
    public int HitChance = 1;
}
