namespace Content.Shared.Body.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;

public partial class SharedBodySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        //healing
        foreach (var part in EntityManager.EntityQuery<BodyPartComponent>(false))
        {
            part.HealingTimer += frameTime;

            if (part.HealingTimer >= part.HealingTime)
            {
                part.HealingTimer = 0;
                if (part.Integrity < part.MaxIntegrity && part.ParentSlot is not null && part.Body is not null && !_mobState.IsDead(part.Body.Value))
                {
                    if (part.Integrity + part.SelfHealingAmount > part.MaxIntegrity)
                        part.Integrity = part.MaxIntegrity;
                    else
                        part.Integrity += part.SelfHealingAmount;
                }
            }
                    
        }

        foreach (var organ in EntityManager.EntityQuery<OrganComponent>(false))
        {
            organ.HealingTimer += frameTime;

            if (organ.HealingTimer >= organ.HealingTime)
            {
                organ.HealingTimer = 0;
                if (organ.Integrity < organ.MaxIntegrity && organ.ParentSlot is not null && organ.Body is not null && !_mobState.IsDead(organ.Body.Value))
                {
                    if (organ.Integrity + organ.SelfHealingAmount > organ.MaxIntegrity)
                        organ.Integrity = organ.MaxIntegrity;
                    else
                        organ.Integrity += organ.SelfHealingAmount;
                }
            }
        }
    }

    public void ChangePartIntegrity(EntityUid uid, BodyPartComponent part, FixedPoint2 damage, bool isRoot)
    {
        if (part.Integrity - damage <= 0)
        {
            part.Integrity = 0;

            //remove part from body (unless it is root)
            if (!isRoot)
            {
                DropPart(uid, part);
            }

        } else
        {
            part.Integrity -= (float) damage;
        }
    }

    public void ChangeOrganIntegrity(EntityUid uid, OrganComponent organ, FixedPoint2 damage)
    {
        if (organ.Integrity - damage <= 0)
        {
            organ.Integrity = 0;

            //destroy organ
            DeleteOrgan(uid,organ);
        }
        else
        {
            organ.Integrity -= (float) damage;
        }
    }
}
