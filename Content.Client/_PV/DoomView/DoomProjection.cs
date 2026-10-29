using System.Numerics;

namespace Content.Client._PV.DoomView;

internal static class DoomProjection
{
    public const float Range = 12f;
    public const float Plane = 1f; // 90-degree view
    public const float WallHeight = 1.6f;
    public const float EyeHeight = 0.8f;
    public const float NearBubble = 2f;

    public static Vector2 Right(Vector2 forward) => new(forward.Y, -forward.X);

    public static Vector2 Ray(Vector2 forward, float screenX) =>
        Vector2.Normalize(forward + Right(forward) * ((screenX * 2f - 1f) * Plane));

    public static float FocalLength(float width) => width / (2f * Plane);

    public static bool InView(Vector2 delta, Vector2 forward, float padding = 2f)
    {
        var distanceSquared = delta.LengthSquared();
        if (distanceSquared <= NearBubble * NearBubble)
            return true;

        var depth = Vector2.Dot(delta, forward);
        if (depth <= 0 || depth > Range)
            return false;

        var lateral = MathF.Abs(Vector2.Dot(delta, Right(forward)));
        return lateral <= depth * Plane + padding;
    }

    public static bool Project(Vector2 delta, Vector2 forward, float width, out float x, out float depth)
    {
        depth = Vector2.Dot(delta, forward);
        x = 0;
        if (depth < 0.05f || depth > Range)
            return false;
        x = width * 0.5f + Vector2.Dot(delta, Right(forward)) * FocalLength(width) / depth;
        return true;
    }
}
