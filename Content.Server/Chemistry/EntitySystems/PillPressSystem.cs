using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Chemistry.Components;
using Content.Server.Labels;
using Content.Server.Labels.Components;
using Content.Server.Popups;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Utility;


namespace Content.Server.Chemistry.EntitySystems
{

    /// <summary>
    /// Contains all the server-side logic for PillPresss.
    /// <seealso cref="PillPressComponent"/>
    /// </summary>
    [UsedImplicitly]
    public sealed class PillPressSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly AudioSystem _audioSystem = default!;
        [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private readonly StorageSystem _storageSystem = default!;
        [Dependency] private readonly LabelSystem _labelSystem = default!;

        private const string PillPrototypeId = "Pill";

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<PillPressComponent, ComponentStartup>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<PillPressComponent, SolutionChangedEvent>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<PillPressComponent, EntInsertedIntoContainerMessage>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<PillPressComponent, EntRemovedFromContainerMessage>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<PillPressComponent, BoundUIOpenedEvent>((_, comp, _) => UpdateUiState(comp));

            SubscribeLocalEvent<PillPressComponent, PillPressSetPillTypeMessage>(OnSetPillTypeMessage);
            SubscribeLocalEvent<PillPressComponent, PillPressCreatePillsMessage>(OnCreatePillsMessage);
        }

        private void UpdateUiState(PillPressComponent pillPress, bool updateLabel = false)
        {
            var inputContainer = _itemSlotsSystem.GetItemOrNull(pillPress.Owner, SharedPillPress.InputSlotName);
            var outputContainer = _itemSlotsSystem.GetItemOrNull(pillPress.Owner, SharedPillPress.OutputSlotName);

            if (TryComp(pillPress.Owner, out AppearanceComponent? appearance))
            {
                appearance.SetData(SharedPillPress.PillPressVisualState.BeakerAttached, !(inputContainer is null));
                appearance.SetData(SharedPillPress.PillPressVisualState.OutputAttached, !(outputContainer is null));
            }

            var state = new PillPressBoundUserInterfaceState(
                BuildInputContainerInfo(inputContainer), BuildOutputContainerInfo(outputContainer),
                pillPress.PillType, pillPress.PillDosageLimit, updateLabel);

            _userInterfaceSystem.TrySetUiState(pillPress.Owner, PillPressUiKey.Key, state);
        }

        private void OnSetPillTypeMessage(EntityUid uid, PillPressComponent pillPress, PillPressSetPillTypeMessage message)
        {
            // Ensure valid pill type. There are 20 pills selectable, 0-19.
            if (message.PillType > SharedPillPress.PillTypes - 1)
                return;

            pillPress.PillType = message.PillType;
            UpdateUiState(pillPress);
            ClickSound(pillPress);
        }

        private void OnCreatePillsMessage(EntityUid uid, PillPressComponent pillPress, PillPressCreatePillsMessage message)
        {
            var user = message.Session.AttachedEntity;
            var maybeContainer = _itemSlotsSystem.GetItemOrNull(pillPress.Owner, SharedPillPress.OutputSlotName);
            if (maybeContainer is not { Valid: true } container
                || !TryComp(container, out ServerStorageComponent? storage)
                || storage.Storage is null)
            {
                return; // output can't fit pills
            }

            // Ensure the number is valid.
            if (message.Number == 0 || message.Number > storage.StorageCapacityMax - storage.StorageUsed)
                return;

            // Ensure the amount is valid.
            if (message.Dosage == 0 || message.Dosage > pillPress.PillDosageLimit)
                return;

            // Ensure label length is within the character limit.
            if (message.Label.Length > SharedPillPress.LabelMaxLength)
                return;

            var needed = message.Dosage * message.Number;
            if (!WithdrawFromBuffer(pillPress, needed, user, out var withdrawal))
                return;

            _labelSystem.Label(container, message.Label);

            for (var i = 0; i < message.Number; i++)
            {
                var item = Spawn(PillPrototypeId, Transform(container).Coordinates);
                _storageSystem.Insert(container, item, storage);
                _labelSystem.Label(item, message.Label);

                var itemSolution = _solutionContainerSystem.EnsureSolution(item, SharedPillPress.PillSolutionName);

                _solutionContainerSystem.TryAddSolution(
                    item, itemSolution, withdrawal.SplitSolution(message.Dosage));

                if (TryComp<SpriteComponent>(item, out var spriteComp))
                    spriteComp.LayerSetState(0, "pill" + (pillPress.PillType + 1));
            }

            UpdateUiState(pillPress);
            ClickSound(pillPress);
        }

        private bool WithdrawFromBuffer(
            IComponent pillPress,
            FixedPoint2 neededVolume, EntityUid? user,
            [NotNullWhen(returnValue: true)] out Solution? outputSolution)
        {
            outputSolution = null;

            var container = _itemSlotsSystem.GetItemOrNull(pillPress.Owner, SharedPillPress.InputSlotName);
            if (container is null)
                return false;
            
            if (!TryComp(container, out FitsInDispenserComponent? fits)
                || !_solutionContainerSystem.TryGetSolution(container.Value, fits.Solution, out var solution))
            {
                return false;
            }

            if (solution.Volume == 0)
            {
                if (user != null)
                    _popupSystem.PopupCursor(Loc.GetString("chem-master-window-buffer-empty-text"), user.Value);
                return false;
            }

            // ReSharper disable once InvertIf
            if (neededVolume > solution.Volume)
            {
                if (user != null)
                    _popupSystem.PopupCursor(Loc.GetString("chem-master-window-buffer-low-text"), user.Value);
                return false;
            }

            outputSolution = solution.SplitSolution(neededVolume);
            _solutionContainerSystem.UpdateAppearance(container.Value, solution);
            return true;
        }

        private void ClickSound(PillPressComponent pillPress)
        {
            _audioSystem.Play(pillPress.ClickSound, Filter.Pvs(pillPress.Owner), pillPress.Owner, false, AudioParams.Default.WithVolume(-2f));
        }

        private PillPressContainerInfo? BuildInputContainerInfo(EntityUid? container)
        {
            if (container is not { Valid: true })
                return null;

            if (!TryComp(container, out FitsInDispenserComponent? fits)
                || !_solutionContainerSystem.TryGetSolution(container.Value, fits.Solution, out var solution))
            {
                return null;
            }

            return BuildContainerInfo(Name(container.Value), solution);
        }

        private PillPressContainerInfo? BuildOutputContainerInfo(EntityUid? container)
        {
            if (container is not { Valid: true })
                return null;

            var name = Name(container.Value);

            if (!TryComp(container, out ServerStorageComponent? storage))
                return null;

            var pills = storage.Storage?.ContainedEntities.Select(pill =>
            {
                _solutionContainerSystem.TryGetSolution(pill, SharedPillPress.PillSolutionName, out var solution);
                var quantity = solution?.Volume ?? FixedPoint2.Zero;
                return (Name(pill), quantity);
            }).ToList();

            return pills is null
                ? null
                : new PillPressContainerInfo(name, false, storage.StorageUsed, storage.StorageCapacityMax, pills);
        }

        private static PillPressContainerInfo BuildContainerInfo(string name, Solution solution)
        {
            var reagents = solution.Contents
                .Select(reagent => (reagent.ReagentId, reagent.Quantity)).ToList();

            return new PillPressContainerInfo(name, true, solution.Volume, solution.MaxVolume, reagents);
        }
    }
}
