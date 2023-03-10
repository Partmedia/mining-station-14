using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Construction;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Server.Popups;
using Content.Shared.Containers.ItemSlots;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Server.Chemistry.Components.SolutionManager;
using Content.Shared.Audio;
using Robust.Shared.Audio;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.Atmos.Piping.Unary.EntitySystems
{

    [UsedImplicitly]
    public sealed class ReagentPumpSystem : EntitySystem
    {
        [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly AudioSystem _audioSystem = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ReagentPumpComponent, ComponentStartup>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<ReagentPumpComponent, SolutionChangedEvent>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<ReagentPumpComponent, EntInsertedIntoContainerMessage>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<ReagentPumpComponent, EntRemovedFromContainerMessage>((_, comp, _) => UpdateUiState(comp));

            SubscribeLocalEvent<ReagentPumpComponent, BoundUIOpenedEvent>((_, comp, _) => UpdateUiState(comp));

            SubscribeLocalEvent<ReagentPumpComponent, ReagentPumpSetModeMessage>(OnSetModeMessage);
            SubscribeLocalEvent<ReagentPumpComponent, ReagentPumpReagentAmountButtonMessage>(OnReagentButtonMessage);

            SubscribeLocalEvent<ReagentPumpComponent, ExtractMessage>(OnExtractMessage);
            SubscribeLocalEvent<ReagentPumpComponent, InjectMessage>(OnInjectMessage);
        }

        public override void Update(float frameTime)
        {

            foreach (var reagentPump in EntityQuery<ReagentPumpComponent>())
            {
                if (reagentPump.ExtractBusy)
                {
                    reagentPump.AccumulatedExtractRuntime += frameTime;
                    if (reagentPump.AccumulatedExtractRuntime >= reagentPump.ExtractRunTime)
                    {
                        if (this.IsPowered(reagentPump.Owner, EntityManager))
                            ExtractReagents(reagentPump.Owner, reagentPump);
                        reagentPump.AccumulatedExtractRuntime -= reagentPump.ExtractRunTime;
                        reagentPump.ExtractBusy = false;
                    }
                }

                if (reagentPump.InjectBusy)
                {
                    reagentPump.AccumulatedInjectRuntime += frameTime;
                    if (reagentPump.AccumulatedInjectRuntime >= reagentPump.InjectRunTime)
                    {
                        if (this.IsPowered(reagentPump.Owner, EntityManager))
                            InjectReagents(reagentPump.Owner, reagentPump);
                        reagentPump.AccumulatedInjectRuntime -= reagentPump.InjectRunTime;
                        reagentPump.InjectBusy = false;
                    }
                }

                // Don't update every tick
                reagentPump.AccumulatedUpdatetime += frameTime;

                if (reagentPump.AccumulatedUpdatetime >= reagentPump.UpdateInterval)
                {
                    reagentPump.AccumulatedUpdatetime -= reagentPump.UpdateInterval;
                    UpdateUiState(reagentPump);
                }
                
            }
        }

        private void UpdateUiState(ReagentPumpComponent ReagentPump)
        {
            if (!_solutionContainerSystem.TryGetSolution(ReagentPump.Owner, SharedReagentPump.BufferSolutionName, out var bufferSolution))
                return;

            var pipeSolution = new Solution();

            if (!TryComp<NodeContainerComponent>(ReagentPump.Owner, out var nodeContainer))
                return;
            
            if (nodeContainer.TryGetNode("pipe", out PipeNode? pipe))
                pipeSolution = pipe.Liquids;
            else
                return;

            var outputContainer = _itemSlotsSystem.GetItemOrNull(ReagentPump.Owner, SharedReagentPump.OutputSlotName);
            var outputContainerInfo = BuildContainerInfo(outputContainer, ReagentPump);

            var bufferReagents = bufferSolution.Contents;
            var pipeReagents = pipeSolution.Contents;
            var bufferCurrentVolume = bufferSolution.Volume;

            var state = new ReagentPumpBoundUserInterfaceState(ReagentPump.Mode,outputContainerInfo,bufferReagents,pipeReagents,bufferCurrentVolume);
            _userInterfaceSystem.TrySetUiState(ReagentPump.Owner, ReagentPumpUiKey.Key, state);
        }

        private void OnExtractMessage(EntityUid uid, ReagentPumpComponent ReagentPump, ExtractMessage message)
        {
            if (!this.IsPowered(ReagentPump.Owner, EntityManager))
                return;

            if (!ReagentPump.InjectBusy && !ReagentPump.ExtractBusy)
            {
                PumpSound(ReagentPump);
                ReagentPump.ExtractBusy = true;
            }
        }

        private void ExtractReagents(EntityUid uid, ReagentPumpComponent ReagentPump)
        {
            //take up to 300u (or whatever is set as the extraction amount) of each reagent from the pipe net and place it in to the buffer
            if (!_solutionContainerSystem.TryGetSolution(ReagentPump.Owner, SharedReagentPump.BufferSolutionName, out var bufferSolution))
                return;

            var pipeSolution = new Solution();

            if (!TryComp<NodeContainerComponent>(ReagentPump.Owner, out var nodeContainer))
                return;

            if (nodeContainer.TryGetNode("pipe", out PipeNode? pipe))
                pipeSolution = pipe.Liquids;
            else
                return; 

            var reagents = new List<Solution.ReagentQuantity>();
            foreach (var reagent in (pipeSolution))
            {
                reagents.Add(reagent);
            }

            if (reagents.Count > 0)
                FinishSound(ReagentPump);

            foreach (var reagent in reagents) 
            {
                var amount = FixedPoint2.Min(ReagentPump.ExtractionAmount, reagent.Quantity);
                pipeSolution.RemoveReagent(reagent.ReagentId, amount);
                bufferSolution.AddReagent(reagent.ReagentId, amount); 
            }

        }

        private void OnInjectMessage(EntityUid uid, ReagentPumpComponent ReagentPump, InjectMessage message)
        {
            if (!this.IsPowered(ReagentPump.Owner, EntityManager))
                return;

            if (!ReagentPump.InjectBusy && !ReagentPump.ExtractBusy)
            {
                PumpSound(ReagentPump);
                ReagentPump.InjectBusy = true;
            }
        }

        private void InjectReagents(EntityUid uid, ReagentPumpComponent ReagentPump)
        {
            //deposit all reagents in "output" container in to pipe net
            var container = _itemSlotsSystem.GetItemOrNull(ReagentPump.Owner, SharedReagentPump.OutputSlotName);
            if (container is null ||
                !TryComp<SolutionContainerManagerComponent>(container.Value, out var containerSolution) ||
                !_solutionContainerSystem.TryGetSolution(ReagentPump.Owner, SharedReagentPump.BufferSolutionName, out var bufferSolution))
                return;

            var pipeSolution = new Solution();

            if (!TryComp<NodeContainerComponent>(ReagentPump.Owner, out var nodeContainer))
                return;

            if (nodeContainer.TryGetNode("pipe", out PipeNode? pipe))
                pipeSolution = pipe.Liquids;
            else
                return;

            foreach (var solution in (containerSolution.Solutions)) //TODO make this better...
            {
                var reagents = solution.Value.Contents.Select(reagent => (reagent.ReagentId, reagent.Quantity)).ToList();

                if (reagents.Count > 0)
                    FinishSound(ReagentPump);

                foreach (var reagent in (reagents))
                {
                    var amount = solution.Value.GetReagentQuantity(reagent.ReagentId);
                    _solutionContainerSystem.TryRemoveReagent(container.Value, solution.Value, reagent.ReagentId, amount);
                    pipeSolution.AddReagent(reagent.ReagentId, amount);
                }
            }
        }

        private void OnSetModeMessage(EntityUid uid, ReagentPumpComponent ReagentPump, ReagentPumpSetModeMessage message)
        {
            // Ensure the mode is valid, either Transfer or Discard.
            if (!Enum.IsDefined(typeof(ReagentPumpMode), message.ReagentPumpMode))
                return;

            ReagentPump.Mode = message.ReagentPumpMode;
            UpdateUiState(ReagentPump);
            ClickSound(ReagentPump);
        }

        private void OnReagentButtonMessage(EntityUid uid, ReagentPumpComponent ReagentPump, ReagentPumpReagentAmountButtonMessage message)
        {
            // Ensure the amount corresponds to one of the reagent amount buttons.
            if (!Enum.IsDefined(typeof(ReagentPumpReagentAmount), message.Amount))
                return;

            switch (ReagentPump.Mode)
            {
                case ReagentPumpMode.Transfer:
                    TransferReagents(ReagentPump, message.ReagentId, message.Amount.GetFixedPoint(), message.FromBuffer);
                    break;
                case ReagentPumpMode.Discard:
                    DiscardReagents(ReagentPump, message.ReagentId, message.Amount.GetFixedPoint(),message.FromBuffer);
                    break;
                default:
                    // Invalid mode.
                    return;
            }

            ClickSound(ReagentPump);
        }

        private void TransferReagents(ReagentPumpComponent ReagentPump, string reagentId, FixedPoint2 amount, bool fromBuffer)
        {
            var container = _itemSlotsSystem.GetItemOrNull(ReagentPump.Owner, SharedReagentPump.OutputSlotName);
            if (container is null ||
                !TryComp<SolutionContainerManagerComponent>(container.Value, out var containerSolution) ||
                !_solutionContainerSystem.TryGetSolution(ReagentPump.Owner, SharedReagentPump.BufferSolutionName, out var bufferSolution))
                return;

            if (containerSolution is null)
                return;

            if (fromBuffer) // Buffer to container
            {
                foreach (var solution in (containerSolution.Solutions)) //TODO make this better...
                {
                    amount = FixedPoint2.Min(amount, solution.Value.AvailableVolume);
                    amount = bufferSolution.RemoveReagent(reagentId, amount);
                    _solutionContainerSystem.TryAddReagent(container.Value, solution.Value, reagentId, amount, out var _);
                }
            }
            else // Container to buffer
            {
                foreach (var solution in (containerSolution.Solutions)) //TODO make this better...
                {
                    amount = FixedPoint2.Min(amount, solution.Value.GetReagentQuantity(reagentId));
                    _solutionContainerSystem.TryRemoveReagent(container.Value, solution.Value, reagentId, amount);
                    bufferSolution.AddReagent(reagentId, amount);
                }
            }

            UpdateUiState(ReagentPump);
        }

        private void DiscardReagents(ReagentPumpComponent ReagentPump, string reagentId, FixedPoint2 amount, bool fromBuffer)
        {

            if (fromBuffer)
            {
                if (_solutionContainerSystem.TryGetSolution(ReagentPump.Owner, SharedReagentPump.BufferSolutionName, out var bufferSolution))
                    bufferSolution.RemoveReagent(reagentId, amount);
                else
                    return;
            }
            else
            {
                var container = _itemSlotsSystem.GetItemOrNull(ReagentPump.Owner, SharedReagentPump.OutputSlotName);
                if (container is not null &&
                    _solutionContainerSystem.TryGetFitsInDispenser(container.Value, out var containerSolution))
                {
                    _solutionContainerSystem.TryRemoveReagent(container.Value, containerSolution, reagentId, amount);
                }
                else
                    return;
            }

            UpdateUiState(ReagentPump);
        }

        private ReagentPumpContainerInfo? BuildContainerInfo(EntityUid? container, ReagentPumpComponent ReagentPump)
        {
            if (container is not { Valid: true })
                return null;

            if (TryComp<SolutionContainerManagerComponent>(container, out var solutions))
                foreach (var solution in (solutions.Solutions)) //will only work on the first iter val
                {
                    var reagents = solution.Value.Contents.Select(reagent => (reagent.ReagentId, reagent.Quantity)).ToList();
                    return new ReagentPumpContainerInfo(Name(container.Value), true, solution.Value.Volume, solution.Value.MaxVolume, reagents);
                }

            return null;
        }

        private void ClickSound(ReagentPumpComponent ReagentPump)
        {
            _audioSystem.PlayPvs(ReagentPump.ClickSound, ReagentPump.Owner, AudioParams.Default.WithVolume(-2f));
        }

        private void PumpSound(ReagentPumpComponent ReagentPump)
        {
            _audioSystem.PlayPvs(ReagentPump.PumpSound, ReagentPump.Owner, AudioParams.Default.WithVolume(-2f));
        }

        private void FinishSound(ReagentPumpComponent ReagentPump)
        {
            _audioSystem.PlayPvs(ReagentPump.FinishSound, ReagentPump.Owner, AudioParams.Default.WithVolume(-2f));
        }
    }
}
