using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

using Content.Shared.Administration.Logs;
using Content.Server.Administration.Systems;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;

// banning
using Content.Server.Database;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server.Administration;

public class AutoAdminRecord
{
    public AutoAdminState State;
    public float Score;
}

public enum AutoAdminState
{
    None,
    Warned,
    Kicked,
    Banned,
}

public sealed class AutoAdmin : EntitySystem, IAutoAdmin
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;

    private Dictionary<NetUserId, AutoAdminRecord> record = new Dictionary<NetUserId, AutoAdminRecord>();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnReset);
    }

    public void CheckCombat(EntityUid attacker, EntityUid attacked, float damage)
    {
        if (!Enabled())
            return;

        // ignore staff of healing(?)
        if (damage < 0)
            return;

        // attacker is not a player, ignore
        if (!_entityManager.TryGetComponent<ActorComponent>(attacker, out var attackerActor))
            return;
        NetUserId attackerId = attackerActor.PlayerSession.UserId;

        // victim is not a player, reduce player's RDM score
        if (!_entityManager.TryGetComponent<ActorComponent>(attacked, out var attackedActor))
        {
            UpdateScore(attackerId, -damage);
            return;
        }
        NetUserId attackedId = attackedActor.PlayerSession.UserId;

        // allow anyone to fight antags
        if (IsAntag(attackedId))
        {
            AdminLogger.Add(LogType.AutoAdmin, LogImpact.Medium, $"{ToPrettyString(attacker)} attacks antag {ToPrettyString(attacked)}");
            return;
        }

        UpdateScore(attackerId, damage);
        AdminLogger.Add(LogType.AutoAdmin, LogImpact.High, $"RDM: {ToPrettyString(attacker)} attacks {ToPrettyString(attacked)}");
        DoAutoAdmin(attackerId, attackerActor.PlayerSession);
    }

    private void UpdateScore(NetUserId id, float score)
    {
        if (!record.ContainsKey(id))
            record.Add(id, new AutoAdminRecord());

        score *= AgeFactor();

        record[id].Score = MathF.Max(record[id].Score + score, 0f);
    }

    private float AgeFactor()
    {
        return 1;
    }

    private void DoAutoAdmin(NetUserId attacker, IPlayerSession session)
    {
        var profile = record[attacker];
        var _bwoinkSystem = _entitySystemManager.GetEntitySystem<BwoinkSystem>();
        switch (profile.State)
        {
            case AutoAdminState.None:
                if (profile.Score >= _cfg.GetCVar(CCVars.AutoWarnThresh))
                {
                    // warn
                    _bwoinkSystem.Bwoink(session.UserId, Loc.GetString("autoadmin-no-rdm"));
                    AdminLogger.Add(LogType.AutoAdmin, LogImpact.High, $"warning ${session.UserId}");
                    profile.State = AutoAdminState.Warned;
                }
                break;
            case AutoAdminState.Warned:
                if (profile.Score >= _cfg.GetCVar(CCVars.AutoKickThresh) && _cfg.GetCVar(CCVars.AutoAdmin) >= 2)
                {
                    // kick
                    _bwoinkSystem.Bwoink(session.UserId, Loc.GetString("autoadmin-kick"));
                    session.ConnectedClient.Disconnect(Loc.GetString("autoadmin-kick"));
                    AdminLogger.Add(LogType.AutoAdmin, LogImpact.High, $"kicking ${session.UserId}");
                    profile.State = AutoAdminState.Kicked;
                }
                break;
            case AutoAdminState.Kicked:
                if (_cfg.GetCVar(CCVars.AutoAdmin) >= 3)
                {
                    // ban
                    _bwoinkSystem.Bwoink(session.UserId, Loc.GetString("autoadmin-ban"));
                    DoBan(attacker, _cfg.GetCVar(CCVars.AutoBanMins), Loc.GetString("autoadmin-ban"), session);
                    AdminLogger.Add(LogType.AutoAdmin, LogImpact.High, $"banning ${session.UserId}");
                }
                break;
        }
    }

    private void OnReset(RoundRestartCleanupEvent ev)
    {
        record.Clear();
    }

    private bool IsAntag(NetUserId uid)
    {
        if (record.TryGetValue(uid, out var profile))
        {
            if (profile.State != AutoAdminState.None)
            {
                return true;
            }
        }
        return false;
    }

    private bool Enabled()
    {
        return _cfg.GetCVar(CCVars.AutoAdmin) > 0;
    }

    // mostly copy/pasted from BanCommand
    private async Task DoBan(NetUserId target, int minutes, string reason, IPlayerSession session)
    {
        var located = await _locator.LookupIdAsync(target);
        if (located == null)
        {
            return;
        }

        var targetUid = located.UserId;
        var targetHWid = located.LastHWId;
        var targetAddr = located.LastAddress;

        DateTimeOffset? expires = null;
        if (minutes > 0)
        {
            expires = DateTimeOffset.Now + TimeSpan.FromMinutes(minutes);
        }

        (IPAddress, int)? addrRange = null;
        if (targetAddr != null)
        {
            if (targetAddr.IsIPv4MappedToIPv6)
                targetAddr = targetAddr.MapToIPv4();

            // Ban /64 for IPv4, /32 for IPv4.
            var cidr = targetAddr.AddressFamily == AddressFamily.InterNetworkV6 ? 64 : 32;
            addrRange = (targetAddr, cidr);
        }

        var banDef = new ServerBanDef(
            null,
            targetUid,
            addrRange,
            targetHWid,
            DateTimeOffset.Now,
            expires,
            reason,
            null,
            null);

        await _dbManager.AddServerBanAsync(banDef);

        var response = new StringBuilder($"Banned {target} with reason \"{reason}\"");

        response.Append(expires == null ?
            " permanently."
            : $" until {expires}");

        session.ConnectedClient.Disconnect(banDef.DisconnectMessage);
    }
}
