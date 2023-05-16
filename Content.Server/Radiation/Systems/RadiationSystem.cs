using Content.Server.Explosion.EntitySystems;
using Content.Server.Radiation.Components;
using Content.Server.Temperature.Components;
using Content.Shared.Radiation.Components;
using Content.Shared.Radiation.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Server.Radiation.Systems;

public sealed partial class RadiationSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosions = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeCvars();
        InitRadBlocking();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        UnsubscribeCvars();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < GridcastUpdateRate)
            return;

        bool doExplosion = true; // only do one explosion per update
        foreach (var source in EntityQuery<RadiationSourceComponent>())
        {
            // Ignore infinite sources
            if (source.N < 0)
                continue;

            // Exponential decay
            var dt = GridcastUpdateRate;
            var dN = -MathF.Log(2)/source.hl * source.N * dt - source.FissionN;

            // Zero depleted sources without them going negative (infinite)
            source.N = MathF.Max(0, source.N + dN);
            source.D += -dN;
            source.FissionN = 0;

            // Set intensity using flux (-dN). Since intensity is in rad/sec, divide by dt.
            if (source.N > 0)
                source.Intensity = -dN/dt;
            else
                source.Intensity = 0;

            // Glow
            if (source.N > 0)
            {
                var light = EnsureComp<PointLightComponent>(source.Owner);
                light.Color = Color.Cyan;
                light.Energy = source.Intensity / 10;
                light.Radius = source.Intensity / 40 + 0.75f;
            }
            else {
                if (TryComp<PointLightComponent>(source.Owner, out var light))
                    EntityManager.RemoveComponent(source.Owner, light);
            }

            // Heat
            var temp = EnsureComp<TemperatureComponent>(source.Owner);
            var dE = 1e2f * (-dN); // E = mc^2
            temp.CurrentTemperature += dE / temp.SpecificHeat * dt;
            
            // Explosions
            if (doExplosion && source.Intensity > 120)
            {
                _explosions.QueueExplosion(source.Owner, "Default", source.Intensity * 20, 1, source.Intensity);
                doExplosion = false;
            }

            // Fission
            if (source.N > 0 && source.fissionK > 0)
                EnsureComp<RadiationReceiverComponent>(source.Owner);
            else
                if (TryComp<RadiationReceiverComponent>(source.Owner, out var rad))
                    EntityManager.RemoveComponent(source.Owner, rad);
        }

        UpdateGridcast();
        UpdateResistanceDebugOverlay();
        _accumulator = 0f;
    }

    public void IrradiateEntity(EntityUid uid, float radsPerSecond, float time)
    {
        var msg = new OnIrradiatedEvent(time, radsPerSecond);
        RaiseLocalEvent(uid, msg);

        // Handle fission
        if (TryComp<RadiationSourceComponent>(uid, out var source))
        {
            var temp = EnsureComp<TemperatureComponent>(source.Owner);
            var K = source.fissionK + source.fissionKTC*(temp.CurrentTemperature-293);
            source.FissionN += radsPerSecond * time * K * (source.N / (source.N + source.D));
        }
    }

    /// <summary>
    ///     Marks entity to receive/ignore radiation rays.
    /// </summary>
    public void SetCanReceive(EntityUid uid, bool canReceive)
    {
        if (canReceive)
        {
            EnsureComp<RadiationReceiverComponent>(uid);
        }
        else
        {
            RemComp<RadiationReceiverComponent>(uid);
        }
    }
}
