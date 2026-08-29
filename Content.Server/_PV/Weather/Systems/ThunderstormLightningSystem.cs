using System.Numerics;
using Content.Server._PV.Weather.Components;
using Content.Server.Lightning;
using Content.Server.Weather;
using Content.Shared.Electrocution;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._PV.Weather.Systems;

public sealed class ThunderstormLightningSystem : EntitySystem
{
    private const string LightningPrototype = "PVWeatherLightning";
    private const string MarkerPrototype = "PVWeatherLightningMarker";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private WeatherSystem _weather = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<RoofComponent> _roofQuery;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _roofQuery = GetEntityQuery<RoofComponent>();

        SubscribeLocalEvent<ThunderstormLightningComponent, ComponentInit>(OnComponentInit);
    }

    private void OnComponentInit(Entity<ThunderstormLightningComponent> ent, ref ComponentInit args)
    {
        ent.Comp.NextStrike = _timing.CurTime + ent.Comp.InitialStrikeDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ThunderstormLightningComponent, StatusEffectComponent>();
        while (query.MoveNext(out _, out var lightning, out var status))
        {
            if (_timing.CurTime < lightning.NextStrike)
                continue;

            lightning.NextStrike = _timing.CurTime + RandomInterval(lightning);

            if (status.AppliedTo is not { } mapUid)
                continue;

            Strike(mapUid, lightning);
        }
    }

    private void Strike(EntityUid mapUid, ThunderstormLightningComponent component)
    {
        EntityUid? mobTarget = null;
        if (_random.Prob(component.MobTargetChance))
            mobTarget = PickExposedMob(mapUid);

        EntityCoordinates strikeCoordinates;
        EntityUid target;

        if (mobTarget is { } mob)
        {
            strikeCoordinates = Transform(mob).Coordinates;
            target = mob;
        }
        else
        {
            var randomCoordinates = PickExposedTile(mapUid);
            if (randomCoordinates is null)
                return;

            strikeCoordinates = randomCoordinates.Value;
            target = Spawn(MarkerPrototype, strikeCoordinates);
        }

        // Base lightning only reaches five tiles. Keep the temporary sky source inside that range.
        var sourceCoordinates = strikeCoordinates.Offset(new Vector2(0f, 4f));
        var source = Spawn(MarkerPrototype, sourceCoordinates);

        // The weather bolt uses Tesla's beam visual, but deliberately disables Tesla target events,
        // chaining and explosions so the event remains predictable.
        _lightning.ShootLightning(source, target, LightningPrototype, triggerLightningEvents: false);

        if (mobTarget is { } struckMob)
        {
            _electrocution.TryDoElectrocution(
                struckMob,
                source,
                component.ShockDamage,
                component.ElectrocutionTime,
                refresh: true);
        }
    }

    private EntityUid? PickExposedMob(EntityUid mapUid)
    {
        EntityUid? selected = null;
        var count = 0;

        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var xform))
        {
            if (xform.MapUid != mapUid || mobState.CurrentState == MobState.Dead || !IsExposed(xform))
                continue;

            count++;
            if (_random.Next(count) == 0)
                selected = uid;
        }

        return selected;
    }

    private EntityCoordinates? PickExposedTile(EntityUid mapUid)
    {
        EntityCoordinates? selected = null;
        var count = 0;

        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var gridUid, out var grid, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            var roof = _roofQuery.TryGetComponent(gridUid, out var roofComp) ? roofComp : null;
            var gridEntity = new Entity<MapGridComponent?, RoofComponent?>(gridUid, grid, roof);

            foreach (var tile in _map.GetAllTiles(gridUid, grid))
            {
                if (!_weather.CanWeatherAffect(gridEntity, tile))
                    continue;

                count++;
                if (_random.Next(count) == 0)
                    selected = _map.GridTileToLocal(gridUid, grid, tile.GridIndices);
            }
        }

        return selected;
    }

    private bool IsExposed(TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !_gridQuery.TryGetComponent(gridUid, out var grid))
            return true;

        var tile = _map.GetTileRef(gridUid, grid, xform.Coordinates);
        var roof = _roofQuery.TryGetComponent(gridUid, out var roofComp) ? roofComp : null;
        return _weather.CanWeatherAffect(new Entity<MapGridComponent?, RoofComponent?>(gridUid, grid, roof), tile);
    }

    private TimeSpan RandomInterval(ThunderstormLightningComponent component)
    {
        var seconds = _random.NextFloat(
            (float) component.MinStrikeInterval.TotalSeconds,
            (float) component.MaxStrikeInterval.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
