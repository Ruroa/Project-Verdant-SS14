using Content.Server._PV.StationEvents.Events;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server._PV.StationEvents.Components;

[RegisterComponent, Access(typeof(WindstormRule))]
public sealed partial class WindstormRuleComponent : Component
{
    [DataField]
    public TimeSpan WeatherDelay = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan MinGustInterval = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan MaxGustInterval = TimeSpan.FromSeconds(7);

    [DataField]
    public float PushChance = 0.4f;

    [DataField]
    public float PushSpeed = 2.5f;

    [DataField]
    public float ObjectPushChance = 0.4f;

    [DataField]
    public float ObjectPushSpeed = 3.5f;

    public Direction WindFrom;
    public MapId Map;
    public TimeSpan NextGust;
}
