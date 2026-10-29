using System;
using System.Numerics;
using Robust.Client.Input;
using Robust.Client.UserInterface;

namespace Content.Client._PV.DoomView;

internal sealed class DoomMouseCaptureControl : Control
{
    private readonly IInputManager _input;
    private readonly Action<Vector2> _onMouseMove;

    public DoomMouseCaptureControl(IInputManager input, Action<Vector2> onMouseMove)
    {
        _input = input;
        _onMouseMove = onMouseMove;
        MouseFilter = MouseFilterMode.Stop;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _onMouseMove(args.Relative * UIScale);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (!args.Handled)
            _input.ViewportKeyEvent(null, args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (!args.Handled)
            _input.ViewportKeyEvent(null, args);
    }
}
