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
    }
}
