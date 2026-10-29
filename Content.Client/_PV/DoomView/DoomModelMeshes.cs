using System.IO;
using System.Linq;
using System.Numerics;
using System.Globalization;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

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
            var root = ParseDocument(_resources.ContentFileReadAllText(MeshRoot + "model-rules.json"));
            _rules = Sequence(root, "rules").Sequence.Select(rule =>
                new ModelRule(
                    TryGet(rule, "spritePathContains", out var path) ? Scalar(path) : null,
                    TryGet(rule, "entityPrototypeParents", out var parents)
                        ? Sequence(parents).Sequence.Select(Scalar).ToArray()
                        : Array.Empty<string>(),
                    Scalar(Get(rule, "mesh")))).ToList();
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

    internal static MappingDataNode ParseDocument(string text)
    {
        using var reader = new StringReader(text);
        var documents = DataNodeParser.ParseYamlStream(reader).ToArray();
        if (documents.Length != 1 || documents[0].Root is not MappingDataNode root)
            throw new InvalidDataException("Invalid Doom View model document.");
        return root;
    }

    internal static DataNode Get(DataNode node, string key)
    {
        if (node is MappingDataNode mapping && mapping.TryGet(key, out var value))
            return value;
        throw new InvalidDataException($"Missing Doom View model field '{key}'.");
    }

    internal static bool TryGet(DataNode node, string key, out DataNode value)
    {
        if (node is MappingDataNode mapping && mapping.TryGet(key, out value!))
            return true;
        value = null!;
        return false;
    }

    internal static SequenceDataNode Sequence(DataNode node) => node as SequenceDataNode ??
        throw new InvalidDataException("Expected a Doom View model list.");

    internal static SequenceDataNode Sequence(DataNode node, string key) => Sequence(Get(node, key));

    internal static string Scalar(DataNode node) => (node as ValueDataNode)?.Value ??
        throw new InvalidDataException("Expected a Doom View model value.");

    internal static int Integer(DataNode node) => int.TryParse(Scalar(node), NumberStyles.Integer,
        CultureInfo.InvariantCulture, out var value) ? value :
        throw new InvalidDataException("Invalid Doom View model integer.");

    internal static float Number(DataNode node) => float.TryParse(Scalar(node), NumberStyles.Float,
        CultureInfo.InvariantCulture, out var value) && float.IsFinite(value) ? value :
        throw new InvalidDataException("Invalid Doom View model number.");

    internal static bool Boolean(DataNode node) => bool.TryParse(Scalar(node), out var value) ? value :
        throw new InvalidDataException("Invalid Doom View model boolean.");

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
        var root = DoomModelMeshes.ParseDocument(json);
        if (DoomModelMeshes.Scalar(DoomModelMeshes.Get(root, "format")) != "verdant.mesh" ||
            DoomModelMeshes.Integer(DoomModelMeshes.Get(root, "version")) != 1 ||
            DoomModelMeshes.Number(DoomModelMeshes.Get(root, "unitsPerTile")) != 1 ||
            DoomModelMeshes.Scalar(DoomModelMeshes.Get(root, "upAxis")) != "Z")
            throw new InvalidDataException("Unsupported Doom View mesh format.");

        var positions = DoomModelMeshes.Sequence(root, "positions").Sequence.Select(point =>
        {
            var values = DoomModelMeshes.Sequence(point).Sequence.Select(DoomModelMeshes.Number).ToArray();
            if (values.Length != 3)
                throw new InvalidDataException("Invalid Doom View mesh position.");
            return new Vector3(values[0], values[1], values[2]);
        }).ToArray();
        var flatTriangles = DoomModelMeshes.Sequence(root, "triangles").Sequence
            .SelectMany(triangle => DoomModelMeshes.Sequence(triangle).Sequence.Select(DoomModelMeshes.Integer)).ToArray();
        var triangleMaterials = DoomModelMeshes.Sequence(root, "triangleMaterials").Sequence
            .Select(DoomModelMeshes.Integer).ToArray();
        var materials = DoomModelMeshes.Sequence(root, "materials").Sequence.Select(material =>
        {
            if (DoomModelMeshes.Boolean(DoomModelMeshes.Get(material, "doubleSided")))
                throw new InvalidDataException("Double-sided model faces are not supported.");
            var rgba = DoomModelMeshes.Sequence(material, "baseColor").Sequence.Select(DoomModelMeshes.Number).ToArray();
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
