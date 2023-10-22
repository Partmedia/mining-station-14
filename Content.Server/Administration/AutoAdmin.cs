using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

using Content.Server.Administration.Systems;
using Content.Shared.Administration;
using Content.Shared.CCVar;

namespace Content.Server.Administration;

public sealed class AutoAdmin : IAutoAdmin
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    
    private Dictionary<NetUserId, int> record = new Dictionary<NetUserId, int>();

    public void CheckCombat(EntityUid attacker, EntityUid attacked)
    {
        if (!Enabled())
            return;

        // victim is not a player, ignore
        if (!IsPlayer(attacked))
            return;

        // attacker is not a player, ignore
        if (!_entityManager.TryGetComponent<ActorComponent>(attacker, out var attackerActor))
            return;

        // if either attacker or attacked is an antag, there's a reason to fight
        if (IsAntag(attacker) || IsAntag(attacked))
            return;

        var attackerId = attackerActor.PlayerSession.UserId;
        int n = RecordHit(attackerId);
        Logger.InfoS("autoadmin", $"auto-admin violation {n} for {attackerId}");
        AdminAction(attackerActor.PlayerSession, n);
    }

    private int RecordHit(NetUserId id)
    {
        if (!record.ContainsKey(id))
            record.Add(id, 0);

        record[id] += 1;
        return record[id];
    }

    private void AdminAction(IPlayerSession session, int n)
    {
        if (n >= 5 && _cfg.GetCVar(CCVars.AutoAdmin) >= 2)
        {
            // kick
            _netManager.DisconnectChannel(session.ConnectedClient, "Kicked by admin");
        }
        else if (n == 3)
        {
            // warn
            var _bwoinkSystem = _entitySystemManager.GetEntitySystem<BwoinkSystem>();
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
