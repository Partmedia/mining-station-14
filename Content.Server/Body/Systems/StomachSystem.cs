using Content.Server.Body.Components;
using Content.Server.Chemistry.Components.SolutionManager;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Body.Organ;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Utility;
using Content.Shared.Rejuvenate;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Server.Popups;

namespace Content.Server.Body.Systems
{
    public sealed class StomachSystem : EntitySystem
    {
        [Dependency] private readonly BodySystem _bodySystem = default!;
        [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;

        public const string DefaultSolutionName = "stomach";

        public override void Initialize()
        {
            SubscribeLocalEvent<StomachComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<StomachComponent, ApplyMetabolicMultiplierEvent>(OnApplyMetabolicMultiplier);
            SubscribeLocalEvent<StomachComponent, RejuvenateEvent>(OnRejuvenate);
        }

        public override void Update(float frameTime)
        {
            foreach (var (stomach, organ, sol)in EntityManager.EntityQuery<StomachComponent, OrganComponent, SolutionContainerManagerComponent>())
            {

                if (stomach.Working)
                {
                    stomach.AccumulatedFrameTime += frameTime;
                    stomach.IntervalLastChecked += frameTime;

                    if (stomach.AccumulatedFrameTime < stomach.UpdateInterval)
                        continue;

                    stomach.AccumulatedFrameTime -= stomach.UpdateInterval;

                    // Get our solutions
                    if (!_solutionContainerSystem.TryGetSolution(stomach.Owner, DefaultSolutionName,
                            out var stomachSolution, sol))
                        continue;

                    if (organ.Body is not { } body || !_solutionContainerSystem.TryGetSolution(body, stomach.BodySolutionName, out var bodySolution))
                        continue;

                    var transferSolution = new Solution();

                    var queue = new RemQueue<StomachComponent.ReagentDelta>();
                    foreach (var delta in stomach.ReagentDeltas)
                    {
                        delta.Increment(stomach.UpdateInterval);

                        //if the stomach accumulated more toxins than the build up threshold, increase the digestion delay
                        var toxinPenalty = 0f;
                        if (stomach.ToxinBuildUp > stomach.BuildUpThreshold)
                            toxinPenalty = stomach.ToxinBuildUp - stomach.BuildUpThreshold;

                        if (delta.Lifetime > stomach.DigestionDelay + toxinPenalty)
                        {
                            if (stomachSolution.TryGetReagent(delta.ReagentId, out var quant))
                            {
                                if (quant > delta.Quantity)
                                    quant = delta.Quantity;

                                _solutionContainerSystem.TryRemoveReagent((stomach).Owner, stomachSolution,
                                    delta.ReagentId, quant);
                                transferSolution.AddReagent(delta.ReagentId, quant);

                                //get meta group of reagent
                                if (_prototypeManager.TryIndex<ReagentPrototype>(delta.ReagentId, out var proto))
                                {

                                    var toxinFound = false;
                                    if (proto.Metabolisms != null)
                                    {
                                        foreach (var meta in proto.Metabolisms.Keys)
                                        {
                                            if (stomach.Toxins.Contains(meta))
                                            {
                                                toxinFound = true;
                                                break;
                                            }
                                        }
                                    }

                                    //if group is in the toxins list, add quant to toxin build up
                                    if (toxinFound)
                                        stomach.ToxinBuildUp += quant.Float();
                                }
                            }

                            queue.Add(delta);
                        }
                    }  

                    if (stomach.IntervalLastChecked >= stomach.RegenerationInterval)
                    {
                        stomach.ToxinBuildUp -= stomach.RegenerationAmount;

                        if (stomach.ToxinBuildUp < 0)
                            stomach.ToxinBuildUp = 0;

                        stomach.IntervalLastChecked = 0;
                    }

                    if (stomach.ToxinBuildUp >= stomach.ToxinThreshold)
                        stomach.Working = false;

                    UpdateStomachStatus(body, stomach);

                    foreach (var item in queue)
                    {
                        stomach.ReagentDeltas.Remove(item);
                    }

                    // Transfer everything to the body solution!
                    _solutionContainerSystem.TryAddSolution(body, bodySolution, transferSolution);
                }
            }
        }

        private void UpdateStomachStatus(EntityUid body, StomachComponent stomach)
        {
            //Update and signal damage
            if (!stomach.Working)
            {
                if (stomach.Condition != OrganCondition.Failure)
                    _popupSystem.PopupEntity("you feel a short sharp pain in your stomach", body, body); //TODO loc
                stomach.Condition = OrganCondition.Failure;
            }
            else if (stomach.ToxinBuildUp >= stomach.CriticalDamage)
            {
                if (stomach.Condition != OrganCondition.Critical)
                    _popupSystem.PopupEntity("you feel a sharp pain in your stomach", body, body); //TODO loc
                stomach.Condition = OrganCondition.Critical;
            }
            else if (stomach.ToxinBuildUp >= stomach.WarningDamage)
            {
                if (stomach.Condition != OrganCondition.Critical && stomach.Condition != OrganCondition.Warning)
                    _popupSystem.PopupEntity("you feel a slight pain in your stomach", body, body); //TODO loc
                stomach.Condition = OrganCondition.Warning;
            }
            else
            {
                stomach.Condition = OrganCondition.Good;
            }
        }

        private void OnApplyMetabolicMultiplier(EntityUid uid, StomachComponent component,
            ApplyMetabolicMultiplierEvent args)
        {
            if (args.Apply)
            {
                component.UpdateInterval *= args.Multiplier;
                return;
            }

            // This way we don't have to worry about it breaking if the stasis bed component is destroyed
            component.UpdateInterval /= args.Multiplier;
            // Reset the accumulator properly
            if (component.AccumulatedFrameTime >= component.UpdateInterval)
                component.AccumulatedFrameTime = component.UpdateInterval;
        }

        private void OnComponentInit(EntityUid uid, StomachComponent component, ComponentInit args)
        {
            _solutionContainerSystem.EnsureSolution(uid, DefaultSolutionName, component.InitialMaxVolume, out _);
        }

        public bool CanTransferSolution(EntityUid uid, Solution solution,
            SolutionContainerManagerComponent? solutions = null)
        {
            if (!Resolve(uid, ref solutions, false))
                return false;

            if (!_solutionContainerSystem.TryGetSolution(uid, DefaultSolutionName, out var stomachSolution, solutions))
                return false;

            // TODO: For now no partial transfers. Potentially change by design
            if (!stomachSolution.CanAddSolution(solution))
                return false;

            return true;
        }

        public bool TryTransferSolution(EntityUid uid, Solution solution,
            StomachComponent? stomach = null,
            SolutionContainerManagerComponent? solutions = null)
        {
            if (!Resolve(uid, ref stomach, ref solutions, false))
                return false;

            if (!_solutionContainerSystem.TryGetSolution(uid, DefaultSolutionName, out var stomachSolution, solutions)
                || !CanTransferSolution(uid, solution, solutions))
                return false;

            _solutionContainerSystem.TryAddSolution(uid, stomachSolution, solution);
            // Add each reagent to ReagentDeltas. Used to track how long each reagent has been in the stomach
            foreach (var reagent in solution.Contents)
            {
                stomach.ReagentDeltas.Add(new StomachComponent.ReagentDelta(reagent.ReagentId, reagent.Quantity));
            }

            return true;
        }

        private void OnRejuvenate(EntityUid uid, StomachComponent stomach, RejuvenateEvent args)
        {
            stomach.Working = true;
            stomach.ToxinBuildUp = 0f;
            stomach.Condition = OrganCondition.Good;
        }
    }
}
