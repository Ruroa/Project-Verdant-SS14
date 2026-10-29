using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Client._PV.DoomView;

internal sealed class DoomModelMeshes
{
    private const string MeshRoot = "/Models/_PV/DoomView/";
    private readonly IResourceCache _resources = IoCManager.Resolve<IResourceCache>();
    private readonly Dictionary<string, DoomModelMesh?> _cache = new();
    private readonly Dictionary<(string Prototype, string Sprite), string?> _resolved = new();
    private readonly List<ModelRule> _rules;

    public DoomModelMeshes()
    {
        try
        {
            using var document = JsonDocument.Parse(_resources.ContentFileReadAllText(MeshRoot + "model-rules.json"));
            _rules = document.RootElement.GetProperty("rules").EnumerateArray().Select(rule =>
                new ModelRule(
                    rule.TryGetProperty("spritePathContains", out var path) ? path.GetString() : null,
                    rule.TryGetProperty("entityPrototypeParents", out var parents)
                        ? parents.EnumerateArray().Select(p => p.GetString() ?? string.Empty).ToArray()
                        : Array.Empty<string>(),
                    rule.GetProperty("mesh").GetString() ?? string.Empty)).ToList();
        }
        catch (Exception e)
        {
            _rules = new List<ModelRule>();
            Logger.Error($"Failed to load Doom View model rules: {e}");
        }
    }

    public DoomModelMesh? Resolve(EntityUid uid, SpriteComponent sprite, IEntityManager entities,
        IPrototypeManager prototypes)
    {
        if (_rules.Count == 0)
            return null;

        if (!entities.TryGetComponent<MetaDataComponent>(uid, out var metadata) || metadata.EntityPrototype == null)
            return null;
        var path = sprite.BaseRSI?.Path.ToString() ?? string.Empty;
        var cacheKey = (metadata.EntityPrototype.ID, path);
        if (!_resolved.TryGetValue(cacheKey, out var selected))
        {
            selected = Match(metadata.EntityPrototype, path, prototypes);
            _resolved.Add(cacheKey, selected);
        }
        return selected == null ? null : Load(selected);
    }

    private string? Match(EntityPrototype entity, string path, IPrototypeManager prototypes)
    {
        var ancestors = new HashSet<string>();
        var pending = new Queue<string>();
        foreach (var parent in entity.Parents ?? Array.Empty<string>())
            pending.Enqueue(parent);
        while (pending.TryDequeue(out var parent))
        {
            if (!ancestors.Add(parent) || !prototypes.TryIndex<EntityPrototype>(parent, out var prototype))
                continue;
            foreach (var next in prototype.Parents ?? Array.Empty<string>())
                pending.Enqueue(next);
        }

        foreach (var rule in _rules)
        {
            if (rule.SpritePathContains != null &&
                !path.Contains(rule.SpritePathContains, StringComparison.OrdinalIgnoreCase))
                continue;
            if (rule.Parents.Length > 0 && !rule.Parents.Any(ancestors.Contains))
                continue;
            return rule.Mesh;
        }

        return null;
    }

    private DoomModelMesh? Load(string name)
    {
        if (_cache.TryGetValue(name, out var cached))
            return cached;
        try
        {
            var mesh = DoomModelMesh.Parse(_resources.ContentFileReadAllText(MeshRoot + name + ".mesh.json"));
            _cache.Add(name, mesh);
            return mesh;
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to load Doom View mesh '{name}': {e}");
            _cache.Add(name, null);
            return null;
        }
    }

    private sealed record ModelRule(string? SpritePathContains, string[] Parents, string Mesh);
}

internal sealed class DoomModelMesh
{
    private const int MaxTriangles = 4096;
    public required Vector3[] Positions { get; init; }
    public required int[] Triangles { get; init; }
    public required Color[] Materials { get; init; }
    public required int[] TriangleMaterials { get; init; }

    public static DoomModelMesh Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("format").GetString() != "verdant.mesh" ||
            root.GetProperty("version").GetInt32() != 1 ||
            root.GetProperty("unitsPerTile").GetSingle() != 1 ||
            root.GetProperty("upAxis").GetString() != "Z")
            throw new InvalidDataException("Unsupported Doom View mesh format.");

        var positions = root.GetProperty("positions").EnumerateArray().Select(point => new Vector3(
            point[0].GetSingle(), point[1].GetSingle(), point[2].GetSingle())).ToArray();
        var flatTriangles = root.GetProperty("triangles").EnumerateArray()
            .SelectMany(triangle => triangle.EnumerateArray().Select(index => index.GetInt32())).ToArray();
        var triangleMaterials = root.GetProperty("triangleMaterials").EnumerateArray()
            .Select(index => index.GetInt32()).ToArray();
        var materials = root.GetProperty("materials").EnumerateArray().Select(material =>
        {
            if (material.GetProperty("doubleSided").GetBoolean())
                throw new InvalidDataException("Double-sided model faces are not supported.");
            var rgba = material.GetProperty("baseColor").EnumerateArray().Select(x => x.GetSingle()).ToArray();
            if (rgba.Length != 4 || rgba.Any(value => !float.IsFinite(value) || value is < 0 or > 1))
                throw new InvalidDataException("Invalid model material color.");
            return new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
        }).ToArray();

        if (positions.Length == 0 || positions.Length > MaxTriangles * 3 ||
            flatTriangles.Length == 0 || flatTriangles.Length % 3 != 0 ||
            flatTriangles.Length / 3 > MaxTriangles || triangleMaterials.Length != flatTriangles.Length / 3 ||
            materials.Length == 0)
            throw new InvalidDataException("Invalid Doom View mesh dimensions.");
        if (positions.Any(point => !float.IsFinite(point.X) || !float.IsFinite(point.Y) ||
                                   !float.IsFinite(point.Z) || MathF.Abs(point.X) > 0.51f ||
                                   MathF.Abs(point.Y) > 0.51f || point.Z is < -0.01f or > 1.61f))
            throw new InvalidDataException("Doom View mesh is outside tile bounds.");

        for (var i = 0; i < flatTriangles.Length; i += 3)
        {
            var a = flatTriangles[i];
            var b = flatTriangles[i + 1];
            var c = flatTriangles[i + 2];
            if ((uint) a >= positions.Length || (uint) b >= positions.Length || (uint) c >= positions.Length ||
                (uint) triangleMaterials[i / 3] >= materials.Length ||
                Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).LengthSquared() < 1e-12f)
                throw new InvalidDataException("Doom View mesh contains an invalid face.");
        }

        return new DoomModelMesh
        {
            Positions = positions,
            Triangles = flatTriangles,
            TriangleMaterials = triangleMaterials,
            Materials = materials,
        };
    }
}
