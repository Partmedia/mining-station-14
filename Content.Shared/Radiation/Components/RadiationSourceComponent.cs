namespace Content.Shared.Radiation.Components;

/// <summary>
///     Irradiate all objects in range.
/// </summary>
[RegisterComponent]
public sealed class RadiationSourceComponent : Component
{
    /// <summary>
    ///     Radiation intensity in center of the source in rads per second.
    ///     From there radiation rays will travel over distance and loose intensity
    ///     when hit radiation blocker.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("intensity")]
    public float Intensity = 1;

    /// <summary>
    ///     Deprecated and unused. Radiation always falls off at an
    ///     inverse law (see Irradiate() in RadiationSystem.GridCast).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("slope")]
    public float Slope = 0.5f;

    /** Number of radioactive molecules (left). Each radioactive molecule can
     * produce one unit of radiation. If negative, infinite to preserve old
     * behavior. */
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("N")]
    public float N = -1;

    /** Number of depleted molecules. Used to slow down fission. */
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("D")]
    public float D = 0;

    /** Half life in seconds. After this many seconds, N will be halved. */
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("hl")]
    public float hl = 0;

    /** For each unit of radiation received, emit fissionK more. 0 means non-fissile. */
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("k")]
    public float fissionK = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("ktc")]
    public float fissionKTC = 0;

    /** Number of new particles to create due to fission from the last update. */
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("fissionN")]
    public float FissionN = 0;
}
