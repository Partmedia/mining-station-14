namespace Content.Shared.Administration;

public interface IAutoAdmin
{
    public void CheckCombat(EntityUid attacker, EntityUid attacked);
}

/**
 * Shell no-op auto admin hooks so that auto-admin can be called from shared code.
 * This gets registered by the client IoC. For the server-side code, see AutoAdmin.
 */

[Virtual]
public class SharedAutoAdmin : IAutoAdmin
{
    public virtual void CheckCombat(EntityUid attacker, EntityUid attacked)
    {
    }
}
