using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._FarHorizons.LimbDamage.Components;
using Content.Shared.Body.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Client._FarHorizons.LimbDamage.UI;

[UsedImplicitly]
public sealed class LimbTargettingUIController : UIController, IOnSystemLoaded<LimbTargettingSystem>
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private LimbTargettingSystem? _targettingSystem;
    private SpriteSystem? _sprites;

    public override void Initialize()
    {
        base.Initialize();

        var screenLoad = UIManager.GetUIController<GameplayStateLoadController>();
        screenLoad.OnScreenLoad += Refresh;
        screenLoad.OnScreenUnload += Hide;
        _player.LocalPlayerAttached += OnPlayerAttached;
        _player.LocalPlayerDetached += OnPlayerDetached;
    }

    public void OnSystemLoaded(LimbTargettingSystem system)
    {
        _targettingSystem = system;
        system.TargetChanged += OnTargetChanged;
        system.LocalBodyInitialized += OnBodyInitialized;
        _sprites = _entities.System<SpriteSystem>();
        Refresh();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_player.LocalEntity is not { } player ||
            !_entities.HasComponent<BodyComponent>(player) &&
            !_entities.HasComponent<LimbTargettingComponent>(player))
            return;

        var widget = GetWidget();
        if (widget == null || widget.Visible && widget.HasTargetDisplay)
            return;

        Refresh(player);
    }

    private void OnPlayerAttached(EntityUid player) => Refresh(player);

    // The player attach event can arrive before the replicated body component.
    // Refresh again when it is initialized so the HUD does not stay hidden.
    private void OnBodyInitialized(EntityUid player)
    {
        if (player == _player.LocalEntity)
            Refresh(player);
    }

    private void OnPlayerDetached(EntityUid _) => Hide();

    private void OnTargetChanged(LimbTarget target)
    {
        var widget = GetWidget();
        if (widget == null)
            return;

        if (!widget.HasTargetDisplay)
            Refresh(_player.LocalEntity);

        widget.SetTarget(target);
        widget.Visible = widget.HasTargetDisplay;
    }

    private void Refresh() => Refresh(_player.LocalEntity);

    private void Refresh(EntityUid? attachedEntity)
    {
        var widget = GetWidget();
        if (widget == null)
            return;

        if (attachedEntity is not { } player ||
            !_entities.HasComponent<BodyComponent>(player) &&
            !_entities.HasComponent<LimbTargettingComponent>(player) ||
            _sprites == null || _targettingSystem == null)
        {
            widget.ShutdownTarget();
            widget.Visible = false;
            return;
        }

        widget.InitTarget(_prototypes, _sprites, "LimbTargetHuman");
        widget.TargetSelected -= OnTargetSelected;
        widget.TargetSelected += OnTargetSelected;
        widget.SetTarget(_targettingSystem.CurrentTarget);
        widget.Visible = true;
    }

    private void OnTargetSelected(LimbTarget target) => _targettingSystem?.SetTarget(target);

    private LimbTargettingUI? GetWidget() => UIManager.GetActiveUIWidgetOrNull<LimbTargettingUI>();

    private void Hide()
    {
        var widget = GetWidget();
        if (widget == null)
            return;

        widget.TargetSelected -= OnTargetSelected;
        widget.ShutdownTarget();
        widget.Visible = false;
    }
}
