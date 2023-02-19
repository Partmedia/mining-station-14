using Content.Shared.Atmos;

namespace Content.Server.Atmos.Piping.Trinary.Components
{
    [RegisterComponent]
    public sealed class GasFilterComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("enabled")]
        public bool Enabled { get; set; } = true;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("inlet")]
        public string InletName { get; set; } = "inlet";

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("filter")]
        public string FilterName { get; set; } = "filter";

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("outlet")]
        public string OutletName { get; set; } = "outlet";

        [ViewVariables(VVAccess.ReadWrite)]

        [DataField("transferRate")]
        public float TransferRate { get; set; } = Atmospherics.MaxTransferRate / 300f; // admittance, L/(sec kPa)

        [ViewVariables(VVAccess.ReadWrite)]
        public Gas? FilteredGas { get; set; }
    }
}
