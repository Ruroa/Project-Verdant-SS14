using Content.Shared._PV.Ranching;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._PV.Ranching;

public sealed class AnimalFeedVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AnimalFeedComponent, MapInitEvent>(OnVisualUpdate);
        SubscribeLocalEvent<AnimalFeedComponent, AfterAutoHandleStateEvent>(OnVisualUpdate);
    }

    private void OnVisualUpdate(Entity<AnimalFeedComponent> ent, ref MapInitEvent args) => UpdateVisuals(ent);
    private void OnVisualUpdate(Entity<AnimalFeedComponent> ent, ref AfterAutoHandleStateEvent args) => UpdateVisuals(ent);

    private void UpdateVisuals(Entity<AnimalFeedComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.LayerSetColor((ent.Owner, sprite), AnimalFeedVisualLayers.Feed, ent.Comp.Color);
        _sprite.LayerSetVisible((ent.Owner, sprite), AnimalFeedVisualLayers.Feed, true);
    }
}
