using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Popups;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Content.Shared.Atmos.Components.SharedGasAnalyzerComponent;

namespace Content.Server.Atmos.EntitySystems
{
    [UsedImplicitly]
    public sealed class GasAnalyzerSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly AtmosphereSystem _atmo = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
        [Dependency] private readonly IPrototypeManager _protoMan = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GasAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<GasAnalyzerComponent, GasAnalyzerDisableMessage>(OnDisabledMessage);
            SubscribeLocalEvent<GasAnalyzerComponent, DroppedEvent>(OnDropped);
            SubscribeLocalEvent<GasAnalyzerComponent, UseInHandEvent>(OnUseInHand);
        }

        public override void Update(float frameTime)
        {

            foreach (var analyzer in EntityQuery<ActiveGasAnalyzerComponent>())
            {
                // Don't update every tick
                analyzer.AccumulatedFrametime += frameTime;

                if (analyzer.AccumulatedFrametime < analyzer.UpdateInterval)
                    continue;

                analyzer.AccumulatedFrametime -= analyzer.UpdateInterval;

                if (!UpdateAnalyzer(analyzer.Owner))
                    RemCompDeferred<ActiveGasAnalyzerComponent>(analyzer.Owner);
            }
        }

        /// <summary>
        /// Activates the analyzer when used in the world, scanning either the target entity or the tile clicked
        /// </summary>
        private void OnAfterInteract(EntityUid uid, GasAnalyzerComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach)
            {
                _popup.PopupEntity(Loc.GetString("gas-analyzer-component-player-cannot-reach-message"), args.User, args.User);
                return;
            }
            ActivateAnalyzer(uid, component, args.User, args.Target);
            OpenUserInterface(args.User, component);
            args.Handled = true;
        }

        /// <summary>
        /// Activates the analyzer with no target, so it only scans the tile the user was on when activated
        /// </summary>
        private void OnUseInHand(EntityUid uid, GasAnalyzerComponent component, UseInHandEvent args)
        {
            ActivateAnalyzer(uid, component, args.User);
            args.Handled = true;
        }

        /// <summary>
        /// Handles analyzer activation logic
        /// </summary>
        private void ActivateAnalyzer(EntityUid uid, GasAnalyzerComponent component, EntityUid user, EntityUid? target = null)
        {
            component.Target = target;
            component.User = user;
            if (target != null)
                component.LastPosition = Transform(target.Value).Coordinates;
            else
                component.LastPosition = null;
            component.Enabled = true;
            Dirty(component);
            UpdateAppearance(component);
            if(!HasComp<ActiveGasAnalyzerComponent>(uid))
                AddComp<ActiveGasAnalyzerComponent>(uid);
            UpdateAnalyzer(uid, component);
        }

        /// <summary>
        /// Close the UI, turn the analyzer off, and don't update when it's dropped
        /// </summary>
        private void OnDropped(EntityUid uid, GasAnalyzerComponent component, DroppedEvent args)
        {
            if(args.User is { } userId && component.Enabled)
                _popup.PopupEntity(Loc.GetString("gas-analyzer-shutoff"), userId, userId);
            DisableAnalyzer(uid, component, args.User);
        }

        /// <summary>
        /// Closes the UI, sets the icon to off, and removes it from the update list
        /// </summary>
        private void DisableAnalyzer(EntityUid uid, GasAnalyzerComponent? component = null, EntityUid? user = null)
        {
            if (!Resolve(uid, ref component))
                return;

            if (user != null && TryComp<ActorComponent>(user, out var actor))
                _userInterface.TryClose(uid, GasAnalyzerUiKey.Key, actor.PlayerSession);

            component.Enabled = false;
            Dirty(component);
            UpdateAppearance(component);
            RemCompDeferred<ActiveGasAnalyzerComponent>(uid);
        }

        /// <summary>
        /// Disables the analyzer when the user closes the UI
        /// </summary>
        private void OnDisabledMessage(EntityUid uid, GasAnalyzerComponent component, GasAnalyzerDisableMessage message)
        {
            if (message.Session.AttachedEntity is not {Valid: true})
                return;
            DisableAnalyzer(uid, component);
        }

        private void OpenUserInterface(EntityUid user, GasAnalyzerComponent component)
        {
            if (!TryComp<ActorComponent>(user, out var actor))
                return;

            _userInterface.TryOpen(component.Owner, GasAnalyzerUiKey.Key, actor.PlayerSession);
        }

        /// <summary>
        /// Fetches fresh data for the analyzer. Should only be called by Update or when the user requests an update via refresh button
        /// </summary>
        private bool UpdateAnalyzer(EntityUid uid, GasAnalyzerComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return false;

            if (!TryComp(component.User, out TransformComponent? xform))
            {
                DisableAnalyzer(uid, component);
                return false;
            }

            // check if the user has walked away from what they scanned
            var userPos = xform.Coordinates;
            if (component.LastPosition.HasValue)
            {
                // Check if position is out of range => don't update and disable
                if (!component.LastPosition.Value.InRange(EntityManager, userPos, SharedInteractionSystem.InteractionRange))
                {
                    if(component.User is { } userId && component.Enabled)
                        _popup.PopupEntity(Loc.GetString("gas-analyzer-shutoff"), userId, userId);
                    DisableAnalyzer(uid, component, component.User);
                    return false;
                }
            }

            var gasMixList = new List<GasMixEntry>();

            // Fetch the environmental atmosphere around the scanner. This must be the first entry
            // TODO CONDENSE account for puddles on tile
            // TODO CONDENSE remove duplicated heat equation code, it breaks when there's no gas anyway
            var tileGasMixture = _atmo.GetContainingMixture(component.Owner, true);
            var tileSolution = _atmo.GetContainingSolution(component.Owner, true);
            var Ttile = Atmospherics.TCMB;
            if (tileGasMixture != null || tileSolution != null)
            {
               
                float Hgas = tileSolution?.Volume == 0 ? Atmospherics.SpaceHeatCapacity : 0;
                float Qgas = 0;
                if(tileGasMixture != null)
                {
                    Hgas = _atmo.GetHeatCapacity(tileGasMixture);
                    Qgas = Hgas * tileGasMixture.Temperature;
                }
                else
                {
                    Qgas = Atmospherics.SpaceHeatCapacity * Atmospherics.TCMB;
                }
                float Hliquid = 0;
                float Qliquid = 0;
                if (tileSolution != null)
                {
                Hliquid = tileSolution.GetHeatCapacity(_protoMan);
                Qliquid = Hliquid * tileSolution.Temperature;
                }
                Ttile = (Qgas + Qliquid) / (Hgas + Hliquid);
            }
            var liquidHeight = 0f;
            if (tileSolution != null) {
                liquidHeight = (tileSolution.Volume/tileSolution.MaxVolume).Float(); //tileSolution.SafeVolume?
            }
            if (tileGasMixture != null)
            {
                gasMixList.Add(new GasMixEntry(Loc.GetString("gas-analyzer-window-environment-tab-label"), tileGasMixture.Pressure, Ttile, liquidHeight, 
                    GenerateGasEntryArray(tileGasMixture), GenerateLiquidEntryArray(tileSolution)));
            }
            else
            {
                // No gases were found
                gasMixList.Add(new GasMixEntry(Loc.GetString("gas-analyzer-window-environment-tab-label"), 0f, Ttile, liquidHeight, null, GenerateLiquidEntryArray(tileSolution)));
            }

            var deviceFlipped = false;
            if (component.Target != null)
            {
                if (Deleted(component.Target))
                {
                    component.Target = null;
                    DisableAnalyzer(uid, component, component.User);
                    return false;
                }

                // gas analyzer was used on an entity, try to request gas data via event for override
                var ev = new GasAnalyzerScanEvent();
                RaiseLocalEvent(component.Target.Value, ev, false);
                if (ev.GasMixtures != null)
                {
                    foreach (var mixes in ev.GasMixtures)
                    {
                        Solution? liquids = null;
                        if(ev.Liquids != null)
                            if(ev.Liquids.ContainsKey(mixes.Key))
                                liquids = ev.Liquids[mixes.Key];
                        if(liquids == null)
                            liquids = new Solution();
                        float Hgas = liquids?.Volume == 0 ? Atmospherics.SpaceHeatCapacity : 0;
                        float Qgas = 0;
                        if(mixes.Value != null)
                        {
                            Hgas = _atmo.GetHeatCapacity(mixes.Value);
                            Qgas = Hgas * mixes.Value.Temperature;
                        }
                        else
                        {
                            Qgas = Atmospherics.SpaceHeatCapacity * Atmospherics.TCMB;
                        }
                        float Hliquid = 0;
                        float Qliquid = 0;
                        float height = 0;
                        if(liquids != null) {
                            Hliquid = liquids.GetHeatCapacity(_protoMan);
                            Qliquid = Hliquid * liquids.Temperature;
                            height = (liquids.Volume/liquids.MaxVolume).Float();
                        }
                        float Tfinal = (Qgas + Qliquid) / (Hgas + Hliquid);
                        if(mixes.Value != null)
                            gasMixList.Add(new GasMixEntry(mixes.Key, mixes.Value.Pressure, Tfinal, height, GenerateGasEntryArray(mixes.Value), GenerateLiquidEntryArray(liquids)));
                    }
                    deviceFlipped = ev.DeviceFlipped;
                }
                else
                {
                    // No override, fetch manually, to handle flippable devices you must subscribe to GasAnalyzerScanEvent
                    if (TryComp(component.Target, out NodeContainerComponent? node))
                    {
                        foreach (var pair in node.Nodes)
                        {
                            if (pair.Value is PipeNode pipeNode)
                                gasMixList.Add(new GasMixEntry(pair.Key, pipeNode.Air.Pressure, pipeNode.Air.Temperature, (pipeNode.Liquids.Volume/pipeNode.Air.Volume).Float(), GenerateGasEntryArray(pipeNode.Air), GenerateLiquidEntryArray(pipeNode.Liquids)));
                        }
                    }
                }
            }

            // Don't bother sending a UI message with no content, and stop updating I guess?
            if (gasMixList.Count == 0)
                return false;

            _userInterface.TrySendUiMessage(component.Owner, GasAnalyzerUiKey.Key,
                new GasAnalyzerUserMessage(gasMixList.ToArray(),
                    component.Target != null ? Name(component.Target.Value) : string.Empty,
                    component.Target ?? EntityUid.Invalid,
                    deviceFlipped));
            return true;
        }

        /// <summary>
        /// Sets the appearance based on the analyzers Enabled state
        /// </summary>
        private void UpdateAppearance(GasAnalyzerComponent analyzer)
        {
            _appearance.SetData(analyzer.Owner, GasAnalyzerVisuals.Enabled, analyzer.Enabled);
        }

        /// <summary>
        /// Generates a GasEntry array for a given GasMixture
        /// </summary>
        private GasEntry[] GenerateGasEntryArray(GasMixture? mixture)
        {
            var gases = new List<GasEntry>();

            for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
            {
                var gas = _atmo.GetGas(i);

                if (mixture?.Moles[i] <= Atmospherics.GasMinMoles)
                    continue;

                if (mixture != null)
                {
                    var gasName = Loc.GetString(gas.Name);
                    gases.Add(new GasEntry(gasName, mixture.Moles[i], gas.Color));
                }
            }
            return gases.ToArray();
        }

        /// <summary>
        /// Generates a LiquidEntry array for a given Solution
        /// </summary>
        private LiquidEntry[] GenerateLiquidEntryArray(Solution? liquids)
        {
            var reagents = new List<LiquidEntry>();
            
            if (liquids == null)
                return reagents.ToArray();
            
            foreach (var reagent in liquids)
            {
                    var liquidName = Loc.GetString(reagent.ReagentId);
                    var color = Color.Transparent;
                    if (_protoMan.TryIndex(reagent.ReagentId, out ReagentPrototype? proto))
                        color = proto.SubstanceColor;
                    reagents.Add(new LiquidEntry(liquidName, reagent.Quantity.Float(), color.ToHex()));
            }
            return reagents.ToArray();
        }
    }
}

/// <summary>
/// Raised when the analyzer is used. An atmospherics device that does not rely on a NodeContainer or
/// wishes to override the default analyzer behaviour of fetching all nodes in the attached NodeContainer
/// should subscribe to this and return the GasMixtures as desired. A device that is flippable should subscribe
/// to this event to report if it is flipped or not. See GasFilterSystem or GasMixerSystem for an example.
/// </summary>
public sealed class GasAnalyzerScanEvent : EntityEventArgs
{
    /// <summary>
    /// Key is the mix name (ex "pipe", "inlet", "filter"), value is the pipe direction and GasMixture. Add all mixes that should be reported when scanned.
    /// </summary>
    public Dictionary<string, GasMixture?>? GasMixtures;
    public Dictionary<string, Solution?>? Liquids;

    /// <summary>
    /// If the device is flipped. Flipped is defined as when the inline input is 90 degrees CW to the side input
    /// </summary>
    public bool DeviceFlipped;
}
