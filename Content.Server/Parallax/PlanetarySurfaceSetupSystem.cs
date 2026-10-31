using Content.Server.Parallax.Components;
using Robust.Shared.Map;

namespace Content.Server.Parallax;

public sealed class PlanetarySurfaceSetupSystem : EntitySystem
{
    [Dependency] private BiomeSystem _biome = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlanetarySurfaceSetupComponent, MapInitEvent>(OnMapInit);
    }

    public void Configure(EntityUid mapUid)
    {
        EntityManager.EnsureComponent<PlanetarySurfaceSetupComponent>(mapUid);
        _biome.EnsureVerdantPlanet(mapUid);
    }

    private void OnMapInit(EntityUid uid, PlanetarySurfaceSetupComponent component, MapInitEvent args)
    {
        _biome.EnsureVerdantPlanet(uid);
    }
}
