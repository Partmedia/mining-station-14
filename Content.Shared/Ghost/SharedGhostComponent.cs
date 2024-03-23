using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Ghost
{
    [NetworkedComponent()]
    public abstract class SharedGhostComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        public bool CanGhostInteract
        {
            get => _canGhostInteract;
            set
            {
                if (_canGhostInteract == value) return;
                _canGhostInteract = value;
                Dirty();
            }
        }

        [DataField("canInteract")]
        private bool _canGhostInteract;

        /// <summary>
        ///     Changed by <see cref="SharedGhostSystem.SetCanReturnToBody"/>
        /// </summary>
        // TODO MIRROR change this to use friend classes when thats merged
        [ViewVariables(VVAccess.ReadWrite)]
        public bool CanReturnToBody
        {
            get => _canReturnToBody;
            set
            {
                if (_canReturnToBody == value) return;
                _canReturnToBody = value;
                Dirty();
            }
        }

        [ViewVariables(VVAccess.ReadWrite)]
        public bool CanGhostRespawn
        {
            get => _canGhostRespawn;
            set
            {
                if (_canGhostRespawn == value) return;
                _canGhostRespawn = value;
                Dirty();
            }
        }

        [ViewVariables(VVAccess.ReadWrite)]
        public float GhostRespawnTimer
        {
            get => _ghostRespawnTimer;
            set
            {
                if (_ghostRespawnTimer == value) return;
                _ghostRespawnTimer = value;
                Dirty();
            }
        }

        [DataField("canReturnToBody")]
        private bool _canReturnToBody;

        [DataField("canGhostRespawn")]
        private bool _canGhostRespawn;

        [DataField("ghostRespawnTimer")]
        private float _ghostRespawnTimer;

        public override ComponentState GetComponentState()
        {
            return new GhostComponentState(CanReturnToBody, CanGhostInteract, CanGhostRespawn, GhostRespawnTimer);
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);

            if (curState is not GhostComponentState state)
            {
                return;
            }

            CanReturnToBody = state.CanReturnToBody;
            CanGhostInteract = state.CanGhostInteract;
            CanGhostRespawn = state.CanGhostRespawn;
            GhostRespawnTimer = state.GhostRespawnTimer;
        }
    }

    [Serializable, NetSerializable]
    public sealed class GhostComponentState : ComponentState
    {
        public bool CanReturnToBody { get; }
        public bool CanGhostInteract { get; }
        public bool CanGhostRespawn { get;  }
        public float GhostRespawnTimer { get; }

        public GhostComponentState(
            bool canReturnToBody,
            bool canGhostInteract,
            bool canGhostRespawn,
            float ghostRespawnTimer)
        {
            CanReturnToBody = canReturnToBody;
            CanGhostInteract = canGhostInteract;
            CanGhostRespawn = canGhostRespawn;
            GhostRespawnTimer = ghostRespawnTimer;
        }
    }
}


