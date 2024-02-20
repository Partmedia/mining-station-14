using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

using Content.Server.Administration.Systems;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;

namespace Content.Server.Administration;

public sealed class AutoAdmin : EntitySystem, IAutoAdmin
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;

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

        // attacker is not a player, ignore
        if (!_entityManager.TryGetComponent<ActorComponent>(attacker, out var attackerActor))
            return;

        var attackerId = attackerActor.PlayerSession.UserId;

        // victim is not a player, reduce player's RDM score
        if (!IsPlayer(attacked))
        {
            Score(attackerId, -damage);
            return;
        }

        // if either attacker or attacked is an antag, there's a reason to fight
        if (IsAntag(attacker) || IsAntag(attacked))
            return;

        float n = Score(attackerId, damage);
        Logger.InfoS("autoadmin", $"auto-admin violation {n} for {attackerId}");
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
        }
        else if (n >= _cfg.GetCVar(CCVars.AutoWarnThresh))
        {
            // warn
            _bwoinkSystem.Bwoink(session.UserId, Loc.GetString("autoadmin-no-rdm"));
        }
    }

    private bool IsPlayer(EntityUid uid)
    {
        return _entityManager.HasComponent<ActorComponent>(uid);
    }

    private bool IsAntag(EntityUid uid)
    {
        return false; // FIXME
    }

    private bool Enabled()
    {
        return _cfg.GetCVar(CCVars.AutoAdmin) > 0;
    }
}
