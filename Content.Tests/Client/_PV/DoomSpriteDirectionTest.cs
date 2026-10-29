using System.Numerics;
using Content.Client._PV.DoomView;
using NUnit.Framework;

namespace Content.Tests.Client._PV;

[TestFixture]
public sealed class DoomSpriteDirectionTest
{
    [TestCase(0, 0, Direction.North)]
    [TestCase(0, MathF.PI, Direction.South)]
    [TestCase(0, MathF.PI / 2, Direction.West)]
    [TestCase(MathF.PI / 2, 0, Direction.East)]
    public void ChoosesSideFacingCamera(float entityRotation, float cameraHeading, Direction expected)
    {
        var forward = new Angle(cameraHeading).ToWorldVec();
        Assert.That(DoomSpriteDirection.FromCamera(new Angle(entityRotation), forward), Is.EqualTo(expected));
    }
}
