using Content.Server.Power.Components;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.Mining;

[RegisterComponent]
public class HopperComponent : Component
{
    [DataField("rate")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Rate = 1f; //< Ores per second

    [ViewVariables(VVAccess.ReadWrite)]
    public float Accum = 0f;
}

public class HopperSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _sharedAudioSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Update(float frameTime)
    {
        foreach (var (comp, apc, storage) in EntityManager.EntityQuery<HopperComponent, ApcPowerReceiverComponent, ServerStorageComponent>())
        {
            if (!apc.Powered)
            {
                // Not powered, don't do anything
                comp.Accum = 0;
                continue;
            }
            
            comp.Accum += frameTime;
            if (comp.Accum < comp.Rate)
                continue;

            comp.Accum -= comp.Rate;
            if (storage.StoredEntities != null && storage.StoredEntities.Count > 0)
            {
                // dump one item
                var entity = storage.StoredEntities[0];
                var transform = Transform(entity);
                transform.AttachParentToContainerOrGrid(EntityManager);
                transform.LocalPosition = transform.LocalPosition + _random.NextVector2Box() * 0.3f;
                transform.LocalRotation = _random.NextAngle();
                _sharedAudioSystem.PlayPvs("/Audio/Effects/thunk.ogg", comp.Owner);
            }
        }
    }
}
