using Content.Shared.Chemistry.Components;

namespace Content.Server.Atmos
{
    public interface IGasMixtureHolder
    {
        public GasMixture Air { get; set; }
        public Solution Liquids { get; set; }
    }
}
