namespace Content.Server.Atmos.Components;

[RegisterComponent]
public sealed class TemperatureProtectionComponent : Component
{
    /// <summary>
    ///     How much to multiply temperature deltas by.
    /// </summary>
    [DataField("coefficient")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Coefficient = 1.0f;
}
