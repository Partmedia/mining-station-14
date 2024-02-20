using Content.Server.Power.Components;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Random;

using Content.Shared.Atmos;

using System.Linq;

// actually used
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;

using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Stack;
using Content.Server.Temperature.Components;
using Content.Shared.Materials;
using Content.Shared.Power;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Content.Shared.Chemistry;
using Content.Shared.Mining.Components;
using Content.Server.Chemistry.Components;
using static Content.Shared.Storage.SharedStorageComponent;

namespace Content.Server.Mining;

[RegisterComponent]
public class FurnaceComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public readonly Dictionary<string, int> Materials = new();

    /// Maximum pour before it automatically pours itself
    [DataField("pourCapacity")]
    public int PourCapacity = 3000;

    [DataField("xfer")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float ThermalCond = 20f; // thermal conductivity between internal heating element and furnace

    [ViewVariables(VVAccess.ReadWrite)]
    public bool ForceMelt = false; // set to true to force melt everything without waiting for temperature, for debugging

    [ViewVariables(VVAccess.ReadWrite)]
    public bool ForcePour = false; // set to true to force pour using VV, for debugging

    [ViewVariables(VVAccess.ReadWrite)]
    public float InternalTemp; // internal variable for creating 1st order heating response

    public float MaxPower = 10000;
}

