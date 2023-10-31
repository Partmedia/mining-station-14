using Content.Server.Body.Components;
using Content.Server.Ghost.Components;
using Content.Server.Mind.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Standing;
using Content.Shared.Movement.Components;

namespace Content.Server.Body.Systems
{
    public sealed class BrainSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly CirculatoryPumpSystem _pumpSystem = default!;
        [Dependency] private readonly StandingStateSystem _standingSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<BrainComponent, AddedToBodyEvent>(OnAddedToBody);
            SubscribeLocalEvent<BrainComponent, AddedToPartEvent>((uid, _, args) => HandleMind(args.Part, uid));
            SubscribeLocalEvent<BrainComponent, AddedToPartInBodyEvent>((uid, _, args) => HandleMind(args.Body, uid));
            SubscribeLocalEvent<BrainComponent, RemovedFromBodyEvent>(OnRemovedFromBody);
            SubscribeLocalEvent<BrainComponent, RemovedFromPartEvent>((uid, _, args) => HandleMind(uid, args.Old));
            SubscribeLocalEvent<BrainComponent, RemovedFromPartInBodyEvent>((uid, _, args) => HandleMind(uid, args.OldBody));
        }

        private void OnRemovedFromBody(EntityUid uid, BrainComponent component, RemovedFromBodyEvent args)
        {
            // This one needs to be special, okay?
            if (!EntityManager.TryGetComponent(uid, out OrganComponent? organ) ||
                organ.ParentSlot is not {Parent: var parent})
                return;

            HandleMind(parent, args.Old);
        }

        private void OnAddedToBody(EntityUid uid, BrainComponent component, AddedToBodyEvent args)
        {
            // This one needs to be special, okay?
            if (!EntityManager.TryGetComponent(uid, out OrganComponent? organ) ||
                organ.ParentSlot is not { Parent: var parent })
                return;

            HandleMind(args.Body,parent);
        }

        private void HandleMind(EntityUid newEntity, EntityUid oldEntity)
        {
            EntityManager.EnsureComponent<MindComponent>(newEntity);
            var oldMind = EntityManager.EnsureComponent<MindComponent>(oldEntity);

            EnsureComp<GhostOnMoveComponent>(newEntity);
            if (HasComp<BodyComponent>(newEntity))
            {
                Comp<GhostOnMoveComponent>(newEntity).MustBeDead = true;
                _standingSystem.Stand(newEntity);
            }

            if (HasComp<BodyComponent>(oldEntity))
                _standingSystem.Down(oldEntity);

            // TODO: This is an awful solution.
            EnsureComp<InputMoverComponent>(newEntity);

            oldMind.Mind?.TransferTo(newEntity);

            //TODO if a mind already exists in the new entity, transfer it to any brain (or brain container) that it can go to

            //if the new entity has a circulatory pump system, set it to working - TODO remove once defibs are ported (or we keep this as an alternate jump start? lets see how we go.)
            if (TryComp<CirculatoryPumpComponent>(newEntity, out var pump))
                _pumpSystem.StartPump(newEntity, pump);
        }
    }
}

