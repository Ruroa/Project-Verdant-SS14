using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._PV.DoomView;

internal sealed class DoomGeometry
{
    public readonly List<Patch> Patches = new();
    private Vector2 _origin;
    private Vector2 _forward;
    private float _width;
    private float _horizon;
    private Func<Vector2, Color>? _lighting;

    public void Begin(Vector2 origin, Vector2 forward, float width, float horizon,
        Func<Vector2, Color>? lighting = null)
    {
        Patches.Clear();
        _origin = origin;
        _forward = forward;
        _width = width;
        _horizon = horizon;
        _lighting = lighting;
    }

    public void Quad(EntityUid entity, Texture texture, Color color, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        int layer = 0, Vector2[]? uv = null)
    {
        uv ??= new[] { Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY };
        var center = (a + b + c + d) / 4;
        var centerDepth = Vector2.Dot(new Vector2(center.X, center.Y) - _origin, _forward);
        var extent = MathF.Max(Vector3.Distance(a, c), Vector3.Distance(b, d));
        if (centerDepth + extent < 0.08f || centerDepth - extent > DoomProjection.Range)
            return;
        var steps = centerDepth > 6 ? 1 : centerDepth > 3 ? 2 : 4;
        for (var y = 0; y < steps; y++)
        for (var x = 0; x < steps; x++)
        {
            var u = x / (float) steps;
            var v = y / (float) steps;
            var vertices = Clip(new[] { Make(u, v), Make(u + 1f / steps, v),
                Make(u + 1f / steps, v + 1f / steps), Make(u, v + 1f / steps) });
            if (vertices.Count < 3)
                continue;
            var depth = vertices.Average(p => p.Position.Y);
            if (depth > DoomProjection.Range)
                continue;
            var focal = DoomProjection.FocalLength(_width);
            var projected = vertices.Select(p => new DrawVertexUV2D(new Vector2(
                _width / 2f + p.Position.X * focal / p.Position.Y,
                _horizon + (DoomProjection.EyeHeight - p.Position.Z) * focal / p.Position.Y), p.Uv)).ToArray();
            if (projected.Max(p => p.Position.X) < 0 || projected.Min(p => p.Position.X) > _width)
                continue;
            var shade = Math.Clamp(1f - depth / 16f, 0.2f, 1f);
            var sampleU = u + 0.5f / steps;
            var sampleV = v + 0.5f / steps;
            var sampleWorld = Vector3.Lerp(Vector3.Lerp(a, b, sampleU),
                Vector3.Lerp(d, c, sampleU), sampleV);
            var light = _lighting?.Invoke(new Vector2(sampleWorld.X, sampleWorld.Y)) ?? Color.White;
            Patches.Add(new Patch(entity, texture, color * light * new Color(shade, shade, shade), projected,
                depth, vertices.Min(p => p.Position.Y), layer));

            Vertex Make(float horizontal, float vertical)
            {
                var world = Vector3.Lerp(Vector3.Lerp(a, b, horizontal), Vector3.Lerp(d, c, horizontal), vertical);
                var delta = new Vector2(world.X, world.Y) - _origin;
                return new Vertex(new Vector3(Vector2.Dot(delta, DoomProjection.Right(_forward)),
                    Vector2.Dot(delta, _forward), world.Z), Vector2.Lerp(Vector2.Lerp(uv[0], uv[1], horizontal),
                    Vector2.Lerp(uv[3], uv[2], horizontal), vertical));
            }
        }
    }

    public void Triangle(EntityUid entity, Color color, Vector3 a, Vector3 b, Vector3 c, int layer = 0)
    {
        var normal = Vector3.Cross(b - a, c - a);
        var center = (a + b + c) / 3f;
        var toCamera = new Vector3(_origin.X - center.X, _origin.Y - center.Y,
            DoomProjection.EyeHeight - center.Z);
        if (Vector3.Dot(normal, toCamera) <= 0)
            return;

        var input = new[] { a, b, c };
        var clipped = new List<Vector3>(4);
        var previous = input[^1];
        foreach (var current in input)
        {
            var previousDepth = Vector2.Dot(new Vector2(previous.X, previous.Y) - _origin, _forward);
            var currentDepth = Vector2.Dot(new Vector2(current.X, current.Y) - _origin, _forward);
            if ((previousDepth >= 0.08f) != (currentDepth >= 0.08f))
            {
                var amount = (0.08f - previousDepth) / (currentDepth - previousDepth);
                clipped.Add(Vector3.Lerp(previous, current, amount));
            }
            if (currentDepth >= 0.08f)
                clipped.Add(current);
            previous = current;
        }

        if (clipped.Count < 3)
            return;
        for (var i = 1; i < clipped.Count - 1; i++)
            Project(clipped[0], clipped[i], clipped[i + 1]);

        void Project(Vector3 first, Vector3 second, Vector3 third)
        {
            var focal = DoomProjection.FocalLength(_width);
            var vertices = new[] { first, second, third }.Select(point =>
            {
                var delta = new Vector2(point.X, point.Y) - _origin;
                var depth = Vector2.Dot(delta, _forward);
                var x = Vector2.Dot(delta, DoomProjection.Right(_forward));
                return new DrawVertexUV2D(new Vector2(_width / 2f + x * focal / depth,
                    _horizon + (DoomProjection.EyeHeight - point.Z) * focal / depth), Vector2.Zero);
            }).ToArray();
            if (vertices.Max(vertex => vertex.Position.X) < 0 || vertices.Min(vertex => vertex.Position.X) > _width)
                return;
            var deltaCenter = (first + second + third) / 3f;
            var worldDepth = Vector2.Dot(new Vector2(deltaCenter.X, deltaCenter.Y) - _origin, _forward);
            if (worldDepth > DoomProjection.Range)
                return;
            var sample = _lighting?.Invoke(new Vector2(deltaCenter.X, deltaCenter.Y)) ?? Color.White;
            var shade = Math.Clamp(1f - worldDepth / 16f, 0.2f, 1f);
            Patches.Add(new Patch(entity, Texture.White,
                color * sample * new Color(shade, shade, shade), vertices,
                worldDepth, worldDepth, layer));
        }
    }

