using Content.Shared.GameTicking;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Cloning;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Server.Cloning.Components;
using Content.Server.Mind.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Atmos.EntitySystems;
using Content.Server.EUI;
using Content.Server.Humanoid;
using Content.Server.MachineLinking.System;
using Content.Server.MachineLinking.Events;
using Content.Shared.Chemistry.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Construction;
using Content.Server.Materials;
using Content.Server.Stack;
using Content.Server.Jobs;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Zombies;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Server.Containers;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Server.Preferences.Managers;
using Robust.Shared.Prototypes;
using Content.Server.DetailExaminable;
using Robust.Shared.Configuration;

namespace Content.Server.Cloning
{
    public sealed class AutoCloningSystem : EntitySystem
    {
        [Dependency] private readonly IPlayerManager _playerManager = null!;
        [Dependency] private readonly IPrototypeManager _prototype = default!;
        [Dependency] private readonly HumanoidAppearanceSystem _humanoidSystem = default!;
        [Dependency] private readonly ContainerSystem _containerSystem = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SpillableSystem _spillableSystem = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly IConfigurationManager _configManager = default!;
        [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<AutoCloningPodComponent, ComponentInit>(OnComponentInit);
        }

        private void OnComponentInit(EntityUid uid, AutoCloningPodComponent clonePod, ComponentInit args)
        {
            clonePod.BodyContainer = _containerSystem.EnsureContainer<ContainerSlot>(clonePod.Owner, "clonepod-bodyContainer");
        }

        internal void TransferMindToClone(EntityUid entity, Mind.Mind mind)
        {
            mind.TransferTo(entity, ghostCheckOverride: true);
            mind.UnVisit();
        }

        private void HandleMindAdded(EntityUid uid, BeingClonedComponent clonedComponent, MindAddedMessage message)
        {
            if (clonedComponent.Parent == EntityUid.Invalid ||
                !EntityManager.EntityExists(clonedComponent.Parent) ||
                !TryComp<AutoCloningPodComponent>(clonedComponent.Parent, out var AutoCloningPodComponent) ||
                clonedComponent.Owner != AutoCloningPodComponent.BodyContainer.ContainedEntity)
            {
                EntityManager.RemoveComponent<BeingClonedComponent>(clonedComponent.Owner);
                return;
            }
            UpdateStatus(CloningPodStatus.Cloning, AutoCloningPodComponent);
        }

        private HumanoidCharacterProfile GetPlayerProfile(IPlayerSession p)
        {
            return (HumanoidCharacterProfile) _prefsManager.GetPreferences(p.UserId).SelectedCharacter;
        }

        private EntityUid SpawnAutoClonedPlayer(AutoCloningPodComponent clonePod, IPlayerSession player)
        {
            var profile = GetPlayerProfile(player);

            var entity = EntityManager.SpawnEntity(
            _prototypeManager.Index<SpeciesPrototype>(profile?.Species ?? HumanoidAppearanceSystem.DefaultSpecies).Prototype,
            Transform(clonePod.Owner).MapPosition);

            if (profile != null)
            {
                _humanoidSystem.LoadProfile(entity, profile);
                EntityManager.GetComponent<MetaDataComponent>(entity).EntityName = profile.Name;
                if (profile.FlavorText != "" && _configurationManager.GetCVar(CCVars.FlavorText))
                {
                    EntityManager.AddComponent<DetailExaminableComponent>(entity).Content = profile.FlavorText;
                }
            }

            return entity;
        }

        public bool TryCloning(EntityUid uid, IPlayerSession player, Mind.Mind mind, AutoCloningPodComponent? clonePod)
        {

            if (!Resolve(uid, ref clonePod))
                return false;

            if (HasComp<ActiveCloningPodComponent>(uid))
                return false;

            if (mind.OwnedEntity != null && !_mobStateSystem.IsDead(mind.OwnedEntity.Value))
                return false; // Body controlled by mind is not dead

            var mob = SpawnAutoClonedPlayer(clonePod, player);

            var cloneMindReturn = EntityManager.AddComponent<BeingClonedComponent>(mob);
            cloneMindReturn.Mind = mind;
            cloneMindReturn.Parent = clonePod.Owner;
            clonePod.BodyContainer.Insert(mob);

            UpdateStatus(CloningPodStatus.NoMind, clonePod);
            TransferMindToClone(mob,mind);
            
            AddComp<ActiveCloningPodComponent>(uid);

            // TODO: Ideally, components like this should be on a mind entity so this isn't neccesary.
            // Remove this when 'mind entities' are added.
            // Add on special job components to the mob.
            if (mind.CurrentJob != null)
            {
                foreach (var special in mind.CurrentJob.Prototype.Special)
                {
                    if (special is AddComponentSpecial)
                        special.AfterEquip(mob);
                }
            }

            return true;
        }

        public void UpdateStatus(CloningPodStatus status, AutoCloningPodComponent cloningPod)
        {
            cloningPod.Status = status;
            _appearance.SetData(cloningPod.Owner, CloningPodVisuals.Status, cloningPod.Status);
        }

        public override void Update(float frameTime)
        {
            foreach (var (_, cloning) in EntityManager.EntityQuery<ActiveCloningPodComponent, AutoCloningPodComponent>())
            {
                if (cloning.BodyContainer.ContainedEntity == null)
                    continue;

                cloning.CloningProgress += frameTime;
                if (cloning.CloningProgress < cloning.CloningTime)
                    continue;

                Eject(cloning.Owner, cloning);
            }
        }

        public void Eject(EntityUid uid, AutoCloningPodComponent? clonePod)
        {
            if (!Resolve(uid, ref clonePod))
                return;

            if (clonePod.BodyContainer.ContainedEntity is not {Valid: true} entity || clonePod.CloningProgress < clonePod.CloningTime)
                return;

            EntityManager.RemoveComponent<BeingClonedComponent>(entity);
            clonePod.BodyContainer.Remove(entity);
            clonePod.CloningProgress = 0f;
            UpdateStatus(CloningPodStatus.Idle, clonePod);
            RemCompDeferred<ActiveCloningPodComponent>(uid);
        }
    }
}
