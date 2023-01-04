using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.Components.SolutionManager;
using Content.Server.Nutrition.Components;
using Content.Server.Labels.Components;
using Content.Server.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
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
    /// <seealso cref="ChemAnalyzerComponent"/>
    /// </summary>
    [UsedImplicitly]
    public sealed class ChemAnalyzerSystem : EntitySystem
    {
        [Dependency] private readonly AudioSystem _audioSystem = default!;
        [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ChemAnalyzerComponent, ComponentStartup>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<ChemAnalyzerComponent, SolutionChangedEvent>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<ChemAnalyzerComponent, EntInsertedIntoContainerMessage>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<ChemAnalyzerComponent, EntRemovedFromContainerMessage>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<ChemAnalyzerComponent, BoundUIOpenedEvent>((_, comp, _) => UpdateUiState(comp));
        }

        private void UpdateUiState(ChemAnalyzerComponent ChemAnalyzer)
        {
            var outputContainer = _itemSlotsSystem.GetItemOrNull(ChemAnalyzer.Owner, SharedChemAnalyzer.OutputSlotName);
            var outputContainerInfo = BuildOutputContainerInfo(outputContainer, ChemAnalyzer);

            var state = new ChemAnalyzerBoundUserInterfaceState(outputContainerInfo);
            _userInterfaceSystem.TrySetUiState(ChemAnalyzer.Owner, ChemAnalyzerUiKey.Key, state);
        }

        private ContainerInfo? BuildOutputContainerInfo(EntityUid? container, ChemAnalyzerComponent ChemAnalyzer)
        {
            if (container is not { Valid: true })
                return null;

            AnalyzerSound(ChemAnalyzer);

            if (TryComp<SolutionContainerManagerComponent>(container, out var solutions))
                foreach (var solution in (solutions.Solutions)) //will only work on the first iter val
                {
                    var reagents = solution.Value.Contents.Select(reagent => (reagent.ReagentId, reagent.Quantity)).ToList();
                    return new ContainerInfo(Name(container.Value), true, solution.Value.Volume, solution.Value.MaxVolume, reagents);
                }

            return null;
        }

        private void AnalyzerSound(ChemAnalyzerComponent ChemAnalyzer)
        {
            _audioSystem.PlayPvs(ChemAnalyzer.ClickSound, ChemAnalyzer.Owner, AudioParams.Default.WithVolume(-2f));
        }

    }
}
