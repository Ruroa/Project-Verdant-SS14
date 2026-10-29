using System.Numerics;
using Content.Shared._PV.DoomView;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Client._PV.DoomView;

public sealed partial class DoomViewSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private ILightManager _lights = default!;
    [Dependency] private SharedTransformSystem _transforms = default!;
    [Dependency] private SharedMoverController _mover = default!;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    private float _pendingTurn;
    private float _lookPitch;
    private bool _middleWasDown;
    private const float MaxLookPitch = 0.6f;
    private float _hitFlashRemaining;
    private const float HitFlashDuration = 0.65f;
    public float HitFlashAlpha => _hitFlashRemaining <= 0
        ? 0f
        : 0.13f * MathF.Sin(MathF.PI * (1f - _hitFlashRemaining / HitFlashDuration));
    public float LookPitch => _lookPitch;
    public bool MouseLooking { get; private set; }
    private DoomViewOverlay? _overlay;
    public bool Enabled => _overlay != null;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PixelToMapEvent>(OnPixelToMap);
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnLocalDamage);
    }

    public bool Toggle()
    {
        if (_overlay != null)
        {
            StopMouseLook();
            _overlays.RemoveOverlay(_overlay);
            _overlay.Dispose();
            _overlay = null;
            return false;
        }

        if (_lights.LockConsoleAccess || _players.LocalEntity == null)
            return false;

        _overlay = new DoomViewOverlay(this, EntityManager);
        _overlays.AddOverlay(_overlay);
        return true;
    }

    public bool HandleLookKey(GUIBoundKeyEventArgs args, bool down)
    {
        // Do not intercept UI mouse bindings: normal context menus must keep working.
        return false;
    }

    public void MouseLook(float horizontalDelta, float verticalDelta)
    {
        if (!MouseLooking)
            return;
        if (float.IsFinite(horizontalDelta))
            _pendingTurn = Math.Clamp(_pendingTurn - horizontalDelta * 0.003f, -0.5f, 0.5f);
        if (float.IsFinite(verticalDelta))
            // Move mouse up to look up, down to look down (standard FPS pitch).
            _lookPitch = Math.Clamp(_lookPitch - verticalDelta * 0.002f, -MaxLookPitch, MaxLookPitch);
    }

    private void StopMouseLook()
    {
        if (MouseLooking)
            _clyde.MainWindow.SetRelativeMouseMode(false);
        MouseLooking = false;
        _pendingTurn = 0;
    }

    public override void FrameUpdate(float frameTime)
    {
        _hitFlashRemaining = MathF.Max(0, _hitFlashRemaining - frameTime);
        // Toggle on the press edge; never intercept right-click or other UI keys.
        var middleDown = _input.IsKeyDown(Keyboard.Key.MouseMiddle);
        var pressed = middleDown && !_middleWasDown;
        _middleWasDown = middleDown;
        var canLook = Enabled && _clyde.MainWindow.IsFocused;
        if (!canLook)
            StopMouseLook();
        else if (pressed)
        {
            if (MouseLooking)
                StopMouseLook();
            else if (_ui.KeyboardFocused == null)
            {
                MouseLooking = true;
                _clyde.MainWindow.SetRelativeMouseMode(true);
            }
        }
        if (MouseLooking && MathF.Abs(_pendingTurn) > 0.0001f)
        {
            RaisePredictiveEvent(new DoomLookEvent(_pendingTurn));
            _pendingTurn = 0;
        }
        if (Enabled && (_lights.LockConsoleAccess || _players.LocalEntity == null))
            Toggle();
    }

    private void OnLocalDamage(EntityUid uid, DamageableComponent component, DamageChangedEvent args)
    {
        if (Enabled && uid == _players.LocalEntity && IsHitDamage(args))
            _hitFlashRemaining = HitFlashDuration;
    }

    private static bool IsHitDamage(DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return false;

        foreach (var (type, amount) in delta.DamageDict)
        {
            if (amount <= 0)
                continue;

            if (type is "Blunt" or "Piercing" or "Slash" or "Heat" or "Shock")
                return true;
        }

        return false;
    }

    public override void Shutdown()
    {
        if (Enabled)
            Toggle();
        base.Shutdown();
    }

    internal bool TryCamera(out EntityUid player, out MapCoordinates origin, out Vector2 forward)
    {
        player = default;
        origin = default;
        forward = Vector2.UnitY;
        if (_players.LocalEntity is not { } uid || !TryComp(uid, out TransformComponent? transform))
            return false;

        player = uid;
        origin = _transforms.GetMapCoordinates(uid);
        var rotation = TryComp<InputMoverComponent>(uid, out var mover)
            ? _mover.GetParentGridAngle(mover)
            : transform.GridUid is { } grid ? _transforms.GetWorldRotation(grid) : Angle.Zero;
        forward = rotation.RotateVec(Vector2.UnitY);
        return origin.MapId != MapId.Nullspace;
    }

    private void OnPixelToMap(ref PixelToMapEvent args)
    {
        var pixel = MouseLooking ? (Vector2) args.Viewport.Size / 2f : args.LocalPosition;
        if (_overlay == null || !_overlay.TryPick(args.Viewport, pixel, out var position))
            return;

        args.VisiblePosition = args.Viewport.WorldToLocal(position);
    }
}

public sealed partial class DoomViewCommand : LocalizedEntityCommands
{
    [Dependency] private DoomViewSystem _view = default!;
    public override string Command => "doomview";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Loc.GetString("cmd-doomview-help"));
            return;
        }
        shell.WriteLine(Loc.GetString(_view.Toggle() ? "cmd-doomview-enabled" : "cmd-doomview-disabled"));
    }
}
