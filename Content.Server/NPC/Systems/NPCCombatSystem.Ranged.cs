using Content.Server.NPC.Components;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCCombatSystem
{
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;

    /// <summary>
    /// Cooldown on raycasting to check LOS.
    /// </summary>
    public const float UnoccludedCooldown = 0.2f;

    private void InitializeRanged()
    {
        SubscribeLocalEvent<NPCRangedCombatComponent, ComponentStartup>(OnRangedStartup);
        SubscribeLocalEvent<NPCRangedCombatComponent, ComponentShutdown>(OnRangedShutdown);
    }

    private void OnRangedStartup(EntityUid uid, NPCRangedCombatComponent component, ComponentStartup args)
    {
        if (TryComp<CombatModeComponent>(uid, out var combat))
        {
            combat.IsInCombatMode = true;
        }
        else
        {
            component.Status = CombatStatus.Unspecified;
        }
    }

    private void OnRangedShutdown(EntityUid uid, NPCRangedCombatComponent component, ComponentShutdown args)
    {
        if (TryComp<CombatModeComponent>(uid, out var combat))
        {
            combat.IsInCombatMode = false;
        }
    }

    private void UpdateRanged(float frameTime)
    {
        var bodyQuery = GetEntityQuery<PhysicsComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();
        var combatQuery = GetEntityQuery<CombatModeComponent>();

        foreach (var (comp, xform) in EntityQuery<NPCRangedCombatComponent, TransformComponent>())
        {
            if (comp.Status == CombatStatus.Unspecified)
                continue;

            if (!xformQuery.TryGetComponent(comp.Target, out var targetXform) ||
                !bodyQuery.TryGetComponent(comp.Target, out var targetBody))
            {
                comp.Status = CombatStatus.TargetUnreachable;
                comp.ShootAccumulator = 0f;
                continue;
            }

            if (targetXform.MapID != xform.MapID)
            {
                comp.Status = CombatStatus.TargetUnreachable;
                comp.ShootAccumulator = 0f;
                continue;
            }

            if (combatQuery.TryGetComponent(comp.Owner, out var combatMode))
            {
                combatMode.IsInCombatMode = true;
            }

            var gun = _gun.GetGun(comp.Owner);

            if (gun == null)
            {
                comp.Status = CombatStatus.NoWeapon;
                comp.ShootAccumulator = 0f;
                continue;
            }

            comp.LOSAccumulator -= frameTime;

            var (x, worldRot) = _transform.GetWorldPositionRotation(xform, xformQuery);
            var v = gun.ProjectileSpeed; // bullet velocity
            var (xt, targetRot) = _transform.GetWorldPositionRotation(targetXform, xformQuery);
            var vt = targetBody.LinearVelocity; // target velocity

            /// Targeting
            Vector2 targetSpot;
            Angle goalRotation;
            var dx = xt - x; // target displacement from gun
            var distance = dx.Length; // distance to target

            if (comp.Advanced)
            {
                var phi = (-dx).ToWorldAngle() - vt.ToWorldAngle();
                var theta = Math.Asin(vt.Length/v * Math.Sin(phi.Theta));
                goalRotation = dx.ToWorldAngle() + theta;
                var psi = Math.PI - phi - theta;
                float intercept_dist = (float)(distance * Math.Sin(theta)/Math.Sin(psi));
                targetSpot = xt + vt.Normalized*intercept_dist;
            }
            else
            {
                // We'll work out the projected spot of the target and shoot there instead of where they are.
                targetSpot = xt + vt * distance / v;
                goalRotation = (targetSpot - x).ToWorldAngle();
            }

            // TODO: Should be doing these raycasts in parallel
            // Ideally we'd have 2 steps, 1. to go over the normal details for shooting and then 2. to handle beep / rotate / shoot
            var oldInLos = comp.TargetInLOS;
            if (comp.LOSAccumulator < 0f)
            {
                comp.LOSAccumulator += UnoccludedCooldown;
                comp.TargetInLOS = _interaction.InRangeUnobstructed(comp.Owner, comp.Target, distance + 0.1f) &&
                    (!comp.Advanced | _interaction.InRangeUnobstructed(comp.Owner, new MapCoordinates(targetSpot, xform.MapID), distance + 0.1f));
            }

            if (!comp.TargetInLOS)
            {
                comp.ShootAccumulator = 0f;
                comp.Status = CombatStatus.TargetUnreachable;
                continue;
            }

            if (!oldInLos && comp.SoundTargetInLOS != null)
            {
                _audio.PlayPvs(comp.SoundTargetInLOS, comp.Owner);
            }

            comp.ShootAccumulator += frameTime;

            if (comp.ShootAccumulator < comp.ShootDelay)
            {
                continue;
            }

            var rotationSpeed = comp.RotationSpeed;

            if (!_rotate.TryRotateTo(comp.Owner, goalRotation, frameTime, comp.AccuracyThreshold, rotationSpeed?.Theta ?? double.MaxValue, xform))
            {
                continue;
            }

            // TODO: LOS
            // TODO: Ammo checks
            // TODO: Burst fire
            // TODO: Cycling
            // Max rotation speed

            // TODO: Check if we can face

            if (!Enabled || !_gun.CanShoot(gun))
                continue;

            EntityCoordinates targetCordinates;

            if (_mapManager.TryFindGridAt(xform.MapID, xt, out var mapGrid))
            {
                targetCordinates = new EntityCoordinates(mapGrid.Owner, mapGrid.WorldToLocal(targetSpot));
            }
            else
            {
                targetCordinates = new EntityCoordinates(xform.MapUid!.Value, targetSpot);
            }

            _gun.AttemptShoot(comp.Owner, gun, targetCordinates);
        }
    }
}
