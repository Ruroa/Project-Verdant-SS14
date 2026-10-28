using Content.Client._PV.DoomView;
using Robust.Client.UserInterface;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_entityManager.EntitySysManager.TryGetEntitySystem<DoomViewSystem>(out var doomView))
            doomView.MouseLook(args.Relative.X * UIScale);
    }
}
