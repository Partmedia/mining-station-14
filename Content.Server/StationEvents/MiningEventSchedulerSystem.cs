using System.Linq;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Configurations;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.StationEvents
{
    [UsedImplicitly]
    public sealed class MiningEventScheduler : GameRuleSystem
    {
        public override string Prototype => "MiningEventScheduler";

        [Dependency] private readonly IPrototypeManager _prototype = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly EventManagerSystem _event = default!;

        /// <summary>
        /// How long until the next check for an event runs
        /// </summary>
        /// Default value is how long until first event is allowed
        [ViewVariables(VVAccess.ReadWrite)]
        private float _timeUntilNextEvent;

        public override void Started()
        {
            ResetTimer();
        }

        public override void Ended()
        {
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!RuleStarted || !_event.EventsEnabled)
                return;

            if (_timeUntilNextEvent > 0)
            {
                _timeUntilNextEvent -= frameTime;
                return;
            }

            string result = RunMiningEvent();
            Logger.Debug(result);
            ResetTimer();
        }

        /// <summary>
        /// Randomly runs a valid event.
        /// </summary>
        public string RunMiningEvent()
        {
            List<string> events = new List<string>{"MeteorSwarm"};
            string randomEvent = _random.Pick(events);
            if (!_prototype.TryIndex<GameRulePrototype>(randomEvent, out var proto))
            {
                var errStr = Loc.GetString("station-event-system-run-random-event-no-valid-events");
                return errStr;
            }

            GameTicker.AddGameRule(proto);
            var str = Loc.GetString("station-event-system-run-event",("eventName", randomEvent));
            return str;
        }

        /// <summary>
        /// Reset the event timer once the event is done.
        /// </summary>
        private void ResetTimer()
        {
            _timeUntilNextEvent = _random.Next(20*60, 40*60);
        }
    }
}
