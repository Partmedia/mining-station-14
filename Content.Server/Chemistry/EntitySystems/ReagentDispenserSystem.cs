using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.Components.SolutionManager;
using Content.Server.Nutrition.Components;
using Content.Server.Labels.Components;
using Content.Server.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Dispenser;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;

namespace Content.Server.Chemistry.EntitySystems
{
    /// <summary>
    /// Contains all the server-side logic for reagent dispensers.
    /// <seealso cref="ReagentDispenserComponent"/>
    /// </summary>
    [UsedImplicitly]
    public sealed class ReagentDispenserSystem : EntitySystem
    {
        [Dependency] private readonly AudioSystem _audioSystem = default!;
        [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly SolutionTransferSystem _solutionTransferSystem = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ReagentDispenserComponent, ComponentStartup>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<ReagentDispenserComponent, SolutionChangedEvent>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<ReagentDispenserComponent, EntInsertedIntoContainerMessage>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<ReagentDispenserComponent, EntRemovedFromContainerMessage>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<ReagentDispenserComponent, BoundUIOpenedEvent>((_, comp, _) => UpdateUiState(comp));
            //SubscribeLocalEvent<ReagentDispenserComponent, GotEmaggedEvent>(OnEmagged);

            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserSetDispenseAmountMessage>(OnSetDispenseAmountMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserDispenseReagentMessage>(OnDispenseReagentMessage);
            SubscribeLocalEvent<ReagentDispenserComponent, ReagentDispenserClearContainerSolutionMessage>(OnClearContainerSolutionMessage);

            SubscribeLocalEvent<ReagentDispenserComponent, MapInitEvent>(OnMapInit);
        }

        private void UpdateUiState(ReagentDispenserComponent reagentDispenser)
        {
            var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser.Owner, SharedReagentDispenser.OutputSlotName);
            var outputContainerInfo = BuildOutputContainerInfo(outputContainer);

            var inventory = GetInventory(reagentDispenser);

            var state = new ReagentDispenserBoundUserInterfaceState(outputContainerInfo, inventory, reagentDispenser.DispenseAmount);
            _userInterfaceSystem.TrySetUiState(reagentDispenser.Owner, ReagentDispenserUiKey.Key, state);
        }

        private ContainerInfo? BuildOutputContainerInfo(EntityUid? container)
        {
            if (container is not { Valid: true })
                return null;

            if (_solutionContainerSystem.TryGetFitsInDispenser(container.Value, out var solution))
            {
                var reagents = solution.Contents.Select(reagent => (reagent.ReagentId, reagent.Quantity)).ToList();
                return new ContainerInfo(Name(container.Value), true, solution.Volume, solution.MaxVolume, reagents);
            }

            return null;
        }

        private List<KeyValuePair<string, KeyValuePair<string, string>>> GetInventory(ReagentDispenserComponent reagentDispenser)
        {

            var inventory = new List<KeyValuePair<string, KeyValuePair<string, string>>>();

            for (var i = 0; i < reagentDispenser.NumSlots; i++)
            {
                var storageSlotId = ReagentDispenserComponent.BaseStorageSlotId + i;
                var storedContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser.Owner, storageSlotId);

                if (TryComp<DrinkComponent>(storedContainer, out var storedDrink))
                    storedDrink.Opened = true;
     
                FixedPoint2 quantity = 0f;
                if (TryComp<SolutionContainerManagerComponent>(storedContainer, out var storageSolutions))
                    foreach (var storeSol in (storageSolutions.Solutions))
                        foreach (var content in (storeSol.Value.Contents))
                            quantity += content.Quantity;

                var storedAmount = quantity + "u";

                if (EntityManager.TryGetComponent<LabelComponent?>(storedContainer, out var label))
                {
                    if (label.CurrentLabel != null)
                        inventory.Add(new KeyValuePair<string, KeyValuePair<string, string>>(storageSlotId, new KeyValuePair<string, string>(label.CurrentLabel, storedAmount)));
                    else
                        if (EntityManager.TryGetComponent<MetaDataComponent?>(storedContainer, out var metadata))
                            inventory.Add(new KeyValuePair<string, KeyValuePair<string, string>>(storageSlotId, new KeyValuePair<string, string>(metadata.EntityName, storedAmount)));
                }
                else
                    if (EntityManager.TryGetComponent<MetaDataComponent?>(storedContainer, out var metadata))
                        inventory.Add(new KeyValuePair<string, KeyValuePair<string, string>>(storageSlotId, new KeyValuePair<string, string>(metadata.EntityName, storedAmount)));
            }

            return inventory;
        }

