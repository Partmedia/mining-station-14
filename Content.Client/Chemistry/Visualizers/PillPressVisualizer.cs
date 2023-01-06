using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using static Content.Shared.Chemistry.SharedPillPress;

namespace Content.Client.Chemistry.Visualizers
{
    public sealed class PillPressVisualizer : AppearanceVisualizer
    {
        [Obsolete("Subscribe to AppearanceChangeEvent instead.")] //sooorry
        public override void OnChangeData(AppearanceComponent component)
        {
            base.OnChangeData(component);
            var sprite = IoCManager.Resolve<IEntityManager>().GetComponent<SpriteComponent>(component.Owner);

            component.TryGetData(PillPressVisualState.BeakerAttached, out bool hasBeaker);
            component.TryGetData(PillPressVisualState.OutputAttached, out bool hasOutput);

            if (hasBeaker && hasOutput)
                sprite.LayerSetState(0, $"pillPress3");
            else if (hasBeaker)
                sprite.LayerSetState(0, $"pillPress1");
            else if (hasOutput)
                sprite.LayerSetState(0, $"pillPress2");
            else
                sprite.LayerSetState(0, $"pillPress0");
        }
    }
}
