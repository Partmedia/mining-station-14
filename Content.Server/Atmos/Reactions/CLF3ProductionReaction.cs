using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

/// <summary>
///     Produces CLF3 from Chlorine and Fluorine.
/// </summary>
[UsedImplicitly]
public sealed class CLF3ProductionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem)
    {
        var initialCl = mixture.GetMoles(Gas.Chlorine);
        var initialF = mixture.GetMoles(Gas.Fluorine);

        var clConversion = initialCl/3;
        var fConversion = initialF;
        var total = clConversion + fConversion;

        mixture.AdjustMoles(Gas.Chlorine, -clConversion);
        mixture.AdjustMoles(Gas.Fluorine, -fConversion);
        mixture.AdjustMoles(Gas.CLF3, total);

        return ReactionResult.Reacting;
    }
}
