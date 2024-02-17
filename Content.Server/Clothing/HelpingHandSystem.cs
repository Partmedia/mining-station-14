using Content.Server.Clothing.Components;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Server.Clothing.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Part;

namespace Content.Server.Clothing;

public sealed class HelpingHandSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _handSystem = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HelpingHandComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<HelpingHandComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotUnequipped(EntityUid uid, HelpingHandComponent component, GotUnequippedEvent args)
    {
        var slotIdPrefix = "helpingHand";
        if (args.Slot == "outerClothing")
        {
            var equipee = args.Equipee;
            if (TryComp(equipee, out BodyComponent? sharedBodyComp) && TryComp(equipee, out SharedHandsComponent? sharedHandComp))
            {
                //get body root part
                //remove hand from wearable slot
                //remove wearable slot (ensure there is a remove slot func)
                var rootSlot = sharedBodyComp.Root;
                if (rootSlot is not null && rootSlot.Child is not null && TryComp(rootSlot.Child, out BodyPartComponent? bodyPart))
                {
                    _handSystem.RemoveHand(equipee, slotIdPrefix, sharedHandComp);
                    _bodySystem.TryRemovePartSlot(rootSlot.Child.Value, slotIdPrefix + uid.ToString(), bodyPart);
                }
            }
        }
    }

    private void OnGotEquipped(EntityUid uid, HelpingHandComponent component, GotEquippedEvent args)
    {
        var slotIdPrefix = "helpingHand";
        if (args.Slot == "outerClothing")
        {
            var equipee = args.Equipee;
            if (TryComp(equipee, out BodyComponent? sharedBodyComp) && TryComp(equipee, out SharedHandsComponent? sharedHandComp)) {
                //get body root part
                var rootSlot = sharedBodyComp.Root;
                if (rootSlot is not null && rootSlot.Child is not null && TryComp(rootSlot.Child, out BodyPartComponent? bodyPart))
                {
                    //Add wearable body part slot to body root part
                    _bodySystem.TryCreatePartSlot(rootSlot.Child, slotIdPrefix + uid.ToString(), out var slot, bodyPart, true);
                    var location = HandLocation.Right;
                    //add "hand" to body part slot (left side)
                    _handSystem.AddHand(equipee, slotIdPrefix, location, sharedHandComp);
                }
            }
        }
    }
}
