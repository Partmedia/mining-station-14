using System.Linq;
using Content.Server.Audio;
using Content.Server.Mind;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Configurations;
using Content.Server.Ghost;
using Content.Server.Players;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

using Content.Shared.Mobs;
using Content.Server.MiningCredits;
using Content.Server.Mind.Components;
using Content.Server.NPC.HTN;
using Content.Server.Warps;

namespace Content.Server.StationEvents
{
    [UsedImplicitly]
    public sealed class MiningProfitManager : GameRuleSystem
    {
        public override string Prototype => "MiningProfitManager";

        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly ServerGlobalSoundSystem _soundSystem = default!;
        [Dependency] private readonly StationSystem _station = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly PricingSystem _pricingSystem = default!;

        private IReadOnlyList<string> 音乐 = new List<string>{
            "/Mining/Audio/16tons.ogg",
            "/Mining/Audio/big_john.ogg",
            "/Mining/Audio/working.ogg"
        };

        private int startValue = 0;

        private SortedSet<string> players = new SortedSet<string>();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayersSpawning);
            SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);

            SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnPlayersSpawned);
            SubscribeLocalEvent<PlayerSpawnCompleteEvent>(HandleLatejoin);
        }

        public override void Started()
        {
            players = new SortedSet<string>();
            _soundSystem.DispatchGlobalEventMusic(RandomExtensions.Pick(_random, 音乐));
        }

        private void OnPlayersSpawning(RulePlayerSpawningEvent ev)
        {
            if (!RuleAdded)
                return;
            startValue = stationPrice();
            Logger.InfoS("mining", $"Initial value: {startValue}");
        }

        private void OnPlayersSpawned(RulePlayerJobsAssignedEvent ev)
        {
            if (!RuleAdded)
                return;

            foreach (var player in ev.Players)
            {
                var data = player.Data.ContentData();
                if (data != null)
                    players.Add(data.Name);
            }
        }

        private void HandleLatejoin(PlayerSpawnCompleteEvent ev)
        {
            if (!RuleAdded)
                return;
            if (!ev.LateJoin)
                return;

            var data = ev.Player.Data.ContentData();
            if (data != null)
                players.Add(data.Name);
        }

        public override void Ended()
        {
        }

        private bool anchored(EntityUid uid)
        {
            if (TryComp<TransformComponent>(uid, out var xform))
                return xform.Anchored;
            return false;
        }

        private int stationPrice()
        {
            double total = 0;
            foreach (var station in _station.Stations)
            {
                if (TryComp<StationDataComponent>(station, out var data))
                {
                    var grid = _station.GetLargestGrid(data);
                    if (grid != null)
                    {
                        var val = _pricingSystem.AppraiseGrid(grid.Value, anchored);
                        total += val;
                    }
                }
            }
            return (int)total;
        }

        private void OnRoundEndText(RoundEndTextAppendEvent ev)
        {
            if (!RuleStarted)
                return;

            int endValue = stationPrice();
            int change = endValue - startValue;
            Logger.InfoS("mining", $"End value: {endValue} (change {change})");

            foreach (var station in _station.Stations)
            {
                int profit = 0;
                if (!TryComp<StationBankAccountComponent>(station, out var bankComponent))
                {
                    continue;
                }

                profit += bankComponent.Balance - bankComponent.InitialBalance + change;

                int powerCosts = 0;
                if (TryComp<StationPowerTrackerComponent>(station, out var powerTracker))
                {
                    powerCosts = (int)Math.Round(powerTracker.TotalPrice);
                    profit -= powerCosts;
                }

                var profitStrings = ListPlayerProfit(profit);

                ev.AddLine(Loc.GetString("cargo-balance", ("amount", bankComponent.Balance)));
                ev.AddLine(Loc.GetString("initial-loan", ("amount", -bankComponent.InitialBalance)));
                ev.AddLine(Loc.GetString("station-value-change", ("amount", change)));
                if (powerTracker != null)
                {
                    ev.AddLine(Loc.GetString("station-power-costs",
                                ("energy", powerTracker.TotalEnergy.ToString("F3")),
                                ("amount", -powerCosts)));
                }
                ev.AddLine("");
                ev.AddLine(Loc.GetString("station-profit", ("profit", profit)));
                ev.AddLine("");
                ev.AddLine(profitStrings.Item1);

                ev.AddSummary(Loc.GetString("team-profit", ("team", ListPlayers(profitStrings.Item2)), ("profit", profit)));
                LogProfit(profit, profitStrings.Item2);
            }
        }

        private (string, SortedSet<String>) ListPlayerProfit(int profit)
        {
            Dictionary<string, int> playerCreds = new Dictionary<string, int>();
            var totalCreds = 0f;

            foreach (var credit in EntityManager.EntityQuery<MiningCreditComponent>())
            {
                if (credit.PlayerName is not null)
                {
                    var player = credit.PlayerName;
                    var numCreds = credit.NumCredits;

                    //if the creds have not yet been record OR there is an entity with more cred, set the player creds
                    if ((playerCreds.ContainsKey(player) && numCreds > playerCreds[player]) || !playerCreds.ContainsKey(player))
                        playerCreds[player] = numCreds;
                }
            }

            foreach (KeyValuePair<string, int> entry in playerCreds)
                totalCreds += entry.Value;

            var profitUnit = totalCreds != 0f ? profit / totalCreds : 0;
            var profitString = "";
            var reportStrings = new SortedSet<String>();

            foreach (KeyValuePair<string,int> entry in playerCreds)
            {
                profitString += String.Format("{0} {1}\n", entry.Key ,Math.Round(profitUnit*entry.Value)); //for round end summary
                reportStrings.Add(String.Format("{0}({1})", entry.Key, Math.Round(profitUnit * entry.Value))); //for log
            }

            var profitStrings = (profitString, reportStrings);

            return profitStrings;
        }

        private String ListPlayers(SortedSet<String> players)
        {
            return String.Join(", ", players);
        }

        private void LogProfit(int profit, SortedSet<String> players)
        {
            var endText = String.Format("The team of {0} made a profit of {1} spacebucks.", ListPlayers(players), profit);
            Logger.InfoS("mining", "profit:{0}", endText);
        }
    }

    [UsedImplicitly]
    public sealed class MiningEventScheduler : GameRuleSystem
    {
        public override string Prototype => "MiningEventScheduler";

        /// <summary>
        /// How long until the next check for an event runs
        /// </summary>
        /// Default value is how long until first event is allowed
        [ViewVariables(VVAccess.ReadWrite)]
        private float _timeUntilNextEvent;

        [Dependency] private readonly EventManagerSystem _event = default!;
        [Dependency] private readonly GameTicker _gameTicker = default!;
        [Dependency] private readonly IPrototypeManager _prototype = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        public override void Started()
        {
            ResetTimer(true);
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
            ResetTimer(false);
        }

        /// <summary>
        /// Randomly runs a valid event.
        /// </summary>
        public string RunMiningEvent()
        {
            List<string> events = new List<string>{"MeteorSwarm","Quake"};
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
        /// Set the time for the next meteor swarm. Meteor swarms increase in frequency
        /// as the round goes on.
        ///
        /// We need to be told whether or not we just got started, because RoundDuration is
        /// only updated after the first Update of a round, i.e. reading RoundDuration gets
        /// us the duration of the last round when starting a new one.
        /// </summary>
        private void ResetTimer(bool start)
        {
            var minsInRound = start ? 0 : _gameTicker.RoundDuration().TotalMinutes;
            var mt = Math.Max(35-2*Math.Sqrt(minsInRound), 6);
            var st = mt/3;
            _timeUntilNextEvent = _random.Next(60*(int)(mt-st), 60*(int)(mt+st));
            Logger.InfoS("mining", $"Next station event in {(int)(_timeUntilNextEvent/60)} minutes ({minsInRound} mins in round, mean {mt}, s {st})");
        }

        public override void Ended()
        {
        }
    }

    [UsedImplicitly]
    public sealed class DungeonRuleSystem : GameRuleSystem
    {
        public override string Prototype => "DungeonRuleSystem";

        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly WarperSystem _dungeon = default!;

        private string OldPool = string.Empty;
        private bool OldSupercond;
        private int KillCount;

        public override void Added()
        {
            OldPool = _configurationManager.GetCVar(CCVars.GameMapPool);
            _configurationManager.SetCVar(CCVars.GameMapPool, "DungeonMapPool");
            OldSupercond = _configurationManager.GetCVar(CCVars.Superconduction);
            _configurationManager.SetCVar(CCVars.Superconduction, false);
        }

        public override void Started()
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("dungeon-intro"));
            KillCount = 0;
        }

        private void OnMobDied(EntityUid mobUid, HTNComponent component, MobStateChangedEvent args)
        {
            if (!RuleStarted)
                return;
            if (args.NewMobState == MobState.Dead)
                KillCount++;
        }

        private void OnRoundEndText(RoundEndTextAppendEvent ev)
        {
            if (!RuleStarted)
                return;

            if (_dungeon.dungeonLevel > 0)
            {
                string depth = Loc.GetString("dungeon-level", ("depth", _dungeon.dungeonLevel));
                ev.AddLine(depth);
                ev.AddSummary(depth);
            }

            string killcount = Loc.GetString("dungeon-kill-count", ("count", KillCount));
            ev.AddLine(killcount);
            ev.AddSummary(killcount);
        }

        public override void Ended()
        {
            _configurationManager.SetCVar(CCVars.GameMapPool, OldPool);
            _configurationManager.SetCVar(CCVars.Superconduction, OldSupercond);
        }
    }
}
