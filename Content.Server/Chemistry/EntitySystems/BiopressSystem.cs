using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Administration;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.Components.SolutionManager;
using Content.Server.Popups;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Biopress;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Content.Shared.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Robust.Shared.Prototypes;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Server.Destructible;

namespace Content.Server.Chemistry.EntitySystems
{

    /// <summary>
    /// Contains all the server-side logic for Biopresss.
    /// <seealso cref="BiopressComponent"/>
    /// </summary>
    [UsedImplicitly]
    public sealed class BiopressSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly AudioSystem _audioSystem = default!;
        [Dependency] private readonly SharedAudioSystem _sharedAudioSystem = default!;
        [Dependency] private readonly SharedAmbientSoundSystem _ambientSoundSystem = default!;
        [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private readonly StorageSystem _storageSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IGamePrototypeLoadManager _gamePrototypeLoadManager = default!;
        [Dependency] private readonly SharedJitteringSystem _jitteringSystem = default!;
        [Dependency] private readonly EntityStorageSystem _entityStorageSystem = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;

        private Queue<BiopressComponent> _uiUpdateQueue = new();
        private Queue<EntityUid> _activeQueue = new();
        private Queue<EntityUid> _checkQueue = new();

        /// <summary>
        ///     A cache of all existant chemical reactions indexed by their resulting reagent.
        /// </summary>
        private IDictionary<string, List<ReactionPrototype>> _reactions = default!;
        /// <summary>
        ///     A cache of all existant molecule groups for some compound reagents not produced by recipes.
        /// </summary>
        private IDictionary<string, List<MoleculeGroupPrototype>> _moleculeGroups = default!;

        public override void Update(float frameTime)
        {

            base.Update(frameTime);

            foreach (var uid in _activeQueue)
            {
                if (!TryComp<BiopressComponent>(uid, out var biopress))
                    continue;

                biopress.ProcessingTimer += frameTime;

                if (biopress.Active && biopress.ProcessingTimer >= biopress.IntervalTime)
                {
                    //check current stage, run appropriate function
                    switch (biopress.Stage)
                    {
                        case BiopressStage.Initial:
                            {
                                HandleSmallMatter(uid, biopress);
                                break;
                            }
                        case BiopressStage.SmallMatter:
                            {
                                var smallMatter = CheckSmallMatter(uid, biopress); //and not so small... for now

                                if (!smallMatter)
                                {
                                    var largeMatter = CheckLargeMatter(uid, biopress);

                                    if (largeMatter)
                                        HandleLargeMatter(uid, biopress);
                                    else
                                        FinalStage(uid, biopress);
                                } else
                                {
                                    HandleSmallMatter(uid, biopress);
                                }

                                break;
                            }
                        case BiopressStage.LargeMatter:
                            {
                                var largeMatter = CheckLargeMatter(uid, biopress);

                                if (largeMatter)
                                    HandleLargeMatter(uid, biopress);
                                else
                                    HandleSmallMatter(uid, biopress);

                                break;
                            }
                        case BiopressStage.Final:
                            {
                                biopress.Active = false;
                                break;
                            }
                    }
                }

                _checkQueue.Enqueue(uid);
            }

            _activeQueue.Clear();

            foreach (var uid in _checkQueue)
            {
                if (!TryComp<BiopressComponent>(uid, out var biopress))
                {
                    AfterShutdown(uid);
                    continue;
                }

                if (biopress.Active)
                    _activeQueue.Enqueue(uid);
                else
                    AfterShutdown(uid);
            }

            _checkQueue.Clear();

        }

        public override void Initialize()
        {
            base.Initialize();

            InitializeReactionCache();

            _prototypeManager.PrototypesReloaded += OnPrototypesReloadedReactions;
            _prototypeManager.PrototypesReloaded += OnPrototypesReloadedMoleculeGroups;

            SubscribeLocalEvent<BiopressComponent, ComponentStartup>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<BiopressComponent, SolutionChangedEvent>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<BiopressComponent, EntInsertedIntoContainerMessage>((_, comp, _) => UpdateUiState(comp));
            SubscribeLocalEvent<BiopressComponent, EntRemovedFromContainerMessage>((_, comp, _) => UpdateUiState(comp));

            SubscribeLocalEvent<BiopressComponent, PowerChangedEvent>(OnPowerChange);
            SubscribeLocalEvent<BiopressComponent, ContainerIsRemovingAttemptEvent>(OnEntRemoveAttempt);

            SubscribeLocalEvent<BiopressComponent, BoundUIOpenedEvent>((_, comp, _) => UpdateUiState(comp));

            SubscribeLocalEvent<BiopressComponent, BiopressSetModeMessage>(OnSetModeMessage);
            SubscribeLocalEvent<BiopressComponent, BiopressReagentAmountButtonMessage>(OnReagentButtonMessage);

            SubscribeLocalEvent<BiopressComponent, BiopressActivateButtonMessage>(OnActivateButtonMessage);
            SubscribeLocalEvent<BiopressComponent, BiopressStopButtonMessage>(OnStopButtonMessage);

            SubscribeLocalEvent<BiopressComponent, BiopressStoreToggleButtonMessage>(OnStoreToggleButtonMessage);

            SubscribeLocalEvent<BiopressComponent, StorageBeforeOpenEvent>(OnStorageOpened);

        }

        /// <summary>
        ///     Handles building the reaction cache.
        /// </summary>
        private void InitializeReactionCache()
        {
            _reactions = new Dictionary<string, List<ReactionPrototype>>();

            var reactions = _prototypeManager.EnumeratePrototypes<ReactionPrototype>();
            foreach (var products in reactions)
            {
                CacheReaction(products);
            }

            _moleculeGroups = new Dictionary<string, List<MoleculeGroupPrototype>>();

            var moleculeGroups = _prototypeManager.EnumeratePrototypes<MoleculeGroupPrototype>();
            foreach (var group in moleculeGroups)
            {
                CacheMoleculeGroup(group);
            }
        }

        private void OnPrototypesReloadedReactions(PrototypesReloadedEventArgs eventArgs)
        {
            if (!eventArgs.ByType.TryGetValue(typeof(ReactionPrototype), out var set))
                return;

            foreach (var (reactant, cache) in _reactions)
            {
                cache.RemoveAll((reaction) => set.Modified.ContainsKey(reaction.ID));
                if (cache.Count == 0)
                    _reactions.Remove(reactant);
            }

            foreach (var prototype in set.Modified.Values)
            {
                CacheReaction((ReactionPrototype) prototype);
            }
        }

        private void OnPrototypesReloadedMoleculeGroups(PrototypesReloadedEventArgs eventArgs)
        {

            if (!eventArgs.ByType.TryGetValue(typeof(MoleculeGroupPrototype), out var set))
                return;

            foreach (var (reagentProportions, cache) in _moleculeGroups)
            {
                cache.RemoveAll((moleculeGroup) => set.Modified.ContainsKey(moleculeGroup.ID));
                if (cache.Count == 0)
                    _moleculeGroups.Remove(reagentProportions);
            }

            foreach (var prototype in set.Modified.Values)
            {
                CacheMoleculeGroup((MoleculeGroupPrototype) prototype);
            }
        }

        private void CacheReaction(ReactionPrototype reaction)
        {
            var reagents = reaction.Products.Keys;
            foreach (var reagent in reagents)
            {
                if (!_reactions.TryGetValue(reagent, out var cache))
                {
                    cache = new List<ReactionPrototype>();
                    _reactions.Add(reagent, cache);
                }

                cache.Add(reaction);
                return; // Only need to cache based on the first reagent.
            }
        }

        private void CacheMoleculeGroup(MoleculeGroupPrototype moleculeGroup)
        {
            var groupId = moleculeGroup.ID;

            if (!_moleculeGroups.TryGetValue(groupId, out var cache))
            {
                cache = new List<MoleculeGroupPrototype>();
                _moleculeGroups.Add(groupId, cache);
            }

            cache.Add(moleculeGroup);

        }

        /// <summary>
        ///     work similar to the centrifuge electrolysis, except that it keeps going until only base elements remains  
        /// </summary>
        private List<Solution.ReagentQuantity> BreakdownReagents(List<Solution.ReagentQuantity> reagents)
        {
            List<Solution.ReagentQuantity> tempList = new List<Solution.ReagentQuantity>();
            List<Solution.ReagentQuantity> finalList = new List<Solution.ReagentQuantity>();

            foreach (var reagent in (reagents))
            {
                if (_reactions.TryGetValue(reagent.ReagentId, out var productReactions)) //typically only one of these...
                {
                    foreach (var reaction in productReactions)
                    {
                        FixedPoint2 totalCoeff = 0f;
                        foreach (var reactant in reaction.Reactants)
                        {
                            totalCoeff += reactant.Value.Amount;
                        }
                        foreach (var reactant in reaction.Reactants)
                        {
                            var name = reactant.Key;
                            var coeff = reactant.Value.Amount;
                            var amount = (reagent.Quantity / totalCoeff) * coeff;

                            if (!reactant.Value.Catalyst)
                            {
                                Solution.ReagentQuantity newReagent = new Solution.ReagentQuantity(name,amount);
                                tempList.Add(newReagent);
                            }
                        }
                    }
                }
                else if (_prototypeManager.TryIndex(reagent.ReagentId, out ReagentPrototype? p) && _moleculeGroups.TryGetValue(p.MoleculeGroup, out var productMoleculeGroups))
                {
                    
                    foreach (var productMolecules in productMoleculeGroups)
                    {
                        FixedPoint2 totalCoeff = 0f;
                        if (productMolecules.ReagentProportions != null)
                        {
                            foreach (KeyValuePair<string, FixedPoint2> molecule in productMolecules.ReagentProportions)
                            {
                                totalCoeff += molecule.Value;
                            }
                            foreach (KeyValuePair<string, FixedPoint2> molecule in productMolecules.ReagentProportions)
                            {
                                var name = molecule.Key;
                                var coeff = molecule.Value;
                                var amount = (reagent.Quantity / totalCoeff) * coeff;
                                Solution.ReagentQuantity newReagent = new Solution.ReagentQuantity(name, amount);

                                if (amount > 0.01) //having issues with infinite loops, placing an acceptance limit
                                {
                                    tempList.Add(newReagent);
                                }
                            }
                        }
                    }
                }
                else
                {
                    finalList.Add(reagent);
                }
            }

            if (tempList.Count > 0)
                finalList.AddRange(BreakdownReagents(tempList));

            return finalList;
        }

        /// <summary>
        ///     Check entities for bio harvest component
        ///     Get all reagents from prototype multiplied by totalReagentUnits
        /// </summary>
        private List<Solution.ReagentQuantity> RunBioHarvest(List<EntityUid> entities) {

            List<Solution.ReagentQuantity> reagentList = new List<Solution.ReagentQuantity>();

            foreach (var uid in entities)
            {
                if (TryComp(uid, out BiopressHarvestComponent? biopressHarvest) && biopressHarvest.BioReagentGroupId != null)
                {
                    if (!(TryComp(uid, out MobStateComponent? mobState) && mobState.CurrentState != MobState.Dead))
                    {
                        if (_prototypeManager.TryIndex(biopressHarvest.BioReagentGroupId, out BioReagentGroupPrototype? group) && group.ReagentProportions != null)
                        {
                            foreach (KeyValuePair<string, FixedPoint2> reagent in group.ReagentProportions)
                            {
                                Solution.ReagentQuantity newReagent = new Solution.ReagentQuantity(reagent.Key, reagent.Value * biopressHarvest.TotalReagentUnits);
                                reagentList.Add(newReagent);
                            }
                        }
                    }
                }
            }

            reagentList = BreakdownReagents(reagentList);

            return reagentList;
        }

        /// <summary>
        ///     Get all entities in container
        ///     Get all entities in containers that are also in this container EXCEPT if the container is ALIVE
        ///     (or if live-able and does NOT have the BiopressHarvest component)    
        /// </summary>
        private Tuple<List<EntityUid>, List<Solution.ReagentQuantity>, List<EntityUid>> GetContainerEntitities(EntityUid uid)
        {
            List<EntityUid> entityList = new List<EntityUid>();
            List<Solution.ReagentQuantity> reagentList = new List<Solution.ReagentQuantity>();
            List<EntityUid> moveList = new List<EntityUid>();

            //first check if the container is capable of living (has the MobState component)
            if (TryComp(uid, out MobStateComponent? mobState))
            {
                //then check if it is alive or not
                if (mobState.CurrentState != MobState.Dead)
                    return Tuple.Create(entityList, reagentList,moveList);
                //if it can live but is dead, check if it has the BiopressHarvest component (if it does, continue)
                else if (!TryComp(uid, out BiopressHarvestComponent? biopressHarvest))
                    return Tuple.Create(entityList, reagentList, moveList);
            }

            //Check for entity storage
            if (TryComp(uid, out EntityStorageComponent? container))
            {
                //relocate any living (or potentially living) entities out to the mainContainer
                var containedEntities = new List<EntityUid>();
                
                foreach (var entityUid in container.Contents.ContainedEntities)
                {
                    if (TryComp(entityUid, out MobStateComponent? containedMob))
                    {
                        moveList.Add(entityUid);
                    }
                    else
                    {
                        containedEntities.Add(entityUid);
                    }
                }

                //iterate through container items
                foreach (var entityUid in containedEntities)
                {
                    entityList.Add(entityUid);
                    var entityTuple = GetContainerEntitities(entityUid);
                    entityList.AddRange(entityTuple.Item1);
                    reagentList.AddRange(entityTuple.Item2);
                    moveList.AddRange(entityTuple.Item3);
                }
            }

            //Check for container manager
            if (TryComp(uid, out ContainerManagerComponent? containers))
            {
                foreach (KeyValuePair<string, IContainer> cont in containers.Containers) //should be only one
                {
                    var containedEntities = new List<EntityUid>();
                    //relocate any living (or potentially living) entities out to the mainContainer
                    foreach (var entityUid in cont.Value.ContainedEntities)
                    {
                        if (TryComp(entityUid, out MobStateComponent? containedMob))
                        {
                            moveList.Add(entityUid);
                        } else
                        {
                            containedEntities.Add(entityUid);
                        }

                    }

                    //iterate through container items
                    foreach (var entityUid in containedEntities)
                    {
                        entityList.Add(entityUid);
                        var entityTuple = GetContainerEntitities(entityUid);
                        entityList.AddRange(entityTuple.Item1);
                        reagentList.AddRange(entityTuple.Item2);
                        moveList.AddRange(entityTuple.Item3);
                    }
                }
            }

            //check for solution container manager
            if (TryComp<SolutionContainerManagerComponent>(uid, out var solutions))
            {
                foreach (var solution in (solutions.Solutions))
                    reagentList.AddRange(solution.Value.Contents);
            }

            return Tuple.Create(entityList,reagentList,moveList);
        }

        /// <summary>
        ///     Applies Slash damage to all damageable entities inside hopper, then
        ///     Remove all non-living, non-gibbable entities and generate reagents for buffer
        /// </summary>
        private void HandleSmallMatter(EntityUid uid, BiopressComponent biopress) {
            biopress.ProcessingTimer = 0;
            biopress.Stage = BiopressStage.SmallMatter;

            if (!_solutionContainerSystem.TryGetSolution(uid, SharedBiopress.BufferSolutionName, out var bufferSolution))
            {
                biopress.Stage = BiopressStage.Final;
                return;
            }

            SoundSystem.Play(biopress.GrindSound.GetSound(), Filter.Pvs(uid), uid, AudioParams.Default);

            List<EntityUid> entityList = new List<EntityUid>();
            List<Solution.ReagentQuantity> reagentList = new List<Solution.ReagentQuantity>();
            List<Solution.ReagentQuantity> finalReagentList = new List<Solution.ReagentQuantity>();
            List<EntityUid> moveList = new List<EntityUid>();

            //Check for entity storage
            if (TryComp(uid, out EntityStorageComponent? container))
            {
                //iterate through container items (using recursion to find contained containers)
                var containedEntities = container.Contents.ContainedEntities;
                foreach (var entityUid in containedEntities) {
                    entityList.Add(entityUid);
                    var entityTuple = GetContainerEntitities(entityUid);
                    entityList.AddRange(entityTuple.Item1);
                    reagentList.AddRange(entityTuple.Item2);
                    moveList.AddRange(entityTuple.Item3);
                }
            }

            foreach (var entityUid in moveList)
            {
               _entityStorageSystem.Insert(entityUid, uid);
            }

            //next, apply slash damage to all damageable entities
            //reassess their containers if needed
            List<EntityUid> killed = new List<EntityUid>();

            foreach (var entityUid in entityList)
            {
                if (TryComp(entityUid, out DamageableComponent? damageable))
                {
                    _damageableSystem.TryChangeDamage(entityUid, biopress.SmallDamage, ignoreResistances: false);
                    if (TryComp(entityUid, out MobStateComponent? mobState))
                    {
                        if (mobState.CurrentState == MobState.Dead)
                            killed.Add(entityUid);
                    }
                }
            }

            foreach (var entityUid in killed)
            {
                var entityTuple = GetContainerEntitities(entityUid);
                entityList.AddRange(entityTuple.Item1);
                reagentList.AddRange(entityTuple.Item2);
            }

            //get all reagents and render them to their base elements (with another recursive function)
            finalReagentList.AddRange(BreakdownReagents(reagentList));

            //convert all entities with the biopressHarvest component in to their constituent reagents
            finalReagentList.AddRange(RunBioHarvest(entityList));

            //remove all biopressHarvest entities
            //remove all non-organic entities
            //replace non-organics in to junk
            foreach (var entityUid in entityList)
            {
                //skip if living mob
                if (TryComp(entityUid, out MobStateComponent? mobState))
                {
                    if (!TryComp(entityUid, out BiopressHarvestComponent? biopressHarvestCheck))
                        continue;
                    if (mobState.CurrentState != MobState.Dead)
                        continue;
                }

                if (!TryComp(entityUid, out BiopressHarvestComponent? biopressHarvest))
                {
                    if (TryComp(entityUid, out TransformComponent? transform))
                    {
                            var coordinates = Transform(entityUid).Coordinates;
                            //spawn junk
                            var ent = EntityManager.SpawnEntity("junk", coordinates); //TODO make junk a component var 
                            _entityStorageSystem.Insert(ent, uid);    
                    }
                }

                EntityManager.DeleteEntity(entityUid);
            }

            //reagents are added to the buffer
            foreach (var reagent in finalReagentList)
            {
                bufferSolution.AddReagent(reagent.ReagentId, reagent.Quantity);
            }

            UpdateUiState(biopress);
        }

        /// <summary>
        ///     Applies Blunt damage to all damageable entities inside hopper
        /// </summary>    
        private void HandleLargeMatter(EntityUid uid, BiopressComponent biopress) {
            biopress.ProcessingTimer = 0;
            biopress.Stage = BiopressStage.LargeMatter;
            SoundSystem.Play(biopress.HydraulicSound.GetSound(), Filter.Pvs(uid), uid, AudioParams.Default);

            if (TryComp(uid, out EntityStorageComponent? container))
            {
                List<EntityUid> entityList = new List<EntityUid>();
                foreach (var entityUid in container.Contents.ContainedEntities)
                {
                    if (TryComp(entityUid, out DamageableComponent? mobState))
                    {
                        entityList.Add(entityUid);
                    }
                }
                foreach (var entityUid in entityList)
                {
                    _damageableSystem.TryChangeDamage(entityUid, biopress.LargeDamage, ignoreResistances: false, unblockable: true);
                }
            }
        }

        /// <summary>
        ///     Check for "non-gibbable" but living entities in hopper
        /// </summary> 
        private bool CheckSmallMatter(EntityUid uid, BiopressComponent biopress)
        {

            if (TryComp(uid, out EntityStorageComponent? container))
            {
                foreach (var entityUid in container.Contents.ContainedEntities)
                {
                    if (TryComp(entityUid, out MobStateComponent? mobState))
                    {
                        if (mobState.CurrentState != MobState.Dead && TryComp(entityUid, out BiopressHarvestComponent? biopressHarvest))
                            return true;
                    }

                }
            }

            return false;
        }

        /// <summary>
        ///     Check for gibbable entities in hopper
        /// </summary> 
        private bool CheckLargeMatter(EntityUid uid, BiopressComponent biopress) {

            if (TryComp(uid, out EntityStorageComponent? container))
            {
                foreach (var entityUid in container.Contents.ContainedEntities)
                {
                    if (TryComp(entityUid, out MobStateComponent? mobState))
                    {
                        if (TryComp(entityUid, out DestructibleComponent? destructible) && !TryComp(entityUid, out BiopressHarvestComponent? biopressHarvest))
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     Remove remaining entities, add ashes to hopper for each junk * AshFactor rounded
        /// </summary> 
        private void FinalStage(EntityUid uid, BiopressComponent biopress) {
            biopress.ProcessingTimer = 0;
            biopress.Stage = BiopressStage.Final;
            SoundSystem.Play(biopress.IncinerateSound.GetSound(), Filter.Pvs(uid), uid, AudioParams.Default);

            List<EntityUid> entityList = new List<EntityUid>();

            if (TryComp(uid, out EntityStorageComponent? container))
            {
                //iterate through container items (using recursion to find contained containers)
                var containedEntities = container.Contents.ContainedEntities;
                foreach (var entityUid in containedEntities)
                {
                    entityList.Add(entityUid);
                }
            } else
                return;

            foreach (var entityUid in entityList)
            {
                //remove junk
                EntityManager.DeleteEntity(entityUid);
            }

            var numAshes = Math.Floor(entityList.Count * biopress.AshFactor); //floor to prevent infinite ashes (unless 1 or above)

            for (var i = 0; i < numAshes; i++)
            {
                //add ashes
                var coordinates = Transform(uid).Coordinates;
                var ent = EntityManager.SpawnEntity("Ash", coordinates);
                _entityStorageSystem.Insert(ent, uid);
            }

        }

        private void OnPowerChange(EntityUid uid, BiopressComponent component, ref PowerChangedEvent args)
        {
            EnqueueUiUpdate(component);
            if (!this.IsPowered(component.Owner, EntityManager) && component.Active)
                component.Active = false;
        }

        private void OnEntRemoveAttempt(EntityUid uid, BiopressComponent component, ContainerIsRemovingAttemptEvent args)
        {
            if (component.Active)
                args.Cancel();
        }

        private void EnqueueUiUpdate(BiopressComponent component)
        {
            if (!_uiUpdateQueue.Contains(component)) _uiUpdateQueue.Enqueue(component);
        }

        private void UpdateUiState(BiopressComponent Biopress)
        {

            if (!_solutionContainerSystem.TryGetSolution(Biopress.Owner, SharedBiopress.BufferSolutionName, out var bufferSolution))
                return;

            var outputContainer = _itemSlotsSystem.GetItemOrNull(Biopress.Owner, SharedBiopress.OutputSlotName);

            /*if (TryComp(Biopress.Owner, out AppearanceComponent? appearance))
            {
                appearance.SetData(SharedBiopress.BiopressVisualState.OutputAttached, Biopress.OutputSlot.HasItem);
            }*/

            var bufferReagents = bufferSolution.Contents;
            var bufferCurrentVolume = bufferSolution.Volume;

            var state = new BiopressBoundUserInterfaceState(
                Biopress.Mode, BuildContainerInfo(outputContainer),
                bufferReagents, bufferCurrentVolume);

            _userInterfaceSystem.TrySetUiState(Biopress.Owner, BiopressUiKey.Key, state);
        }

        private void OnSetModeMessage(EntityUid uid, BiopressComponent Biopress, BiopressSetModeMessage message)
        {
            // Ensure the mode is valid, either Transfer or Discard.
            if (!Enum.IsDefined(typeof(BiopressMode), message.BiopressMode))
                return;

            Biopress.Mode = message.BiopressMode;
            UpdateUiState(Biopress);
            ClickSound(Biopress);
        }

        private void OnStorageOpened(EntityUid uid, BiopressComponent component, StorageBeforeOpenEvent args)
        {
            if (component.Active)
                component.Active = false;
        }

        private void OnStoreToggleButtonMessage(EntityUid uid, BiopressComponent Biopress, BiopressStoreToggleButtonMessage message)
        {
            if (!TryComp<EntityStorageComponent>(uid, out var storage))
                return;

            if (storage.Open)
                _entityStorageSystem.CloseStorage(uid, storage);
            else 
                _entityStorageSystem.OpenStorage(uid, storage);
        }

        private void OnReagentButtonMessage(EntityUid uid, BiopressComponent Biopress, BiopressReagentAmountButtonMessage message)
        {
            // Ensure the amount corresponds to one of the reagent amount buttons.
            if (!Enum.IsDefined(typeof(BiopressReagentAmount), message.Amount))
                return;

            switch (Biopress.Mode)
            {
                case BiopressMode.Transfer:
                    TransferReagents(Biopress, message.ReagentId, message.Amount.GetFixedPoint(), SharedBiopress.OutputSlotName, message.FromBuffer);
                    break;
                case BiopressMode.Discard:
                    DiscardReagents(Biopress, message.ReagentId, message.Amount.GetFixedPoint(), SharedBiopress.OutputSlotName, message.FromBuffer);
                    break;
                default:
                    // Invalid mode.
                    return;
            }

            ClickSound(Biopress);
        }

        private void AfterShutdown(EntityUid uid)
        {
            RemComp<JitteringComponent>(uid);
            _ambientSoundSystem.SetAmbience(uid, false);
        }

        private void OnActivateButtonMessage(EntityUid uid, BiopressComponent component, BiopressActivateButtonMessage message)
        {

            if (!TryComp<EntityStorageComponent>(uid, out var storage))
                return;

            if (!this.IsPowered(component.Owner, EntityManager) ||
                component.Active ||
                storage.Open)
                return;

            ClickSound(component);
            component.Active = true;
            component.ProcessingTimer = 0;

            _jitteringSystem.AddJitter(uid, -95, 25);
            _sharedAudioSystem.PlayPvs("/Audio/Machines/reclaimer_startup.ogg", uid);
            _ambientSoundSystem.SetAmbience(uid, true);

            _activeQueue.Enqueue(uid);

            component.Stage = BiopressStage.Initial;

            UpdateUiState(component);
        }

        private void OnStopButtonMessage(EntityUid uid, BiopressComponent component, BiopressStopButtonMessage message)
        {
            ClickSound(component);

            if (!this.IsPowered(component.Owner, EntityManager) ||
                !component.Active)
                return;

            component.Active = false;

            UpdateUiState(component);
        }

        private void TransferReagents(BiopressComponent Biopress, string reagentId, FixedPoint2 amount, string slot, bool fromBuffer)
        {
            var container = _itemSlotsSystem.GetItemOrNull(Biopress.Owner, slot);
            if (container is null ||
                !TryComp<SolutionContainerManagerComponent>(container.Value, out var containerSolution) ||
                !_solutionContainerSystem.TryGetSolution(Biopress.Owner, SharedBiopress.BufferSolutionName, out var bufferSolution))
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

            UpdateUiState(Biopress);
        }

        private void DiscardReagents(BiopressComponent Biopress, string reagentId, FixedPoint2 amount, string slot, bool fromBuffer)
        {

            if (fromBuffer)
            {
                if (_solutionContainerSystem.TryGetSolution(Biopress.Owner, SharedBiopress.BufferSolutionName, out var bufferSolution))
                    bufferSolution.RemoveReagent(reagentId, amount);
                else
                    return;
            }
            else
                return;

            UpdateUiState(Biopress);
        }

        private void ClickSound(BiopressComponent Biopress)
        {
            _audioSystem.Play(Biopress.ClickSound, Filter.Pvs(Biopress.Owner), Biopress.Owner, false, AudioParams.Default.WithVolume(-2f));
        }

        private BiopressContainerInfo? BuildContainerInfo(EntityUid? container)
        {
            if (container is not { Valid: true })
                return null;

            /*if (!TryComp(container, out FitsInDispenserComponent? fits)
                || !_solutionContainerSystem.TryGetSolution(container.Value, fits.Solution, out var solution))
            {
                return null;
            }*/

            if (TryComp<SolutionContainerManagerComponent>(container, out var solutions))
                foreach (var solution in (solutions.Solutions)) //will only work on the first iter val
                {
                    var reagents = solution.Value.Contents.Select(reagent => (reagent.ReagentId, reagent.Quantity)).ToList();
                    return new BiopressContainerInfo(Name(container.Value), true, solution.Value.Volume, solution.Value.MaxVolume, reagents);
                }

            return null;
        }

    }
}
