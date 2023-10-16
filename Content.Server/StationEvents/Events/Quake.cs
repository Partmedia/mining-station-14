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

        private const float ChanceOfCollapse = 0.02f;

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

                if (!timedSpace.Timed)
                    continue;
                // check if the entity is anchored - prevents floating squares as a result of station drifting triggering cave-ins
                if (!Transform(uid).Anchored)
                    continue;
                if (_miningSystem.CaveInCheck(uid, timedSpace, true))
                    continue;

                {
                    var roll = (int) RobustRandom.Next(1, 100);
                    if (roll <= ChanceOfCollapse * 100
                            || GetSeverityModifier() > 1f) // debugging
                        _miningSystem.CaveIn(uid, timedSpace);
                    else
                        _replenishQueue.Enqueue(uid);
                }
            }

            _checkQueue.Clear();

            foreach (var uid in _replenishQueue)
                _miningSystem.EventQueue.Enqueue(uid);

            _replenishQueue.Clear();

            ForceEndSelf();
        }
    }
}
