using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;

namespace Content.Server.Body.Systems
{
    public sealed class LiverSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<LiverComponent, AddedToBodyEvent>(OnAddedToBody);
            SubscribeLocalEvent<LiverComponent, AddedToPartEvent>((uid, _, args) => HandleToxinFilter(args.Part, uid));
            SubscribeLocalEvent<LiverComponent, AddedToPartInBodyEvent>((uid, _, args) => HandleToxinFilter(args.Body, uid));
            SubscribeLocalEvent<LiverComponent, RemovedFromBodyEvent>(OnRemovedFromBody);
            SubscribeLocalEvent<LiverComponent, RemovedFromPartEvent>((uid, _, args) => HandleToxinFilter(uid, args.Old));
            SubscribeLocalEvent<LiverComponent, RemovedFromPartInBodyEvent>((uid, _, args) => HandleToxinFilter(uid, args.OldBody));
        }

        private void OnRemovedFromBody(EntityUid uid, LiverComponent component, RemovedFromBodyEvent args)
        {
            if (!EntityManager.TryGetComponent(uid, out OrganComponent? organ) ||
                organ.ParentSlot is not {Parent: var parent})
                return;

            HandleToxinFilter(parent, args.Old);
        }

        private void OnAddedToBody(EntityUid uid, LiverComponent component, AddedToBodyEvent args)
        {
            if (!EntityManager.TryGetComponent(uid, out OrganComponent? organ) ||
                organ.ParentSlot is not { Parent: var parent })
                return;

            HandleToxinFilter(args.Body,parent);
        }

        private void HandleToxinFilter(EntityUid newEntity, EntityUid oldEntity)
        {

            //first check if the new entity has a toxin filter, if it does do nothing (first come first serve)
            if (TryComp<ToxinFilterComponent>(newEntity, out var existingFilter) && !TryComp<LiverComponent>(newEntity, out var existingLiver))
                return;

            //check if the new organ has a toxin filter (be a pretty bad liver if it didn't)
            if (!TryComp<ToxinFilterComponent>(oldEntity, out var toxinFilter))
                return;

            //next, add the toxin filter to the entity
            var newToxinFilter = EntityManager.EnsureComponent<ToxinFilterComponent>(newEntity);
            //if the ToxinFilter ever gets any more properties, ensure those properties are transferred here (except embedded which must be false (and is by default))

            //if the old ToxinFilter is NOT embedded, remove the component
            if (!toxinFilter.Embedded)
                RemComp<ToxinFilterComponent>(oldEntity);

        }
    }
}

