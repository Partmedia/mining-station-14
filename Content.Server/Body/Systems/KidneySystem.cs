using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;

namespace Content.Server.Body.Systems
{
    public sealed class KidneySystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<KidneyComponent, AddedToBodyEvent>(OnAddedToBody);
            SubscribeLocalEvent<KidneyComponent, AddedToPartEvent>((uid, _, args) => HandleToxinRemover(args.Part, uid));
            SubscribeLocalEvent<KidneyComponent, AddedToPartInBodyEvent>((uid, _, args) => HandleToxinRemover(args.Body, uid));
            SubscribeLocalEvent<KidneyComponent, RemovedFromBodyEvent>(OnRemovedFromBody);
            SubscribeLocalEvent<KidneyComponent, RemovedFromPartEvent>((uid, _, args) => HandleToxinRemover(uid, args.Old));
            SubscribeLocalEvent<KidneyComponent, RemovedFromPartInBodyEvent>((uid, _, args) => HandleToxinRemover(uid, args.OldBody));
        }

        private void OnRemovedFromBody(EntityUid uid, KidneyComponent component, RemovedFromBodyEvent args)
        {
            if (!EntityManager.TryGetComponent(uid, out OrganComponent? organ) ||
                organ.ParentSlot is not {Parent: var parent})
                return;

            HandleToxinRemover(parent, args.Old);
        }

        private void OnAddedToBody(EntityUid uid, KidneyComponent component, AddedToBodyEvent args)
        {
            if (!EntityManager.TryGetComponent(uid, out OrganComponent? organ) ||
                organ.ParentSlot is not { Parent: var parent })
                return;

            HandleToxinRemover(args.Body,parent);
        }

        private void HandleToxinRemover(EntityUid newEntity, EntityUid oldEntity)
        {

            //first check if the new entity has a toxin remover, if it does do nothing (first come first serve)
            if (TryComp<ToxinRemoverComponent>(newEntity, out var existingRemover) && !TryComp<KidneyComponent>(newEntity, out var existingKidney))
                return;

            //check if the new organ has a toxin remover (be a pretty bad kideny if it didn't)
            if (!TryComp<ToxinRemoverComponent>(oldEntity, out var toxinRemover))
                return;

            //next, add the toxin remover to the entity
            var newToxinRemover = EntityManager.EnsureComponent<ToxinRemoverComponent>(newEntity);
            //carry over properties of old to new
            newToxinRemover.ToxinRemovalRate = toxinRemover.ToxinRemovalRate;

            //if the old ToxinRemover is NOT embedded, remove the component
            if (!toxinRemover.Embedded)
                RemComp<ToxinRemoverComponent>(oldEntity);
            
        }
    }
}

