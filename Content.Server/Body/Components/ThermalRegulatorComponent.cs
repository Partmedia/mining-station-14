using Content.Server.Body.Systems;

namespace Content.Server.Body.Components;

[RegisterComponent]
[Access(typeof(ThermalRegulatorSystem))]
public sealed class ThermalRegulatorComponent : Component
{
    /// <summary>
    /// Obsolete and ignored.
    /// </summary>
    [DataField("metabolismHeat")]
    public float MetabolismHeat { get; private set; }

    /// <summary>
    /// Obsolete and ignored.
    /// </summary>
    [DataField("radiatedHeat")]
    public float RadiatedHeat { get; private set; }

    /// <summary>
    /// Maximum heat regulated via sweat
    /// </summary>
    [DataField("sweatHeatRegulation")]
    public float SweatHeatRegulation { get; private set; }

    /// <summary>
    /// Maximum heat regulated via shivering
    /// </summary>
    [DataField("shiveringHeatRegulation")]
    public float ShiveringHeatRegulation { get; private set; }

    /// <summary>
    /// Amount of heat regulation that represents thermal regulation processes not
    /// explicitly coded.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("implicitHeatRegulation")]
    public float ImplicitHeatRegulation { get; private set; }

    /// <summary>
    /// Normal body temperature
    /// </summary>
    [DataField("normalBodyTemperature")]
    public float NormalBodyTemperature { get; private set; }

    /// <summary>
    /// Obsolete and ignored.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("thermalRegulationTemperatureThreshold")]
    public float ThermalRegulationTemperatureThreshold { get; private set; }

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("gain")]
    public float Gain = 10000;

    public float AccumulatedFrametime;
}
