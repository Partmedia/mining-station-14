using System.Linq;
using Content.Server.Audio;
using Content.Server.Mind;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Configurations;
using Content.Server.Ghost;
using Content.Server.Players;
using Content.Server.Station.Systems;
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

        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IPrototypeManager _prototype = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly EventManagerSystem _event = default!;
        [Dependency] private readonly ServerGlobalSoundSystem _soundSystem = default!;
        [Dependency] private readonly StationSystem _station = default!;

        /// <summary>
        /// How long until the next check for an event runs
        /// </summary>
        /// Default value is how long until first event is allowed
        [ViewVariables(VVAccess.ReadWrite)]
        private float _timeUntilNextEvent;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        }

        public override void Started()
        {
            ResetTimer();
            _soundSystem.DispatchGlobalEventMusic("/Mining/Audio/16tons.ogg");
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

        private void OnRoundEndText(RoundEndTextAppendEvent ev)
        {
            foreach (var station in _station.Stations)
            {
                TryComp<StationBankAccountComponent>(station, out var bankComponent);
                if (bankComponent != null)
                {
                    var profit = bankComponent.Balance - 2000;
                    ev.AddLine(String.Format("The station made a profit of {0} spacebucks.", profit));
                    Logger.InfoS("mining", "profit:{0}", GenEndText(profit));
                }
            }
        }

        private SortedSet<String> GetAllPlayers()
        {
            SortedSet<String> players = new SortedSet<string>();
            var allMinds = Get<MindTrackerSystem>().AllMinds;
            foreach (var mind in allMinds)
            {
                if (mind == null)
                    continue;

                var userId = mind.OriginalOwnerUserId;
                var observer = mind.AllRoles.Any(role => role is ObserverRole);
                PlayerData? contentPlayerData = null;
                if (_playerManager.TryGetPlayerData(userId, out var playerData))
                {
                    contentPlayerData = playerData.ContentData();
                }

                if (!observer && contentPlayerData != null)
                    players.Add(contentPlayerData.Name);
            }
            return players;
        }

        private String ListPlayers(SortedSet<String> players)
        {
            return String.Join(", ", players);
        }

        private String GenEndText(int profit)
        {
            var players = GetAllPlayers();
            return String.Format("The team of {0} made a profit of {1} spacebucks.", ListPlayers(players), profit);
        }
    }
}
