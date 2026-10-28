using System.Linq;
using System.Numerics;
using Content.Client._PV.DoomView;
using NUnit.Framework;

namespace Content.Tests.Client._PV;

[TestFixture]
public sealed class DoomUvTest
{
    [Test]
    public void UnrotatedMatchesTextureCorners()
    {
        Assert.That(DoomUv.Tile(0), Is.EqualTo(new[] { Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY }));
    }

    [Test]
    public void QuarterTurnMatchesGridRenderer()
    {
        Assert.That(DoomUv.Tile(1), Is.EqualTo(new[] { Vector2.UnitX, Vector2.One, Vector2.UnitY, Vector2.Zero }));
    }

    [Test]
    public void MirrorReversesHorizontalAxis()
    {
        Assert.That(DoomUv.Tile(4), Is.EqualTo(new[] { Vector2.UnitX, Vector2.Zero, Vector2.UnitY, Vector2.One }));
    }

    [Test]
    public void AllOrientationsPreserveFourDistinctCorners()
    {
        for (var orientation = 0; orientation < 8; orientation++)
        {
            var uv = DoomUv.Tile(orientation);
            Assert.That(uv.Distinct().Count(), Is.EqualTo(4));
            Assert.That(uv.All(p => (p.X == 0 || p.X == 1) && (p.Y == 0 || p.Y == 1)), Is.True);
        }
    }
}
