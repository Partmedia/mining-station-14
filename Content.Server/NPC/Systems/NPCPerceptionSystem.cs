using Content.Server.NPC.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.NPC.Systems;

/// <summary>
/// Handles sight + sounds for NPCs.
/// </summary>
public sealed partial class NPCPerceptionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NPCComponent, AttackedEvent>(OnAttacked);
    }

    private void OnAttacked(EntityUid uid, NPCComponent component, AttackedEvent args)
    {
        component.Blackboard.SetValue(NPCBlackboard.LastAttacker, args.User);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateRecentlyInjected(frameTime);
    }
}
