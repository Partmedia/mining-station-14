using System.Linq;
using Content.Shared.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Shared.CCVar;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Players;
using System.Text;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        [ViewVariables]
        private readonly Dictionary<NetUserId, PlayerGameStatus> _playerGameStatuses = new();

        [ViewVariables]
        private TimeSpan _roundStartTime;

        [ViewVariables]
        public new bool Paused { get; set; }

        [ViewVariables]
        private bool _roundStartCountdownHasNotStartedYetDueToNoPlayers;

        public IReadOnlyDictionary<NetUserId, PlayerGameStatus> PlayerGameStatuses => _playerGameStatuses;

        private void InitMinPlayers()
        {
            SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
            SubscribeLocalEvent<PlayerDisconnectedEvent>(OnPlayerDisconnected);
        }

        public void UpdateInfoText()
        {
            RaiseNetworkEvent(GetInfoMsg(), Filter.Empty().AddPlayers(_playerManager.NetworkedSessions));
        }

        private string GetInfoText()
        {
            if (Preset == null)
            {
                return string.Empty;
            }

            var playerCount = $"{_playerManager.PlayerCount}";
            var readyCount = _playerGameStatuses.Values.Count(x => x == PlayerGameStatus.ReadyToPlay);

            StringBuilder stationNames = new StringBuilder();
            if (_stationSystem.Stations.Count != 0)
            {
                foreach (EntityUid entUID in _stationSystem.Stations)
                {
                    StationDataComponent? stationData = null;
                    MetaDataComponent? metaData = null;
                    if (Resolve(entUID, ref stationData, ref metaData, logMissing: true))
                    {
                        if (stationNames.Length > 0)
                            stationNames.Append('\n');

                        stationNames.Append(metaData.EntityName);
                    }
                }
            }
            else
            {
                stationNames.Append(Loc.GetString("game-ticker-no-map-selected"));
            }

            var gmTitle = Loc.GetString(Preset.ModeTitle);
            var desc = Loc.GetString(Preset.Description);
            return Loc.GetString(RunLevel == GameRunLevel.PreRoundLobby ? "game-ticker-get-info-preround-text" : "game-ticker-get-info-text",
                ("roundId", RoundId), ("playerCount", playerCount), ("readyCount", readyCount), ("mapName", stationNames.ToString()),("gmTitle", gmTitle),("desc", desc));
        }

        private TickerLobbyReadyEvent GetStatusSingle(ICommonSession player, PlayerGameStatus gameStatus)
        {
            return new (new Dictionary<NetUserId, PlayerGameStatus> { { player.UserId, gameStatus } });
        }

        private TickerLobbyReadyEvent GetPlayerStatus()
        {
            var players = new Dictionary<NetUserId, PlayerGameStatus>();
            foreach (var player in _playerGameStatuses.Keys)
            {
                _playerGameStatuses.TryGetValue(player, out var status);
                players.Add(player, status);
            }
            return new TickerLobbyReadyEvent(players);
        }

        private TickerLobbyStatusEvent GetStatusMsg(IPlayerSession session)
        {
            _playerGameStatuses.TryGetValue(session.UserId, out var status);
            return new TickerLobbyStatusEvent(RunLevel != GameRunLevel.PreRoundLobby, LobbySong, LobbyBackground,status == PlayerGameStatus.ReadyToPlay, _roundStartTime, _roundStartTimeSpan, Paused);
        }

        private void SendStatusToAll()
        {
            foreach (var player in _playerManager.ServerSessions)
            {
                RaiseNetworkEvent(GetStatusMsg(player), player.ConnectedClient);
            }
        }

        private TickerLobbyInfoEvent GetInfoMsg()
        {
            return new (GetInfoText());
        }

        private void UpdateLateJoinStatus()
        {
            RaiseNetworkEvent(new TickerLateJoinStatusEvent(DisallowLateJoin));
        }

        public bool PauseStart(bool pause = true)
        {
            if (Paused == pause)
            {
                return false;
            }

            Paused = pause;

            if (!pause)
            {
                // Reset round timer after unpausing
                _roundStartTime = _gameTiming.CurTime + LobbyDuration;
            }

            RaiseNetworkEvent(new TickerLobbyCountdownEvent(_roundStartTime, Paused));

            _chatManager.DispatchServerAnnouncement(Loc.GetString(Paused
                ? "game-ticker-pause-start"
                : "game-ticker-pause-start-resumed"));

            return true;
        }

        public bool TogglePause()
        {
            PauseStart(!Paused);
            return Paused;
        }

        public void ToggleReadyAll(bool ready)
        {
            var status = ready ? PlayerGameStatus.ReadyToPlay : PlayerGameStatus.NotReadyToPlay;
            foreach (var playerUserId in _playerGameStatuses.Keys)
            {
                _playerGameStatuses[playerUserId] = status;
                if (!_playerManager.TryGetSessionById(playerUserId, out var playerSession))
                    continue;
                RaiseNetworkEvent(GetStatusMsg(playerSession), playerSession.ConnectedClient);
                RaiseNetworkEvent(GetStatusSingle(playerSession, status));
            }
        }

        public void ToggleReady(IPlayerSession player, bool ready)
        {
            if (!_playerGameStatuses.ContainsKey(player.UserId))
                return;

            if (!_userDb.IsLoadComplete(player))
                return;

            var status = ready ? PlayerGameStatus.ReadyToPlay : PlayerGameStatus.NotReadyToPlay;
            _playerGameStatuses[player.UserId] = ready ? PlayerGameStatus.ReadyToPlay : PlayerGameStatus.NotReadyToPlay;
            RaiseNetworkEvent(GetStatusMsg(player), player.ConnectedClient);
            RaiseNetworkEvent(GetStatusSingle(player, status));
            // update server info to reflect new ready count
            UpdateInfoText();
            CheckMinPlayers();
        }

        private void CheckMinPlayers()
        {
            if (RunLevel != GameRunLevel.PreRoundLobby)
                return;

            var minPlayers = _configurationManager.GetCVar(CCVars.MinPlayers);
            if (minPlayers == 0)
            {
                // Disabled, return.
                return;
            }

            var readyCount = Readied();
            var needPlayers = minPlayers - readyCount;
            if (needPlayers > 0)
            {
                _chatManager.DispatchServerAnnouncement(String.Format("At least {0:d} more readied players are required to start the round.", needPlayers));
                PauseStart(true);
            } else {
                PauseStart(false);
            }
        }

        private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
        {
            CheckMinPlayers();
        }

        private void OnPlayerDisconnected(PlayerDisconnectedEvent ev)
        {
            _playerGameStatuses.Remove(ev.PlayerSession.UserId);
            CheckMinPlayers();
        }

        public int Readied()
        {
            return _playerGameStatuses.Values.Count(x => x == PlayerGameStatus.ReadyToPlay);
        }
    }
}
