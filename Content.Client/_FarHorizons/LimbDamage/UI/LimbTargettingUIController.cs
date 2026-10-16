using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._FarHorizons.LimbDamage.Components;
using Content.Shared.Body.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._FarHorizons.LimbDamage.UI;

public sealed class LimbTargettingUIController : UIController, IOnSystemLoaded<LimbTargettingSystem>
{
    [Dependency] private readonly IPlayerManager _player = default!;
    private LimbTargettingSystem? _targettingSystem;

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
    }

    private void OnPlayerAttached(EntityUid _) => Refresh();
    private void OnPlayerDetached(EntityUid _) => Hide();

    private void OnTargetChanged(LimbTarget target)
    {
        var widget = UIManager.GetActiveUIWidgetOrNull<LimbTargettingUI>();
        if (widget == null)
            return;

        widget.Visible = true;
        widget.SetTarget(target);
    }

    private void Refresh()
    {
        var widget = UIManager.GetActiveUIWidgetOrNull<LimbTargettingUI>();
        if (widget == null)
            return;

        if (_player.LocalEntity is not { } player ||
            !EntityManager.HasComponent<BodyComponent>(player) &&
            !EntityManager.HasComponent<LimbTargettingComponent>(player))
        {
            widget.Visible = false;
            return;
        }

        widget.Visible = true;
        widget.TargetSelected -= OnTargetSelected;
        widget.TargetSelected += OnTargetSelected;
        widget.SetTarget(_targettingSystem?.CurrentTarget ?? LimbTarget.Random);
    }

    private void OnTargetSelected(LimbTarget target) => _targettingSystem?.SetTarget(target);

    private void Hide()
    {
        var widget = UIManager.GetActiveUIWidgetOrNull<LimbTargettingUI>();
        if (widget != null)
            widget.Visible = false;
    }
}
