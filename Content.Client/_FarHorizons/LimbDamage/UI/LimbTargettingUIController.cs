using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._FarHorizons.LimbDamage.Components;
using Content.Shared.Body.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
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
        _sprites = _entities.System<SpriteSystem>();
        Refresh();
    }

    private void OnPlayerAttached(EntityUid _) => Refresh();

    private void OnPlayerDetached(EntityUid _) => Hide();

    private void OnTargetChanged(LimbTarget target)
    {
        var widget = GetWidget();
        if (widget == null)
            return;

        widget.SetTarget(target);
        widget.Visible = true;
    }

    private void Refresh()
    {
        var widget = GetWidget();
        if (widget == null)
            return;

        if (_player.LocalEntity is not { } player ||
            !_entities.HasComponent<BodyComponent>(player) ||
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
