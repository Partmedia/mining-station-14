using Content.Server.GameTicking;
using Robust.Shared.Map;
using Content.Server.Mining;
using Robust.Shared.Audio;
using Robust.Shared.Random;
using Robust.Shared.Player;

namespace Content.Server.StationEvents.Events
{
    public sealed class Quake : StationEventSystem
    {

        [Dependency] private readonly MiningSystem _miningSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;

        public override string Prototype => "Quake";

        private const float ChanceOfCollapse = 0.01f;

        public override void Started()
        {
            base.Started();
            SoundSystem.Play("/Audio/Effects/ominous_quake.ogg", Filter.Broadcast(), AudioParams.Default.WithVolume(-2f));

        }

        public override void Ended()
        {
            base.Ended();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!RuleStarted)
                return;

            Queue<EntityUid> _checkQueue = new Queue<EntityUid>(_miningSystem.EventQueue);
            Queue<EntityUid> _replenishQueue = new();

            _miningSystem.EventQueue.Clear();

            //iterate through all existing mined spaces
            foreach (var uid in _checkQueue)
            {
                if (!TryComp<CaveInComponent>(uid, out var timedSpace))
                    continue;

            //roll for collapse

            ForceEndSelf();
        }
    }
}
