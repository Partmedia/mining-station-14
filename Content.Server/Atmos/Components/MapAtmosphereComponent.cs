using Content.Shared.Chemistry.Components;

namespace Content.Server.Atmos.Components;

/// <summary>
///     Component that defines the default GasMixture for a map.
/// </summary>
/// <remarks>Honestly, no need to [Friend] this. It's just three simple data fields... Change them to your heart's content.</remarks>
[RegisterComponent]
public sealed class MapAtmosphereComponent : Component
{
    /// <summary>
    ///     The default GasMixture a map will have. Space mixture by default.
    /// </summary>
    [DataField("mixture"), ViewVariables(VVAccess.ReadWrite)]
    public GasMixture? Mixture = GasMixture.SpaceGas;

    // TODO CONDENSE why should a map have a 0 volume solution? this needs to be rectified...
    /// <summary>
    ///     The default Solution a map will have. Empty solution by default.
    /// </summary>
    [DataField("solution"), ViewVariables(VVAccess.ReadWrite)]
    public Solution? Liquids = new Solution();

    /// <summary>
    ///     Whether empty tiles will be considered space or not.
    /// </summary>
    [DataField("space"), ViewVariables(VVAccess.ReadWrite)]
    public bool Space = true;
}
