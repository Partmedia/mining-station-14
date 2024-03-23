using Content.Shared.Cloning;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Materials;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Cloning.Components
{
    [RegisterComponent]
    public sealed class AutoCloningPodComponent : Component
    {
        [ViewVariables]
        public ContainerSlot BodyContainer = default!;

        /// <summary>
        /// How long the cloning has been going on for.
        /// </summary>
        [ViewVariables]
        public float CloningProgress = 0;

        /// <summary>
        /// The base amount of time it takes to clone a body
        /// </summary>
        [DataField("baseCloningTime")]
        public float BaseCloningTime = 30f;

        /// <summary>
        /// The current amount of time it takes to clone a body
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float CloningTime = 30f;

        [ViewVariables(VVAccess.ReadWrite)]
        public CloningPodStatus Status;
    }
}
