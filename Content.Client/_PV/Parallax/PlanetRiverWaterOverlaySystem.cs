using Robust.Client.Graphics;

namespace Content.Client._PV.Parallax;

public sealed class PlanetRiverWaterOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new PlanetRiverWaterOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<PlanetRiverWaterOverlay>();
    }
}
