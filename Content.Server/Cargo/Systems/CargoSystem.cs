using Content.Server.Cargo.Components;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem : SharedCargoSystem
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("cargo");
        InitializeConsole();
        InitializeShuttle();
        InitializeTelepad();
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInit);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ShutdownShuttle();
        CleanupShuttle();
    }

    private void OnStationInit(StationInitializedEvent ev)
    {
        var bank = EnsureComp<StationBankAccountComponent>(ev.Station);
        bank.InitialBalance = _configurationManager.GetCVar(CCVars.InitialBalance);
        bank.Balance = bank.InitialBalance;
        EnsureComp<StationCargoOrderDatabaseComponent>(ev.Station);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateConsole(frameTime);
        UpdateTelepad(frameTime);
    }

    // please don't delete this thank you
    public void UpdateBankAccount(StationBankAccountComponent component, int balanceAdded)
    {
        component.Balance += balanceAdded;
    }
}
