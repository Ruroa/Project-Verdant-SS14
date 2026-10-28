using System.Numerics;

namespace Content.Client._PV.DoomView;

internal static class DoomUv
{
    public static Vector2[] Tile(int rotationMirroring)
    {
        var bottomLeft = new Vector2(0, 1);
        var bottomRight = Vector2.One;
        var topRight = new Vector2(1, 0);
        var topLeft = Vector2.Zero;
        for (var i = 0; i < rotationMirroring % 4; i++)
            (bottomLeft, bottomRight, topRight, topLeft) = (topLeft, bottomLeft, bottomRight, topRight);
        var uv = new[] { topLeft, topRight, bottomRight, bottomLeft };
        if (rotationMirroring >= 4)
        {
            for (var i = 0; i < uv.Length; i++)
            {
                if (rotationMirroring % 2 == 0)
                    uv[i].X = 1 - uv[i].X;
                else
                    uv[i].Y = 1 - uv[i].Y;
            }
        }
        return uv;
    }
}
