using Content.Client._PV.DoomView;
using NUnit.Framework;

namespace Content.Tests.Client._PV;

[TestFixture]
public sealed class DoomModelMeshTest
{
    private const string ValidMesh = """
        {"format":"verdant.mesh","version":1,"unitsPerTile":1,"upAxis":"Z",
         "positions":[[-0.5,-0.5,0],[0.5,-0.5,0],[0,0.5,1]],"triangles":[[0,1,2]],
         "materials":[{"id":"steel","baseColor":[0.37,0.46,0.52,1],"doubleSided":false}],
         "triangleMaterials":[0]}
        """;

    [Test]
    public void ReadsMeshGeometryAndMaterial()
    {
        var mesh = DoomModelMesh.Parse(ValidMesh);

        Assert.That(mesh.Positions, Has.Length.EqualTo(3));
        Assert.That(mesh.Triangles, Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(mesh.TriangleMaterials, Is.EqualTo(new[] { 0 }));
        Assert.That(mesh.Materials, Has.Length.EqualTo(1));
    }

    [TestCase("\"version\":2")]
    [TestCase("\"triangles\":[[0,1,5]]")]
    [TestCase("\"positions\":[[-0.6,-0.5,0],[0.5,-0.5,0],[0,0.5,1]]")]
    public void RejectsUnsupportedOrUnsafeMeshes(string replacement)
    {
        var json = replacement.StartsWith("\"version\"", StringComparison.Ordinal)
            ? ValidMesh.Replace("\"version\":1", replacement, StringComparison.Ordinal)
            : ValidMesh.Replace(replacement.StartsWith("\"triangles\"", StringComparison.Ordinal)
                ? "\"triangles\":[[0,1,2]]"
                : "\"positions\":[[-0.5,-0.5,0],[0.5,-0.5,0],[0,0.5,1]]", replacement,
                StringComparison.Ordinal);

        Assert.Throws<System.IO.InvalidDataException>(() => DoomModelMesh.Parse(json));
    }
}
