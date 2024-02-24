using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

public sealed class SwitchToAttackerOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)>
        Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        // Did someone attack us?
        if (blackboard.TryGetValue<EntityUid>(NPCBlackboard.LastAttacker, out var attacker, _entityManager))
        {
            // If we have a current target...
            if (blackboard.TryGetValue<EntityUid>("CombatTarget", out var target, _entityManager))
            {
                // ...don't switch if the target is the same as the attacker.
                if (target == attacker)
                {
                    return (false, null);
                }
            }
            var effects = new Dictionary<string, object>()
            {
                {"CombatTarget", attacker}
            };
            return (true, effects);
        }
        return (false, null);
    }


    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (blackboard.TryGetValue<EntityUid>(NPCBlackboard.LastAttacker, out var attacker, _entityManager))
        {
            blackboard.SetValue("CombatTarget", attacker);
        }
        blackboard.Remove<EntityUid>(NPCBlackboard.LastAttacker);
        return HTNOperatorStatus.Finished;
    }
}
