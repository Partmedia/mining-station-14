using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Eye.Blinding;
using Content.Server.Surgery;

namespace Content.Server.Body.Systems
{
    public sealed class EyeSightSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly SurgerySystem _surgerySystem = default!;
        [Dependency] private readonly SharedBlindingSystem _sharedBlindingSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<EyeSightComponent, AddedToBodyEvent>(OnAddedToBody);
            SubscribeLocalEvent<EyeSightComponent, AddedToPartEvent>((uid, _, args) => HandleSight(args.Part, uid));
            SubscribeLocalEvent<EyeSightComponent, AddedToPartInBodyEvent>((uid, _, args) => HandleSight(args.Body, uid));
            SubscribeLocalEvent<EyeSightComponent, RemovedFromBodyEvent>(OnRemovedFromBody);
            SubscribeLocalEvent<EyeSightComponent, RemovedFromPartEvent>((uid, _, args) => HandleSight(uid, args.Old));
            SubscribeLocalEvent<EyeSightComponent, RemovedFromPartInBodyEvent>((uid, _, args) => HandleSight(uid, args.OldBody));
        }

        private void OnRemovedFromBody(EntityUid uid, EyeSightComponent component, RemovedFromBodyEvent args)
        {
            if (!EntityManager.TryGetComponent(uid, out OrganComponent? organ) ||
                organ.ParentSlot is not {Parent: var parent})
                return;

            HandleSight(parent, args.Old);
        }

        private void OnAddedToBody(EntityUid uid, EyeSightComponent component, AddedToBodyEvent args)
        {
            if (!EntityManager.TryGetComponent(uid, out OrganComponent? organ) ||
                organ.ParentSlot is not { Parent: var parent })
                return;

            HandleSight(args.Body,parent);
        }

        private void HandleSight(EntityUid newEntity, EntityUid oldEntity)
        {
            //transfer existing component to organ
            var newSight = EntityManager.EnsureComponent<BlindableComponent>(newEntity);
            var oldSight = EntityManager.EnsureComponent<BlindableComponent>(oldEntity);

            //give new sight all values of old sight
            _sharedBlindingSystem.TransferBlindness(newSight, oldSight);
            _sharedBlindingSystem.AdjustEyeDamage(newEntity, 0, newSight);
            _sharedBlindingSystem.AdjustBlindSources(newEntity, 0, newSight);

            var hasOtherEyes = false;
            //check for other eye components on owning body and owning body organs (if old entity has a body)
            if (TryComp<BodyComponent>(oldEntity, out var body)) {
                if (TryComp<EyeSightComponent>(oldEntity, out var bodyEyes)) //some bodies see through their skin!!! (slimes)
                    hasOtherEyes = true;
                else {
                    var organs = _surgerySystem.GetAllBodyOrgans(oldEntity);
                    foreach (var organ in organs) {
                        if (TryComp<EyeSightComponent>(organ, out var eyes))
                        {
                            hasOtherEyes = true;
                            break;
                        }
                    }
                    //TODO should we do this for body parts too? might be a little overpowered but could be funny/interesting
                }
            }

            //if there are no existing eye components for the old entity - set old sight to be blind otherwise leave it as is
            if (!hasOtherEyes && !TryComp<EyeSightComponent>(oldEntity, out var self))
                _sharedBlindingSystem.AdjustEyeDamage(oldEntity, oldSight.MaxDamage, oldSight);
            
        }
    }
}

