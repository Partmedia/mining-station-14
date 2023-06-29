using Content.Server.GameTicking;
using Robust.Shared.Map;
using Content.Server.Mining;

namespace Content.Server.StationEvents.Events
{
    public sealed class Quake : StationEventSystem
    {

        [Dependency] private readonly MiningSystem _miningSystem = default!;

        public override string Prototype => "Quake";

        private const float ChanceOfCollapse = 0.01f;

        public override void Started()
        {
            base.Started();

        }

        public override void Ended()
        {
            base.Ended();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            //iterate through all existing mined spaces

            //roll for collapse

        }
    }
}
