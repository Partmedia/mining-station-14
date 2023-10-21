using Robust.Server.GameObjects;
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

        // TODO: check if attacker is an antag, if so, ignore

        var attackerId = attackerActor.PlayerSession.UserId;
        Logger.InfoS("autoadmin", $"PvP detected: {attackerId}");

        var _bwoinkSystem = _entitySystemManager.GetEntitySystem<BwoinkSystem>();
        _bwoinkSystem.Bwoink(attackerId, Loc.GetString("autoadmin-no-rdm"));
    }

    private bool IsPlayer(EntityUid uid)
    {
        return _entityManager.HasComponent<ActorComponent>(uid);
    }

    private bool Enabled()
    {
        return _cfg.GetCVar(CCVars.AutoAdmin);
    }
}
