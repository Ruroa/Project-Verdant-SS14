using Content.Server.Parallax;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Systems;

public sealed partial class StationBiomeSystem : EntitySystem
{
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationBiomeComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(Entity<StationBiomeComponent> map, ref StationPostInitEvent args)
    {
        var station = _station.GetLargestGrid(map.Owner);
        if (station == null)
            return;

        var mapId = Transform(station.Value).MapID;
        var mapUid = _map.GetMapOrInvalid(mapId);

        _biome.EnsurePlanet(mapUid, _proto.Index(map.Comp.Biome), map.Comp.Seed, mapLight: map.Comp.MapLightColor);
        EnsureComp<PlanetarySurfaceComponent>(mapUid);

        if (!TryComp(mapUid, out BiomeComponent? biome))
            return;

        foreach (var marker in map.Comp.OreMarkers)
        {
            _biome.AddMarkerLayer(mapUid, biome, marker);
        }
    }
}
