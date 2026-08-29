using Content.Shared._PV.Drugs;
using Content.Shared.Drugs;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Client.Drugs;

/// <summary>
///     System to handle drug related overlays.
/// </summary>
public sealed partial class DrugOverlaySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IRobustRandom _random = default!;

    private RainbowOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<PotentSeeingRainbowsStatusEffectComponent, StatusEffectAppliedEvent>(OnPotentApplied);
        SubscribeLocalEvent<PotentSeeingRainbowsStatusEffectComponent, StatusEffectRemovedEvent>(OnPotentRemoved);

        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnPlayerAttached);
        SubscribeLocalEvent<SeeingRainbowsStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnPlayerDetached);
        SubscribeLocalEvent<PotentSeeingRainbowsStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnPotentPlayerAttached);

        _overlay = new();
    }

    private void OnRemoved(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _overlay.Intoxication = 0;
        _overlay.TimeTicker = 0;
        _overlay.Potent = false;
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnPotentApplied(Entity<PotentSeeingRainbowsStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _overlay.Potent = true;
        _overlay.Intoxication = RainbowOverlay.MaximumIntoxication;
        _overlay.TimeTicker = 0;
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPotentRemoved(Entity<PotentSeeingRainbowsStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _overlay.Potent = false;
    }

    private void OnApplied(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        _overlay.Phase = _random.NextFloat(MathF.Tau); // random starting phase for movement effect
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerAttached(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPotentPlayerAttached(Entity<PotentSeeingRainbowsStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        _overlay.Potent = true;
        _overlay.Intoxication = RainbowOverlay.MaximumIntoxication;
        _overlay.TimeTicker = 0;
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(Entity<SeeingRainbowsStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        _overlay.Intoxication = 0;
        _overlay.TimeTicker = 0;
        _overlay.Potent = false;
        _overlayMan.RemoveOverlay(_overlay);
    }
}
