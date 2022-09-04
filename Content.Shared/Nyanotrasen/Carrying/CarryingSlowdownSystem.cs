using Robust.Shared.GameStates;

namespace Content.Shared.Carrying
{
    public sealed class CarryingSlowdownSystem : EntitySystem
    {

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<CarryingSlowdownComponent, ComponentGetState>(OnGetState);
            SubscribeLocalEvent<CarryingSlowdownComponent, ComponentHandleState>(OnHandleState);
        }

        public void SetModifier(EntityUid uid, float walkSpeedModifier, float sprintSpeedModifier, CarryingSlowdownComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            component.WalkModifier = walkSpeedModifier;
            component.SprintModifier = sprintSpeedModifier;
        }
        private void OnGetState(EntityUid uid, CarryingSlowdownComponent component, ref ComponentGetState args)
        {
            args.State = new CarryingSlowdownComponentState(component.WalkModifier, component.SprintModifier);
        }

        private void OnHandleState(EntityUid uid, CarryingSlowdownComponent component, ref ComponentHandleState args)
        {
            if (args.Current is CarryingSlowdownComponentState state)
            {
                component.WalkModifier = state.WalkModifier;
                component.SprintModifier = state.SprintModifier;
            }
        }
    }
}
