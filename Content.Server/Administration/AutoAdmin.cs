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

namespace Content.Server.Administration;

public sealed class AutoAdmin : EntitySystem, IAutoAdmin
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLogger = default!;

    private Dictionary<NetUserId, float> record = new Dictionary<NetUserId, float>();

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
            Score(attackerId, -damage);
            return;
        }
        NetUserId attackedId = attackedActor.PlayerSession.UserId;

        // allow anyone to fight antags
        if (IsAntag(attackedId))
        {
            AdminLogger.Add(LogType.AutoAdmin, LogImpact.Medium, $"{ToPrettyString(attacker)} attacks antag {ToPrettyString(attacked)}");
            return;
        }

        float n = Score(attackerId, damage);
        AdminLogger.Add(LogType.AutoAdmin, LogImpact.High, $"RDM: {ToPrettyString(attacker)} attacks {ToPrettyString(attacked)}");
        AdminAction(attackerActor.PlayerSession, n);
    }

    private float Score(NetUserId id, float score)
    {
        if (!record.ContainsKey(id))
            record.Add(id, 0f);

        record[id] = MathF.Max(record[id] + score, 0f);
        return record[id];
    }

    private void OnReset(RoundRestartCleanupEvent ev)
    {
        record.Clear();
    }

    private void AdminAction(IPlayerSession session, float n)
    {
        var _bwoinkSystem = _entitySystemManager.GetEntitySystem<BwoinkSystem>();
        if (n >= _cfg.GetCVar(CCVars.AutoKickThresh) && _cfg.GetCVar(CCVars.AutoAdmin) >= 2)
        {
            // kick
            _netManager.DisconnectChannel(session.ConnectedClient, Loc.GetString("autoadmin-kick"));
            _bwoinkSystem.Bwoink(session.UserId, Loc.GetString("autoadmin-kick"));
            AdminLogger.Add(LogType.AutoAdmin, LogImpact.High, $"kicking ${session.UserId}");
        }
        else if (n >= _cfg.GetCVar(CCVars.AutoWarnThresh))
        {
            // warn
            _bwoinkSystem.Bwoink(session.UserId, Loc.GetString("autoadmin-no-rdm"));
            AdminLogger.Add(LogType.AutoAdmin, LogImpact.High, $"warning ${session.UserId}");
        }
    }

    private bool IsAntag(NetUserId uid)
    {
        if (record.TryGetValue(uid, out var score))
        {
            if (score >= _cfg.GetCVar(CCVars.AutoWarnThresh))
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
}
