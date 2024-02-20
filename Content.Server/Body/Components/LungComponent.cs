using Content.Server.Atmos;
using Content.Server.Body.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;

namespace Content.Server.Body.Components;

[RegisterComponent, Access(typeof(LungSystem))]
public sealed class LungComponent : Component
{
    [DataField("air")]
    [Access(typeof(LungSystem), Other = AccessPermissions.ReadExecute)] // FIXME Friends
    public GasMixture Air { get; set; } = new()
    {
        Volume = 6,
        Temperature = Atmospherics.NormalBodyTemperature
    };

    [ViewVariables]
    [Access(typeof(LungSystem), Other = AccessPermissions.ReadExecute)] // FIXME Friends
    public Solution LungSolution = default!;

    //the modifier that sets the amount of damage done to the lungs whenever narcotics or toxins are inhaled
    //proportional to reagent metabolised
    [DataField("damageMod")]
    public float DamageMod = 0.0025f;

    //amount of damage done to the lungs
    [ViewVariables]
    public float Damage = 0f;

    //Current Condition
    [ViewVariables]
    [Access(typeof(LungSystem), Other = AccessPermissions.ReadExecute)]
    public OrganCondition Condition = OrganCondition.Good;

    //Warning Damage
    public float WarningDamage = 0.05f;

    //Critical Damage
    public float CriticalDamage = 0.18f;

    //reagent groups that can cause damage - default poisons and narcotics
    [DataField("damageGroups")]
    public List<string> DamageGroups = new List<string>() {"Poison","Narcotic"};
}
