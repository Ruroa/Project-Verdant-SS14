using System.Numerics;
using Content.Server._PV.StationEvents.Components;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Server.Weather;
using Content.Shared.Clothing;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Light.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._PV.StationEvents.Events;

public sealed partial class WindstormRule : StationEventSystem<WindstormRuleComponent>
{
    private static readonly Direction[] CardinalDirections =
    [
        Direction.North,
        Direction.South,
        Direction.East,
        Direction.West,
    ];

    [Dependency] private WeatherSystem _weather = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IRobustRandom _random = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<RoofComponent> _roofQuery;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _roofQuery = GetEntityQuery<RoofComponent>();
    }

    protected override void Added(EntityUid uid, WindstormRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        component.WindFrom = _random.Pick(CardinalDirections);

        if (TryComp<StationEventComponent>(uid, out var stationEvent))
        {
            var direction = Loc.GetString($"wind-direction-{component.WindFrom.ToString().ToLowerInvariant()}");
            stationEvent.StartAnnouncement = Loc.GetString(
                "station-event-windstorm-start-announcement",
                ("direction", direction));
        }

        base.Added(uid, component, gameRule, args);
    }

    protected override void Started(EntityUid uid, WindstormRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryComp<StationEventComponent>(uid, out var stationEvent) ||
            stationEvent.TargetStation is not { } station ||
            !TryComp<StationDataComponent>(station, out var stationData))
            return;

        var grid = StationSystem.GetLargestGrid((station, stationData));
        if (grid is null)
            return;

        component.Map = Transform(grid.Value).MapID;
        component.WindStartsAt = Timing.CurTime + component.WeatherDelay;
        Timer.Spawn(component.WeatherDelay, () =>
        {
            if (GameTicker.IsGameRuleActive(uid, gameRule))
                _weather.TryAddWeather(component.Map, "WeatherWindstorm", out _);
        });
    }

    protected override void ActiveTick(EntityUid uid, WindstormRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (Timing.CurTime < component.WindStartsAt)
            return;

        ApplyWind(component, frameTime);
    }

    protected override void Ended(EntityUid uid, WindstormRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
        _weather.TryRemoveWeather(component.Map, "WeatherWindstorm");
    }

    private void ApplyWind(WindstormRuleComponent component, float frameTime)
    {
        var direction = WindPushVector(component.WindFrom);
        var query = EntityQueryEnumerator<PhysicsComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var body, out var xform))
        {
            if (xform.MapID != component.Map ||
                xform.Anchored ||
                _containers.IsEntityInContainer(uid) ||
                HasActiveMagboots(uid) ||
                !IsExposedToWeather(xform))
                continue;

            var downwindSpeed = Vector2.Dot(body.LinearVelocity, direction);
            if (downwindSpeed >= component.MaxWindSpeed)
                continue;

            var acceleration = MathF.Min(
                component.WindAcceleration,
                (component.MaxWindSpeed - downwindSpeed) / frameTime);
            _physics.ApplyLinearImpulse(
                uid,
                direction * acceleration * body.Mass * frameTime,
                body: body);
        }
    }

    private bool HasActiveMagboots(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "shoes", out var shoes) &&
               HasComp<MagbootsComponent>(shoes.Value) &&
               _itemToggle.IsActivated(shoes.Value);
    }

    private bool IsExposedToWeather(TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !_gridQuery.TryGetComponent(gridUid, out var grid))
            return false;

        var currentTile = _map.GetTileRef(gridUid, grid, xform.Coordinates);
        var roof = _roofQuery.TryGetComponent(gridUid, out var roofComp) ? roofComp : null;
        var gridEntity = new Entity<MapGridComponent?, RoofComponent?>(gridUid, grid, roof);

        return !currentTile.Tile.IsEmpty && _weather.CanWeatherAffect(gridEntity, currentTile);
    }

    private static Vector2 WindPushVector(Direction windFrom)
    {
        return windFrom switch
        {
            Direction.North => new Vector2(0f, -1f),
            Direction.South => new Vector2(0f, 1f),
            Direction.East => new Vector2(-1f, 0f),
            Direction.West => new Vector2(1f, 0f),
            _ => Vector2.Zero,
        };
    }
}
