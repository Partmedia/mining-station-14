using Content.Shared.Body.Part;
using Content.Server.Bionics.Components;
using Content.Server.Gatherable.Components;
using Content.Server.Gatherable;
using Content.Shared.Body.Events;

namespace Content.Server.Bionics;

public sealed class DrillArmSystem : EntitySystem
{
    [Dependency] private readonly GatherableSystem _gatherable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DrillArmComponent, PartAddedToBodyEvent>(HandleBodyPartAdded);
        SubscribeLocalEvent<DrillArmComponent, PartRemovedFromBodyEvent>(HandleBodyPartRemoved);
    }

    private void HandleBodyPartAdded(EntityUid uid, DrillArmComponent component, PartAddedToBodyEvent args)
    {
        //first check if the new entity has a gatherable component, if it doesn't then its a pretty lousy drill arm
        if (!TryComp<GatheringToolComponent>(uid, out var tool))
            return;

        var newTool = EntityManager.EnsureComponent<GatheringToolComponent>(args.Body);
        _gatherable.TransferToolValues(tool, newTool);
    }

    private void HandleBodyPartRemoved(EntityUid uid, DrillArmComponent component, PartRemovedFromBodyEvent args)
    {
        RemComp<GatheringToolComponent>(args.Body);
    }
}

