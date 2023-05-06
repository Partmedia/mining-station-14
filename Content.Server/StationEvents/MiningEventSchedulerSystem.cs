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

using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

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
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;

        private readonly HttpClient _httpClient = new();

        private IReadOnlyList<string> 音乐 = new List<string>{
            "/Mining/Audio/16tons.ogg",
            "/Mining/Audio/big_john.ogg",
            "/Mining/Audio/working.ogg"
        };

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
            _soundSystem.DispatchGlobalEventMusic(RandomExtensions.Pick(_random, 音乐));
            ReportRound(Loc.GetString("round-started"));
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
            if (!RuleStarted)
                return;

            foreach (var station in _station.Stations)
            {
                TryComp<StationBankAccountComponent>(station, out var bankComponent);
                if (bankComponent != null)
                {
                    var profit = bankComponent.Balance - bankComponent.InitialBalance;
                    ev.AddLine(Loc.GetString("station-profit", ("profit", profit)));
                    var players = GetAllPlayers();
                    ReportRound(Loc.GetString("team-profit", ("team", ListPlayers(players)), ("profit", profit)));
                    LogProfit(profit, players);
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

        private void LogProfit(int profit, SortedSet<String> players)
        {
            var endText = String.Format("The team of {0} made a profit of {1} spacebucks.", ListPlayers(players), profit);
            Logger.InfoS("mining", "profit:{0}", endText);
        }

        private async Task ReportRound(String message)
        {
            Logger.InfoS("discord", message);
            String _webhookUrl = _configurationManager.GetCVar(CCVars.DiscordRoundEndWebook);
            if (_webhookUrl == string.Empty)
                return;

            var payload = new WebhookPayload{ Content = message };
            var ser_payload = JsonSerializer.Serialize(payload);
            var content = new StringContent(ser_payload, Encoding.UTF8, "application/json");
            var request = await _httpClient.PostAsync($"{_webhookUrl}?wait=true", content);
            var reply = await request.Content.ReadAsStringAsync();
            if (!request.IsSuccessStatusCode)
            {
                Logger.ErrorS("mining", $"Discord returned bad status code when posting message: {request.StatusCode}\nResponse: {reply}");
            }
        }

        private struct WebhookPayload
        {
            [JsonPropertyName("content")]
            public String Content { get; set; }
        }
    }
}
