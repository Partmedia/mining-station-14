using Content.Shared.Shuttles.Components;

namespace Content.Server.Shuttles.Components
{
    [RegisterComponent]
    public sealed class ShuttleComponent : Component
    {
        [ViewVariables]
        public bool Enabled = true;

        /// <summary>
        /// The cached thrust available for each cardinal direction
        /// </summary>
        [ViewVariables]
        public readonly float[] LinearThrust = new float[4];

        /// <summary>
        /// The thrusters contributing to each direction for impulse.
        /// </summary>
        public readonly List<ThrusterComponent>[] LinearThrusters = new List<ThrusterComponent>[4];

        /// <summary>
        /// The thrusters contributing to the angular impulse of the shuttle.
        /// </summary>
        public readonly List<ThrusterComponent> AngularThrusters = new();

        [ViewVariables]
        public float AngularThrust = 0f;

        /// <summary>
        /// A bitmask of all the directions we are considered thrusting.
        /// </summary>
        [ViewVariables]
        public DirectionFlag ThrustDirections = DirectionFlag.None;

        /// <summary>
        /// Damping applied to the shuttle's physics component when not in FTL.
        /// </summary>
        [DataField("linearDamping"), ViewVariables(VVAccess.ReadWrite)]
        public float LinearDamping = 0.05f;

        [DataField("angularDamping"), ViewVariables(VVAccess.ReadWrite)]
        public float AngularDamping = 0.05f;
    }
}