public class FurnaceSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _sharedAudioSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly StorageSystem _storageSystem = default!;

    private float AccumTime;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FurnaceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FurnaceComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        SubscribeLocalEvent<FurnaceComponent, GetVerbsEvent<ActivationVerb>>(AddPourVerb);
        SubscribeLocalEvent<FurnaceComponent, BoundUIOpenedEvent>((uid, comp, _) => UpdateUiState(uid,comp));

        SubscribeLocalEvent<FurnaceComponent, FurnaceStoreToggleButtonMessage>(OnStoreToggleButtonMessage);
        SubscribeLocalEvent<FurnaceComponent, FurnacePourButtonMessage>((uid, comp, _) => Pour(uid, comp));
        SubscribeLocalEvent<FurnaceComponent, SetTargetPowerMessage>(OnSetTargetPowerMessage);
    }

    private void OnMapInit(EntityUid uid, FurnaceComponent comp, MapInitEvent args)
    {
        // Initialize internal temperature to the TemperatureComponent's temperature
        if (TryComp<TemperatureComponent>(comp.Owner, out var temp))
        {
            comp.InternalTemp = temp.CurrentTemperature;
        }
    }

    public override void Update(float dt)
    {
        float UpdateRate = 0.5f;
        AccumTime += dt;
        if (AccumTime < UpdateRate)
            return;
        AccumTime -= UpdateRate;

        foreach (var comp in EntityManager.EntityQuery<FurnaceComponent>())
        {
            if (!TryComp<TemperatureComponent>(comp.Owner, out var temp))
                continue;

            UpdateTemp(comp.Owner, comp, temp, UpdateRate);
            MeltOres(comp.Owner, comp, temp);
            OreReactions(comp.Owner, comp, temp);

            if (comp.ForcePour)
            {
                Pour(comp.Owner, comp);
                comp.ForcePour = false;
            }

            // for just the furnace, don't allow insertion if the door is closed
            if (TryComp<ServerStorageComponent>(comp.Owner, out var storage))
            {
                storage.ClickInsert = storage.IsOpen;
                storage.CollideInsert = storage.IsOpen;
            }

            UpdateUiState(comp.Owner, comp);
        }
    }

    private void OnSetTargetPowerMessage(EntityUid uid, FurnaceComponent comp, SetTargetPowerMessage message)
    {
        if (TryComp<ApcPowerReceiverComponent>(uid, out var power))
        {
            power.Load = comp.MaxPower * message.TargetPower;
        }
        UpdateUiState(comp.Owner, comp);
    }

    private void OnStoreToggleButtonMessage(EntityUid uid, FurnaceComponent comp, FurnaceStoreToggleButtonMessage message)
    {
        if (!TryComp<ServerStorageComponent>(uid, out var storage))
            return;

        var user = message.Session.AttachedEntity;
        if (user != null)
        {
            if (!storage.IsOpen)
            {
                _storageSystem.OpenStorageUI(uid, user.Value, storage);
                RaiseLocalEvent(uid, new BoundUIOpenedEvent(StorageUiKey.Key, uid, message.Session));
            }
            else if (TryComp(uid, out ServerUserInterfaceComponent? ui) && TryComp<ActorComponent>(user, out var actor))
            { 
                storage.IsOpen = false;
                _userInterfaceSystem.TryClose(uid, StorageUiKey.Key, actor.PlayerSession, ui);
                RaiseLocalEvent(uid, new BoundUIClosedEvent(StorageUiKey.Key, uid, message.Session));
            }
        }

        UpdateUiState(uid, comp);
    }

    private void UpdateUiState(EntityUid uid, FurnaceComponent comp)
    {
        if (!TryComp<ServerStorageComponent>(uid, out var storage))
            return;

        if (!TryComp<TemperatureComponent>(uid, out var temp))
            return;

        if (!TryComp<ApcPowerReceiverComponent>(uid, out var power))
            return;

        var currentPower = power.Load / comp.MaxPower;

        var state = new FurnaceBoundUserInterfaceState(storage.IsOpen, temp.CurrentTemperature, currentPower);

        _userInterfaceSystem.TrySetUiState(uid, FurnaceUiKey.Key, state);
    }

    private void OnAtmosUpdate(EntityUid uid, FurnaceComponent comp, AtmosDeviceUpdateEvent args)
    {
        if (TryComp<TemperatureComponent>(uid, out var temp))
        {
            if (TryComp<ServerStorageComponent>(uid, out var storage) && storage.IsOpen)
            {
                // Don't leave the door open when it's hot
                temp.AtmosTemperatureTransferEfficiency = 0.9f;

                // TODO: make atmos hotspot
            }
            else
            {
                temp.AtmosTemperatureTransferEfficiency = 0.05f;
            }
        }
    }

    private void UpdateTemp(EntityUid uid, FurnaceComponent comp, TemperatureComponent temp, float dt)
    {
        // Add energy to internal heating element
        const float Hratio = 10f;
        float specHeat = temp.SpecificHeat / Hratio; // heating element specific heat
        if (TryComp<ApcPowerReceiverComponent>(uid, out var power))
        {
            if (power.Powered)
            {
                float energy = power.Load * (1 - power.DumpHeat) * dt;
                comp.InternalTemp += energy/specHeat;
            }
            _appearance.SetData(uid, PowerDeviceVisuals.Powered, power.Powered && power.Load > 10f);
        }

        // Transfer energy to temperature component
        float dT = comp.InternalTemp - temp.CurrentTemperature;
        float dE = dT * comp.ThermalCond * dt;
        comp.InternalTemp -= dE/specHeat;
        temp.CurrentTemperature += dE/temp.SpecificHeat;
    }

    private void MeltOres(EntityUid uid, FurnaceComponent comp, TemperatureComponent temp)
    {
        if (!TryComp<ServerStorageComponent>(uid, out var storage) || storage.StoredEntities is null)
            return;

        foreach (var ore in storage.StoredEntities)
        {
            if (TryComp<MaterialComponent>(ore, out var material))
            {
                if (comp.ForceMelt || temp.CurrentTemperature > MeltingTemperature(material, comp))
                {
                    Melt(ore, comp, material);
                }
            }
        }

        // auto pour if overflowing
        int total = comp.Materials.Sum(x => x.Value);
        if (total > comp.PourCapacity)
            Pour(uid, comp);
    }

    private float MeltingTemperature(MaterialComponent material, FurnaceComponent comp)
    {
        float max = 0;
        foreach ((string k, int v) in material.Materials)
        {
            if (_prototype.TryIndex<MaterialPrototype>(k, out var mat))
            {
                float myMelt = mat.MeltingTemperature;
                // soda ash as a flux to reduce the melting temperature of glass
                if (k == "Glass" && comp.Materials.ContainsKey("Soda"))
                    myMelt -= 500f;
                max = MathF.Max(myMelt, max);
            }
        }
        return max;
    }

    private void Melt(EntityUid uid, FurnaceComponent furnace, MaterialComponent ore)
    {
        foreach ((var k, var v) in ore.Materials)
        {
            if (!furnace.Materials.ContainsKey(k))
                furnace.Materials[k] = 0;
            furnace.Materials[k] += v;
        }
        QueueDel(ore.Owner);
    }

    private void OreReactions(EntityUid uid, FurnaceComponent furnace, TemperatureComponent temp)
    {
        // 2C + O2 -> 2CO
        // iron oxide + CO -> Iron
    }

    private EntityUid SpawnSheet(EntityUid uid, Dictionary<string, int> materials)
    {
        int total = materials.Sum(x => x.Value);
        var partials = materials.ToDictionary(x => x.Key, x => (float)x.Value/total);
        float purity = 1f;

        // Prototype selection logic, assume slag unless we meet certain criteria
        string proto = "SheetSlag1";
        if (percentage("Gold", partials) > 0.5)
        {
            proto = "IngotGold1";
            purity = percentage("Gold", partials);
        }
        else if (percentage("Silver", partials) > 0.5)
        {
            proto = "IngotSilver1";
            purity = percentage("Silver", partials);
        }
        else if (percentage("Uranium", partials) > 0.8)
        {
            proto = "SheetUranium1";
            purity = percentage("Uranium", partials);
        }
        else if (percentage("Plasma", partials) > 0.8)
        {
            proto = "SheetPlasma";
            purity = percentage("Plasma", partials);
        }
        else if (percentage("Glass", partials) > 0.8)
        {
            if (percentage("Plasma", partials) > 0.1 && percentage("Steel", partials) > 0.1)
                proto = "SheetRPGlass";
            else if (percentage("Plasma", partials) > 0.1)
                proto = "SheetPGlass";
            else
                proto = "SheetGlass1";
        }
        else if (percentage("Steel", partials) > 0.5)
        {
            proto = "SheetSteel1";
            purity = percentage("Steel", partials);
        }
        else if (percentage("Copper", partials) > 0.5)
        {
            proto = "SheetCopper1";
            purity = percentage("Copper", partials);
        }

        var result = Spawn(proto, Transform(uid).Coordinates);
        // adjust coloration based on purity
        if (TryComp<SpriteComponent>(result, out var sprite))
        {
            float min_color = 0.75f;
            int color_scale = (int)(255*((1-min_color)*purity + min_color));
            /*
             * broken
            sprite._netSync = true;
            sprite.Color = new Color(color_scale, color_scale, color_scale, 255);
            */
        }
        return result;
    }

    private float percentage(string key, Dictionary<string, float> materials)
    {
        if (materials.ContainsKey(key))
            return materials[key];
        else
            return 0f;
    }

    private void Pour(EntityUid uid, FurnaceComponent furnace)
    {
        int total = furnace.Materials.Sum(x => x.Value);
        int numSheets = total/100;
        var result = SpawnSheet(uid, furnace.Materials);
        if (TryComp<MaterialComponent>(result, out var mat))
        {
            mat.Materials.Clear();
            foreach ((var k, var v) in furnace.Materials)
            {
                mat.Materials.Add(k, v/numSheets);
            }
        }
        _stack.SetCount(result, numSheets);
        furnace.Materials.Clear();
    }

    private void AddPourVerb(EntityUid uid, FurnaceComponent component, GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        ActivationVerb verb = new()
        {
            Text = Loc.GetString("furnace-pour"),
            IconEntity = args.Using,
            Act = () => Pour(uid, component)
        };

        args.Verbs.Add(verb);
    }
}
