using Content.Server.Body.Systems;
using Content.Shared.Damage;

namespace Content.Server.Body.Components
{
    [RegisterComponent]
    public sealed class CirculatoryPumpComponent : Component
    {
        //if the CirculatoryPump is embedded, do not remove it from its host whenever an organ is removed
        [DataField("embedded")]
        public bool Embedded = false;

        //some mobs don't the er... the thing that makes you do and stuff
        [DataField("brainless")]
        public bool Brainless = false;

        [ViewVariables(VVAccess.ReadWrite)]
        public bool Working = true;

        [DataField("notWorkingDamage", required: true), ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier NotWorkingDamage = default!;

        public float CheckInterval = 5f; //check every 5 seconds that the heart can pump
        public float IntervalLastChecked = 0f;

        //if the player is overfed, strain is put on the pump
        //every update, existing strain has a chance to become permanent
        //the chance of damage becoming permanent and the permanent damage added are equal to the strain multiplied by the strain damage mod
        //the more strain, the greater the impact and risk
        [ViewVariables(VVAccess.ReadWrite)]
        public float Strain = 0f; //if the player is overfed, put strain on the heart

        public float StrainDamageMod = 0.001f; //mod for strain damage chance (% chance for strain to be permanent damage every 5 seconds, as well as the damage made permanent)
        public float StrainMod = 180f; //negation of overfed ceiling from strain (so that it does not start at 200)
        public float StrainRecovery = 1f; //amount of strain to recover every 5 seconds after player is no longer overfed
        public float StrainCeiling = 0f; //strain ceiling for the period the player is overfed - strain is not increased unless it breaches this value

        [ViewVariables(VVAccess.ReadWrite)]
        public float Damage = 0f;

        public float DamageMod = 0.005f; //chance mod for a heart attack to occur every 5 seconds based on damage

        //consider minimum heart attack damage req
        public float MinDamageThreshold = 2f; //minimum heart damage before heart attacks can occur
        //consider max heart attack damage
        public float MaxDamage = 10f;
    }
}
