using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
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

    private DoomViewOverlay? _overlay;
    public bool Enabled => _overlay != null;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PixelToMapEvent>(OnPixelToMap);
    }

    public bool Toggle()
    {
        if (_overlay != null)
        {
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

    public override void FrameUpdate(float frameTime)
    {
        if (Enabled && (_lights.LockConsoleAccess || _players.LocalEntity == null))
            Toggle();
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
        if (_overlay == null || !_overlay.TryPick(args.Viewport, args.LocalPosition, out var position))
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
