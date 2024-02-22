using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Coordinates;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Content.Shared.Humanoid;
using static Content.Shared.Humanoid.HumanoidAppearanceState;
using Content.Shared.Humanoid.Prototypes;

namespace Content.Shared.Body.Systems;

public partial class SharedBodySystem
{
    public void InitializeBody()
    {
        SubscribeLocalEvent<BodyComponent, ComponentInit>(OnBodyInit);

        SubscribeLocalEvent<BodyComponent, ComponentGetState>(OnBodyGetState);
        SubscribeLocalEvent<BodyComponent, ComponentHandleState>(OnBodyHandleState);
    }

    private void OnBodyInit(EntityUid bodyId, BodyComponent body, ComponentInit args)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (body.Prototype == null || body.Root != null)
            return;

        var prototype = Prototypes.Index<BodyPrototype>(body.Prototype);
        InitBody(body, prototype);
        Dirty(body); // Client doesn't actually spawn the body, need to sync it
    }

    private void OnBodyGetState(EntityUid uid, BodyComponent body, ref ComponentGetState args)
    {
        args.State = new BodyComponentState(body.Root, body.GibSound);
    }

    private void OnBodyHandleState(EntityUid uid, BodyComponent body, ref ComponentHandleState args)
    {
        if (args.Current is not BodyComponentState state)
            return;

        body.Root = state.Root;
        body.GibSound = state.GibSound;
    }

    public bool TryCreateBodyRootSlot(
        EntityUid? bodyId,
        string slotId,
        [NotNullWhen(true)] out BodyPartSlot? slot,
        BodyComponent? body = null)
    {
        slot = null;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (bodyId == null ||
            !Resolve(bodyId.Value, ref body, false) ||
            body.Root != null)
            return false;

        slot = new BodyPartSlot(slotId, bodyId.Value, null, body.Species);
        body.Root = slot;

        return true;
    }

    protected abstract void InitBody(BodyComponent body, BodyPrototype prototype);

    protected void InitPart(BodyPartComponent parent, BodyPrototype prototype, string slotId, HashSet<string>? initialized = null)
    {
        initialized ??= new HashSet<string>();

        if (initialized.Contains(slotId))
            return;

        initialized.Add(slotId);

        var (_, connections, organs) = prototype.Slots[slotId];
        connections = new HashSet<string>(connections);
        connections.ExceptWith(initialized);

        var coordinates = parent.Owner.ToCoordinates();
        var subConnections = new List<(BodyPartComponent child, string slotId)>();

        Containers.EnsureContainer<Container>(parent.Owner, BodyContainerId);

        foreach (var connection in connections)
        {
            var childSlot = prototype.Slots[connection];

            //TODO tidy this up
            if (childSlot.Part != null)
            {
                var childPart = Spawn(childSlot.Part, coordinates);
                var childPartComponent = Comp<BodyPartComponent>(childPart);

                childPartComponent.OriginalBody = parent.Owner;
                var slot = CreatePartSlot(connection, parent.Owner, childPartComponent.PartType, parent.Species, parent);
                if (slot == null)
                {
                    Logger.Error($"Could not create slot for connection {connection} in body {prototype.ID}");
                    continue;
                }

                if (TryComp(parent.Owner, out HumanoidAppearanceComponent? bodyAppearance))
                {
                    var appearance = AddComp<BodyPartAppearanceComponent>(childPart);
                    appearance.OriginalBody = childPartComponent.OriginalBody;
                    appearance.Color = bodyAppearance.SkinColor;

                    var symmetry = ((BodyPartSymmetry)childPartComponent.Symmetry).ToString();
                    if (symmetry == "None")
                        symmetry = "";
                    appearance.ID = "removed" + symmetry + ((BodyPartType)childPartComponent.PartType).ToString();

                    Dirty(appearance);
                }

                AttachPart(childPart, slot, childPartComponent);
                subConnections.Add((childPartComponent, connection));       
            }
            else if (childSlot.SlotType != null)
            {
                var slot = CreatePartSlot(connection, parent.Owner, childSlot.SlotType.Value, parent.Species, parent);          
                if (slot == null)
                {
                    Logger.Error($"Could not create slot for connection {connection} in body {prototype.ID}");
                    continue;
                }
                slot.Cauterised = true;
                AttachPart(null, slot);
            }           
            else
                continue;
        }

        foreach (KeyValuePair<string, OrganPrototypeSlot> organSlot in organs)
        {
            if (organSlot.Value != null)
            {
                var slot = CreateOrganSlot(organSlot.Key, parent.Owner, organSlot.Value.SlotType, organSlot.Value.Internal, parent);
                if (slot == null)
                {
                    Logger.Error($"Could not create slot for connection {organSlot.Key} in body {prototype.ID}");
                    continue;
                }

                if (organSlot.Value.Organ != null)
                {
                    var organ = Spawn(organSlot.Value.Organ, coordinates);
                    var organComponent = Comp<OrganComponent>(organ);
                    InsertOrgan(organ, slot, organComponent);
                } else
                {
                    slot.Cauterised = true;
                }
            }
        }

        foreach (var connection in subConnections)
        {
            InitPart(connection.child, prototype, connection.slotId, initialized);
        }
    }
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid? id, BodyComponent? body = null)
    {
        if (id == null ||
            !Resolve(id.Value, ref body, false) ||
            !TryComp(body.Root?.Child, out BodyPartComponent? part))
            yield break;

        yield return (body.Root.Child.Value, part);

        foreach (var child in GetPartChildren(body.Root.Child))
        {
            yield return child;
        }
    }

    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetBodyOrgans(EntityUid? bodyId, BodyComponent? body = null)
    {
        if (bodyId == null || !Resolve(bodyId.Value, ref body, false))
            yield break;

        foreach (var part in GetBodyChildren(bodyId, body))
        {
            foreach (var organ in GetPartOrgans(part.Id, part.Component))
            {
                yield return organ;
            }
        }
    }

    public IEnumerable<BodyPartSlot> GetBodyAllSlots(EntityUid? bodyId, BodyComponent? body = null)
    {
        if (bodyId == null || !Resolve(bodyId.Value, ref body, false))
            yield break;

        foreach (var slot in GetPartAllSlots(body.Root?.Child))
        {
            yield return slot;
        }
    }

    /// <summary>
    /// Returns all body part slots in the graph, including ones connected by
    /// body parts which are null.
    /// </summary>
    /// <param name="partId"></param>
    /// <param name="part"></param>
    /// <returns></returns>
    public IEnumerable<BodyPartSlot> GetAllBodyPartSlots(EntityUid partId, BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, false))
            yield break;

        foreach (var slot in part.Children.Values)
        {
            if (!TryComp<BodyPartComponent>(slot.Child, out var childPart))
                continue;

            yield return slot;

            foreach (var child in GetAllBodyPartSlots(slot.Child.Value, childPart))
            {
                yield return child;
            }
        }
    }

    public virtual HashSet<EntityUid> GibBody(EntityUid? partId, bool gibOrgans = false,
        BodyComponent? body = null, bool deleteItems = false)
    {
        if (partId == null || !Resolve(partId.Value, ref body, false))
            return new HashSet<EntityUid>();

        EntityUid? rootPart = null;
        if (body.Root is not null && body.Root.Child is not null)
        {
            rootPart = body.Root.Child;
        }

        var parts = GetBodyChildren(partId, body).ToArray();
        var gibs = new HashSet<EntityUid>(parts.Length);

        foreach (var part in parts)
        {
            DropPart(part.Id, part.Component);
            gibs.Add(part.Id);

            if (!gibOrgans)
                continue;

            foreach (var organ in GetPartOrgans(part.Id, part.Component))
            {
                DropOrgan(organ.Id, organ.Component);
                gibs.Add(organ.Id);
            }

            if (rootPart is not null && part.Id == rootPart.Value)
                QueueDel(part.Id);
        }

        return gibs;
    }
}