    private static List<Vertex> Clip(Vertex[] input)
    {
        const float near = 0.08f;
        var output = new List<Vertex>(5);
        var previous = input[^1];
        foreach (var current in input)
        {
            if ((previous.Position.Y >= near) != (current.Position.Y >= near))
            {
                var t = (near - previous.Position.Y) / (current.Position.Y - previous.Position.Y);
                output.Add(new Vertex(Vector3.Lerp(previous.Position, current.Position, t),
                    Vector2.Lerp(previous.Uv, current.Uv, t)));
            }
            if (current.Position.Y >= near)
                output.Add(current);
            previous = current;
        }
        return output;
    }

    public static void Draw(Patch patch, in OverlayDrawArgs args, float[]? wallDepth = null)
    {
        var left = args.ViewportBounds.Left;
        var top = args.ViewportBounds.Top;
        var width = args.ViewportBounds.Width;
        var texture = patch.Texture;
        var vertices = patch.Vertices.Select(p => new DrawVertexUV2D(p.Position + new Vector2(left, top), p.UV)).ToArray();
        while (texture is AtlasTexture atlas)
        {
            for (var i = 0; i < vertices.Length; i++)
                vertices[i].UV = (atlas.SubRegion.TopLeft + vertices[i].UV * (Vector2) texture.Size) / (Vector2) atlas.SourceTexture.Size;
            texture = atlas.SourceTexture;
        }
        for (var i = 0; i < vertices.Length; i++)
            vertices[i].UV.Y = 1f - vertices[i].UV.Y;
        if (wallDepth == null)
        {
            args.ScreenHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, texture, vertices, patch.Color);
            return;
        }
        var first = Math.Clamp((int) (patch.Vertices.Min(p => p.Position.X) / width * wallDepth.Length), 0, wallDepth.Length - 1);
        var last = Math.Clamp((int) (patch.Vertices.Max(p => p.Position.X) / width * wallDepth.Length), 0, wallDepth.Length - 1);
        var start = -1;
        for (var i = first; i <= last + 1; i++)
        {
            var visible = i <= last && patch.NearDepth < wallDepth[i];
            if (visible && start == -1)
                start = i;
            if (visible || start == -1)
                continue;
            args.RenderHandle.SetScissor(new UIBox2i(left + (int) (start * width / (float) wallDepth.Length), top,
                left + (int) MathF.Ceiling(i * width / (float) wallDepth.Length), args.ViewportBounds.Bottom));
            args.ScreenHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, texture, vertices, patch.Color);
            start = -1;
        }
    }

    public void Pick(Vector2 pixel, ref EntityUid entity, ref float depth)
    {
        foreach (var patch in Patches)
        {
            if (patch.Entity == default || patch.Depth >= depth || !Contains(patch.Vertices, pixel))
                continue;
            entity = patch.Entity;
            depth = patch.Depth;
        }
    }

    private static bool Contains(DrawVertexUV2D[] vertices, Vector2 point)
    {
        var sign = 0;
        for (var i = 0; i < vertices.Length; i++)
        {
            var edge = vertices[(i + 1) % vertices.Length].Position - vertices[i].Position;
            var delta = point - vertices[i].Position;
            var cross = edge.X * delta.Y - edge.Y * delta.X;
            if (MathF.Abs(cross) < 0.001f)
                continue;
            var next = Math.Sign(cross);
            if (sign != 0 && sign != next)
                return false;
            sign = next;
        }
        return sign != 0;
    }

    private readonly record struct Vertex(Vector3 Position, Vector2 Uv);
    internal sealed record Patch(EntityUid Entity, Texture Texture, Color Color, DrawVertexUV2D[] Vertices,
        float Depth, float NearDepth, int Layer);
}
