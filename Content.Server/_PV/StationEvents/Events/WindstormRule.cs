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
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Station.Components;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
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
    [Dependency] private ThrowingSystem _throwing = default!;
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
        component.NextGust = Timing.CurTime + component.WeatherDelay + RandomGustInterval(component);

        Timer.Spawn(component.WeatherDelay, () =>
        {
            if (GameTicker.IsGameRuleActive(uid, gameRule))
                _weather.TryAddWeather(component.Map, "WeatherWindstorm", out _);
        });
    }

    protected override void ActiveTick(EntityUid uid, WindstormRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (Timing.CurTime < component.NextGust)
            return;

        component.NextGust = Timing.CurTime + RandomGustInterval(component);
        ApplyGust(component);
    }

    protected override void Ended(EntityUid uid, WindstormRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);
        _weather.TryRemoveWeather(component.Map, "WeatherWindstorm");
    }

    private void ApplyGust(WindstormRuleComponent component)
    {
        var push = WindPushVector(component.WindFrom);
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var mobState, out var xform))
        {
            if (xform.MapID != component.Map ||
                mobState.CurrentState == MobState.Dead ||
                HasActiveMagboots(uid) ||
                !_random.Prob(component.PushChance) ||
                !CanPushSafely(xform, push, out var destination))
                continue;

            _throwing.TryThrow(
                uid,
                destination,
                component.PushSpeed,
                pushbackRatio: 0f,
                compensateFriction: true,
                recoil: false,
                playSound: false,
                doSpin: false);
        }

        var objectQuery = EntityQueryEnumerator<PhysicsComponent, TransformComponent>();
        while (objectQuery.MoveNext(out var uid, out _, out var xform))
        {
            // Living mobs were handled above. Dead bodies behave like other loose objects.
            if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState != MobState.Dead)
                continue;

            if (xform.MapID != component.Map ||
                xform.Anchored ||
                _containers.IsEntityInContainer(uid) ||
                !_random.Prob(component.ObjectPushChance) ||
                !CanPushSafely(xform, push, out var destination))
                continue;

            _throwing.TryThrow(
                uid,
                destination,
                component.ObjectPushSpeed,
                pushbackRatio: 0f,
                compensateFriction: true,
                recoil: false,
                playSound: false,
                doSpin: true);
        }
    }

    private bool HasActiveMagboots(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "shoes", out var shoes) &&
               HasComp<MagbootsComponent>(shoes.Value) &&
               _itemToggle.IsActivated(shoes.Value);
    }

    private bool CanPushSafely(TransformComponent xform, Vector2 push, out EntityCoordinates destination)
    {
        destination = xform.Coordinates.Offset(push);

        if (xform.GridUid is not { } gridUid || !_gridQuery.TryGetComponent(gridUid, out var grid))
            return false;

        var currentTile = _map.GetTileRef(gridUid, grid, xform.Coordinates);
        var destinationTile = _map.GetTileRef(gridUid, grid, destination);
        var roof = _roofQuery.TryGetComponent(gridUid, out var roofComp) ? roofComp : null;
        var gridEntity = new Entity<MapGridComponent?, RoofComponent?>(gridUid, grid, roof);

        return !destinationTile.Tile.IsEmpty &&
               _weather.CanWeatherAffect(gridEntity, currentTile) &&
               _weather.CanWeatherAffect(gridEntity, destinationTile);
    }

    private TimeSpan RandomGustInterval(WindstormRuleComponent component)
    {
        return TimeSpan.FromSeconds(_random.NextFloat(
            (float) component.MinGustInterval.TotalSeconds,
            (float) component.MaxGustInterval.TotalSeconds));
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
