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
    public float WindAcceleration = 6f;

    [DataField]
    public float MaxWindSpeed = 4f;

    public Direction WindFrom;
    public MapId Map;
    public TimeSpan WindStartsAt;
}
