using Content.Server._PV.Weather.Systems;

namespace Content.Server._PV.Weather.Components;

/// <summary>
/// Periodically creates controlled lightning strikes while a thunderstorm weather effect is active.
/// </summary>
[RegisterComponent, Access(typeof(ThunderstormLightningSystem))]
public sealed partial class ThunderstormLightningComponent : Component
{
    [DataField]
    public TimeSpan MinStrikeInterval = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan MaxStrikeInterval = TimeSpan.FromSeconds(25);

    [DataField]
    public float MobTargetChance = 0.65f;

    [DataField]
    public int ShockDamage = 15;

    [DataField]
    public TimeSpan ElectrocutionTime = TimeSpan.FromSeconds(2);

    public TimeSpan NextStrike;
}
