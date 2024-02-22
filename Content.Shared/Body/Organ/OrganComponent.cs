using Content.Shared.Body.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Body.Organ;

[RegisterComponent, NetworkedComponent]
public sealed class OrganComponent : Component
{
    [DataField("body")]
    public EntityUid? Body;

    [DataField("parent")]
    public OrganSlot? ParentSlot;

    [DataField("organType")]
    public OrganType OrganType = OrganType.Other;

    [DataField("internal")]
    public bool Internal = true;

    [DataField("species", required: true)]
    public string Species = "";

    //number of times cellular damage should be dealt because of species mismatch
    [DataField("rejectionRounds")]
    public int RejectionRounds = 3;

    //number of times cellular damage has been dealt because of species mismatch - should be reset to 0 on removal
    [DataField("rejectionCounter")]
    public int RejectionCounter = 0;

    //when integrity reaches zero, remove the organ
    [DataField("integrity")]
    public float Integrity = 25;

    [DataField("maxIntegrity")]
    public float MaxIntegrity = 25;

    [DataField("healingTime")]
    public float HealingTime = 30;

    public float HealingTimer = 0;

    [DataField("selfHealingAmount")]
    public float SelfHealingAmount = 5;

    //change relative to other organs that this organ gets hit
    [DataField("hitChance")]
    public int HitChance = 1;
}
