using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;

namespace Content.Server.Body.Systems
{
    public sealed class HeartSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly CirculatoryPumpSystem _pumpSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<HeartComponent, AddedToBodyEvent>(OnAddedToBody);
            SubscribeLocalEvent<HeartComponent, AddedToPartEvent>((uid, _, args) => HandleCirculatoryPump(args.Part, uid));
            SubscribeLocalEvent<HeartComponent, AddedToPartInBodyEvent>((uid, _, args) => HandleCirculatoryPump(args.Body, uid));
            SubscribeLocalEvent<HeartComponent, RemovedFromBodyEvent>(OnRemovedFromBody);
            SubscribeLocalEvent<HeartComponent, RemovedFromPartEvent>((uid, _, args) => HandleCirculatoryPump(uid, args.Old));
            SubscribeLocalEvent<HeartComponent, RemovedFromPartInBodyEvent>((uid, _, args) => HandleCirculatoryPump(uid, args.OldBody));
        }

        private void OnRemovedFromBody(EntityUid uid, HeartComponent component, RemovedFromBodyEvent args)
        {
            if (!EntityManager.TryGetComponent(uid, out OrganComponent? organ) ||
                organ.ParentSlot is not {Parent: var parent})
                return;

            HandleCirculatoryPump(parent, args.Old);
        }

        private void OnAddedToBody(EntityUid uid, HeartComponent component, AddedToBodyEvent args)
        {
            if (!EntityManager.TryGetComponent(uid, out OrganComponent? organ) ||
                organ.ParentSlot is not { Parent: var parent })
                return;

            HandleCirculatoryPump(args.Body,parent);
        }

        private void HandleCirculatoryPump(EntityUid newEntity, EntityUid oldEntity)
        {

            //first check if the new entity has a pump, if it does do nothing (first come first serve) TODO - allow for multiple hearts?
            if (TryComp<CirculatoryPumpComponent>(newEntity, out var existingFilter) && !TryComp<HeartComponent>(newEntity, out var existingHeart))
                return;

            //check if the new organ has a pump (be a pretty bad heart if it didn't)
            if (!TryComp<CirculatoryPumpComponent>(oldEntity, out var pump))
                return;

            //next, add the pump to the entity
            var newCirculatoryPump = EntityManager.EnsureComponent<CirculatoryPumpComponent>(newEntity);
            newCirculatoryPump.NotWorkingDamage = pump.NotWorkingDamage;

            //if the old CirculatoryPump is NOT embedded, remove the component
            if (!pump.Embedded)
                RemComp<CirculatoryPumpComponent>(oldEntity);

            //if we just placed this in a body, kickstart the heart TODO consider removing once defibs are ported (or not?)
            if (TryComp<BodyComponent>(newEntity, out var body))
                _pumpSystem.StartPump(newEntity, pump);
        }
    }
}

