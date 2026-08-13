using Content.Shared._PV.Ranching;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Client._PV.Ranching;

public sealed class ReagentCowVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ReagentCowComponent, MapInitEvent>(OnVisualUpdate);
        SubscribeLocalEvent<ReagentCowComponent, AfterAutoHandleStateEvent>(OnVisualUpdate);
    }

    private void OnVisualUpdate(Entity<ReagentCowComponent> ent, ref MapInitEvent args) => UpdateVisuals(ent);
    private void OnVisualUpdate(Entity<ReagentCowComponent> ent, ref AfterAutoHandleStateEvent args) => UpdateVisuals(ent);

    private void UpdateVisuals(Entity<ReagentCowComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) ||
            !_prototypes.TryIndex(ent.Comp.Reagent, out ReagentPrototype? reagent))
            return;

        _sprite.LayerSetColor((ent.Owner, sprite), ReagentCowVisualLayers.Base, reagent.SubstanceColor);
    }
}
