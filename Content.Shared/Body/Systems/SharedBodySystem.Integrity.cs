namespace Content.Shared.Body.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;

public partial class SharedBodySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        //healing
    }

    public void ChangePartIntegrity(EntityUid uid, BodyPartComponent part, FixedPoint2 damage, bool isRoot)
    {
        Logger.Debug(damage.ToString());
        Logger.Debug(part.Integrity.ToString());

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

        Logger.Debug(part.Integrity.ToString());
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
