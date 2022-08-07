using Robust.Shared.Serialization;

namespace Content.Shared.OuterRim.Generator;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed class SharedGeneratorComponent : Component
{
    [DataField("remainingFuel"), ViewVariables(VVAccess.ReadWrite)]
    public float RemainingFuel = 0.0f;

    [DataField("targetPower"), ViewVariables(VVAccess.ReadWrite)]
    public float TargetPower = 0;

    [DataField("fuelMaterial"), ViewVariables(VVAccess.ReadWrite)]
    public string FuelMaterial = "Plasma";

    public float Efficiency = 0;
    public float Output = 0;
}

/// <summary>
/// Sent to the server to adjust the targetted power level.
/// </summary>
[Serializable, NetSerializable]
public sealed class SetTargetPowerMessage : BoundUserInterfaceMessage
{
    public float TargetPower;

    public SetTargetPowerMessage(float targetPower)
    {
        TargetPower = targetPower;
    }
}

/// <summary>
/// Contains network state for SharedGeneratorComponent.
/// </summary>
[Serializable, NetSerializable]
public sealed class GeneratorComponentBuiState : BoundUserInterfaceState
{
    public float RemainingFuel;
    public float TargetPower;
    public float Efficiency;
    public float Output;

    public GeneratorComponentBuiState(SharedGeneratorComponent component)
    {
        RemainingFuel = component.RemainingFuel;
        TargetPower = component.TargetPower;
        Efficiency = component.Efficiency;
        Output = component.Output;
    }
}

[Serializable, NetSerializable]
public enum GeneratorComponentUiKey
{
    Key
}
