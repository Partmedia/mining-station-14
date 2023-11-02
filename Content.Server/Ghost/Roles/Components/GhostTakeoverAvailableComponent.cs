using Content.Server.Database;
using Content.Server.Mind;
using Content.Server.Mind.Commands;
using Content.Server.Mind.Components;
using Content.Server.MiningCredits;
using Robust.Server.Player;

namespace Content.Server.Ghost.Roles.Components
{
    /// <summary>
    ///     Allows a ghost to take over the Owner entity.
    /// </summary>
    [RegisterComponent, ComponentReference(typeof(GhostRoleComponent))]
    public sealed class GhostTakeoverAvailableComponent : GhostRoleComponent
    {
        [Dependency] private readonly IEntityManager _entMan = default!;
        public override bool Take(IPlayerSession session)
        {
            if (Taken)
                return false;

            Taken = true;

            var mind = Owner.EnsureComponent<MindComponent>();

            var oldAttachedEntity = session.AttachedEntity;
            if (oldAttachedEntity != null)
            {
                if (_entMan.TryGetComponent<VisitingMindComponent>(oldAttachedEntity.Value, out var oldMind))
                {
                    if (oldMind.Mind is not null && oldMind.Mind.OwnedEntity != null)
                        _entMan.EventBus.RaiseLocalEvent(oldMind.Mind.OwnedEntity.Value, new MindTransferEvent(Owner, oldMind.Mind.OwnedEntity.Value));
                }
                else
                    _entMan.EventBus.RaiseLocalEvent(oldAttachedEntity.Value, new MindTransferEvent(Owner, oldAttachedEntity.Value));
            }

            if (mind.HasMind)
                return false;

            if (MakeSentient)
                MakeSentientCommand.MakeSentient(Owner, IoCManager.Resolve<IEntityManager>(), AllowMovement, AllowSpeech);

            var ghostRoleSystem = EntitySystem.Get<GhostRoleSystem>();
            ghostRoleSystem.GhostRoleInternalCreateMindAndTransfer(session, Owner, Owner, this);

            ghostRoleSystem.UnregisterGhostRole(this);

            return true;
        }
    }
}
