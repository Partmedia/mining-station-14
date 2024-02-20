using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Inventory.Events;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Server.Popups;
using Content.Shared.Examine;
using Content.Shared.Rejuvenate;
using Content.Shared.Body.Organ;

namespace Content.Server.Body.Systems;

public sealed class LungSystem : EntitySystem
{
    [Dependency] private readonly InternalsSystem _internals = default!;
    [Dependency] private readonly SolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    public static string LungSolutionName = "Lung";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LungComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BreathToolComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<BreathToolComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<LungComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<LungComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnGotUnequipped(EntityUid uid, BreathToolComponent component, GotUnequippedEvent args)
    {
        _atmosphereSystem.DisconnectInternals(component);
    }

    private void OnGotEquipped(EntityUid uid, BreathToolComponent component, GotEquippedEvent args)
    {

        if ((args.SlotFlags & component.AllowedSlots) != component.AllowedSlots) return;
        component.IsFunctional = true;

        if (TryComp(args.Equipee, out InternalsComponent? internals))
        {
            component.ConnectedInternalsEntity = args.Equipee;
            _internals.ConnectBreathTool(internals, uid);
        }
    }

    private void OnComponentInit(EntityUid uid, LungComponent component, ComponentInit args)
    {
        component.LungSolution = _solutionContainerSystem.EnsureSolution(uid, LungSolutionName);
        component.LungSolution.MaxVolume = 100.0f;
        component.LungSolution.CanReact = false; // No dexalin lungs
    }

    public void CheckLungDamage(EntityUid uid, LungComponent lung, string reagentId, float amount)
    {
        //narcotics and toxins deal damage to the lungs
        //the damage taken is used modify the suffocation threshold UNLESS said lungs are immune
        //determine reagent type - is it in the damage list?
        if (!_prototypeManager.TryIndex<ReagentPrototype>(reagentId, out var proto) || proto.Metabolisms == null)
            return;

        var toxic = false;
        //if it is, multiply amount by coeff and add value to Lung Damage value
        foreach (var group in lung.DamageGroups)
        {
            if (proto.Metabolisms.ContainsKey(group))
            {
                toxic = true;
                break;
            }
        }

        if (toxic)
        {
            if (amount < 0.1f)
                amount = 0f;
            //track damage
            lung.Damage += amount * lung.DamageMod;
        }
    }

    public void GasToReagent(EntityUid uid, LungComponent lung)
    {
        foreach (var gas in Enum.GetValues<Gas>())
        {
            var i = (int) gas;
            var moles = lung.Air.Moles[i];
            if (moles <= 0)
                continue;
            var reagent = _atmosphereSystem.GasReagents[i];
            if (reagent == null) continue;

            var amount = (moles * Atmospherics.BreathMolesToReagentMultiplier);

            _solutionContainerSystem.TryAddReagent(uid, lung.LungSolution, reagent, amount, out _);

            // We don't remove the gas from the lung mix,
            // that's the responsibility of whatever gas is being metabolized.
            // Most things will just want to exhale again.

            CheckLungDamage(uid, lung, reagent, amount);           
        }
    }

    public void UpdateLungStatus(EntityUid body, LungComponent lung)
    {
        //Update and signal damage
        if (lung.Damage >= lung.CriticalDamage)
        {
            if (lung.Condition != OrganCondition.Critical)
                _popupSystem.PopupEntity("you struggle to breath and your chest hurts", body, body); //TODO loc
            lung.Condition = OrganCondition.Critical;
        }
        else if (lung.Damage >= lung.WarningDamage)
        {
            if (lung.Condition != OrganCondition.Critical && lung.Condition != OrganCondition.Warning)
                _popupSystem.PopupEntity("you find it slightly harder to breath", body, body); //TODO loc
            lung.Condition = OrganCondition.Warning;
        }
        else
        {
            lung.Condition = OrganCondition.Good;
        }
    }

    private void OnExamine(EntityUid uid, LungComponent comp, ExaminedEvent args)
    {
        switch (comp.Condition)
        {
            case OrganCondition.Good:
                args.PushText(Loc.GetString("organ-healthy"));
                break;
            case OrganCondition.Warning:
                args.PushText(Loc.GetString("organ-warning"));
                break;
            case OrganCondition.Critical:
                args.PushText(Loc.GetString("organ-critical"));
                break;
        }
    }

    private void OnRejuvenate(EntityUid uid, LungComponent lung, RejuvenateEvent args)
    {
        lung.Damage = 0f;
        lung.Condition = OrganCondition.Good;
    }
}
