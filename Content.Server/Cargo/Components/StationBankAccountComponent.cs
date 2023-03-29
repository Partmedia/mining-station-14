using Content.Shared.Cargo;

namespace Content.Server.Cargo.Components;

/// <summary>
/// Added to the abstract representation of a station to track its money.
/// </summary>
[RegisterComponent]
public sealed class StationBankAccountComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("balance")]
    public int Balance;

    [ViewVariables]
    public int InitialBalance;

    /// <summary>
    /// How much the bank balance goes up per second, every Delay period. Rounded down when multiplied.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("increasePerSecond")]
    public int IncreasePerSecond = 0;
}
