using System.Numerics;

namespace Content.Client._PV.DoomView;

internal static class DoomSpriteDirection
{
    public static Direction FromCamera(Angle entityRotation, Vector2 cameraForward)
    {
        var cameraSide = Angle.FromWorldVec(-Vector2.Normalize(cameraForward));
        return (cameraSide - entityRotation).GetDir();
    }
}