        private void OnSetDispenseAmountMessage(EntityUid uid, ReagentDispenserComponent reagentDispenser, ReagentDispenserSetDispenseAmountMessage message)
        {
            reagentDispenser.DispenseAmount = message.ReagentDispenserDispenseAmount;
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnDispenseReagentMessage(EntityUid uid, ReagentDispenserComponent reagentDispenser, ReagentDispenserDispenseReagentMessage message)
        {
            // Ensure that the reagent is something this reagent dispenser can dispense.
            var storedContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser.Owner, message.SlotId);
            if (storedContainer == null)
                return;

            var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser.Owner, SharedReagentDispenser.OutputSlotName);
            if (outputContainer is not {Valid: true} || !_solutionContainerSystem.TryGetFitsInDispenser(outputContainer.Value, out var solution))
                return;

            //run solution transfer from specified storage container to output container
            //this may need to be adjusted should there ever be multiple "solutions" in a container - as it is there only ever appears to be one
            if (TryComp<SolutionContainerManagerComponent>(storedContainer, out var storageSolutions) && TryComp<SolutionContainerManagerComponent>(outputContainer, out var outputSolutions))
                foreach (var outSol in (outputSolutions.Solutions))
                    foreach (var storeSol in (storageSolutions.Solutions))
                        _solutionTransferSystem.Transfer(uid, storedContainer.Value, storeSol.Value, outputContainer.Value, outSol.Value, (int)reagentDispenser.DispenseAmount);

            //if (_solutionContainerSystem.TryAddReagent(outputContainer.Value, solution, reagentId, (int)reagentDispenser.DispenseAmount, out var dispensedAmount)
            //    && message.Session.AttachedEntity is not null)
            //{
            //    TODO check if this is necessary (admin log already in transfer function)
            //    _adminLogger.Add(LogType.ChemicalReaction, LogImpact.Medium,
            //        $"{ToPrettyString(message.Session.AttachedEntity.Value):player} dispensed {dispensedAmount}u of {reagentId} into {ToPrettyString(outputContainer.Value):entity}");
            //}

            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void OnClearContainerSolutionMessage(EntityUid uid, ReagentDispenserComponent reagentDispenser, ReagentDispenserClearContainerSolutionMessage message)
        {
            var outputContainer = _itemSlotsSystem.GetItemOrNull(reagentDispenser.Owner, SharedReagentDispenser.OutputSlotName);
            if (outputContainer is not {Valid: true} || !_solutionContainerSystem.TryGetFitsInDispenser(outputContainer.Value, out var solution))
                return;

            _solutionContainerSystem.RemoveAllSolution(outputContainer.Value, solution);
            UpdateUiState(reagentDispenser);
            ClickSound(reagentDispenser);
        }

        private void ClickSound(ReagentDispenserComponent reagentDispenser)
        {
            _audioSystem.PlayPvs(reagentDispenser.ClickSound, reagentDispenser.Owner, AudioParams.Default.WithVolume(-2f));
        }

        private void OnMapInit(EntityUid uid, ReagentDispenserComponent component, MapInitEvent args)
        {

            //get list of pre-loaded containers
            List<string> preLoad = new List<string>();
            List<string> preLoadLabels = new List<string>();
            if (component.PackPrototypeId is not null
                && _prototypeManager.TryIndex(component.PackPrototypeId, out ReagentDispenserInventoryPrototype? packPrototype))
            {
                preLoad.AddRange(packPrototype.Inventory);
                preLoadLabels.AddRange(packPrototype.Labels);
            }

            //populate storage slots with base storage slot whitelist
            for (var i = 0; i < component.NumSlots; i++)
            {
                var storageSlotId = ReagentDispenserComponent.BaseStorageSlotId + i;
                ItemSlot storageComponent = new();
                storageComponent.Whitelist = component.StorageWhitelist;
                storageComponent.Swap = false;
                storageComponent.EjectOnBreak = true;

                //check corresponding index in pre-loaded container (if exists) and set starting item
                if (i < preLoad.Count)
                    storageComponent.StartingItem = preLoad[i];

                component.StorageSlotIds.Add(storageSlotId);
                component.StorageSlots.Add(storageComponent);
                component.StorageSlots[i].Name = "Storage Slot " + (i+1);
                //Console.WriteLine(component.StorageSlotIds[i]);
                _itemSlotsSystem.AddItemSlot(uid, component.StorageSlotIds[i], component.StorageSlots[i]);

                //re-label item for brevity (if labels provided)
                if (i < preLoadLabels.Count) {
                    var storedContainer = _itemSlotsSystem.GetItemOrNull(component.Owner, storageSlotId);
                    if (storedContainer != null)
                    {
                        var label = EnsureComp<LabelComponent>(storedContainer.Value);
                        label.CurrentLabel = preLoadLabels[i];
                    }
                }
            }

            _itemSlotsSystem.AddItemSlot(uid, ReagentDispenserComponent.BeakerSlotId, component.BeakerSlot);
        }
    }
}
