using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed class CLF3FireReaction : IGasReactionEffect
    {
        public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem)
        {

            var energyReleased = 0f;
            var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture);
            var temperature = mixture.Temperature;
            var location = holder as TileAtmosphere;
            mixture.ReactionResults[GasReaction.Fire] = 0f;
            var burnedFuel = 0f;
            var initialCLF3 = mixture.GetMoles(Gas.CLF3);
            var retardant = mixture.GetMoles(Gas.Nitrogen);
            

            burnedFuel = initialCLF3 - (retardant/Atmospherics.CLF3NitrogenRetardantFactor);
            energyReleased += (Atmospherics.FireHydrogenEnergyReleased * burnedFuel * (Atmospherics.HydrogenBurnHydroFactor - 1));

            if (burnedFuel > 0)
            {
                
                mixture.SetMoles(Gas.CLF3, initialCLF3 * (1 - 1 / Atmospherics.HydrogenBurnHydroFactor)); //just using the hydro burn for now
                energyReleased += (Atmospherics.FireHydrogenEnergyReleased * burnedFuel);

                // Conservation of mass is important.
                mixture.AdjustMoles(Gas.Oxygen, burnedFuel); //just going with this for now

                mixture.ReactionResults[GasReaction.Fire] += burnedFuel;
            } else
            {
                //the nitrogen won't stop the reaction, but it will slow it down significantly
                mixture.SetMoles(Gas.CLF3, initialCLF3 * (1 - 1 / (Atmospherics.HydrogenBurnHydroFactor*10000)));

                // Conservation of mass is important.
                mixture.AdjustMoles(Gas.Oxygen, initialCLF3 - (initialCLF3 * (1 - 1 / (Atmospherics.HydrogenBurnHydroFactor * 10000))));
            }

            if (energyReleased > 0)
            {
                var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture);
                if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                    mixture.Temperature = ((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity);
            }

            if (location != null)
            {
                temperature = mixture.Temperature;
                if (temperature > Atmospherics.FireMinimumTemperatureToExist)
                {
                    atmosphereSystem.HotspotExpose(location.GridIndex, location.GridIndices, temperature, mixture.Volume);
                }
            }

            return mixture.ReactionResults[GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
        }
    }
}
