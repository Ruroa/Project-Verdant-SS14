using System;
using System.Numerics;
using Content.Client._PV.DoomView;
using NUnit.Framework;

namespace Content.Tests.Client._PV;

[TestFixture]
public sealed class DoomProjectionTest
{
    [Test]
    public void CenterRayMatchesMovement()
    {
        foreach (var forward in new[] { Vector2.UnitX, Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY })
            Assert.That(Vector2.Distance(DoomProjection.Ray(forward, 0.5f), forward), Is.LessThan(0.0001f));
    }

    [Test]
    public void ProjectionRoundTrip()
    {
        foreach (var forward in new[] { Vector2.UnitX, Vector2.UnitY, Vector2.Normalize(new Vector2(1, 1)) })
        foreach (var screenX in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            var delta = DoomProjection.Ray(forward, screenX) * 5;
            Assert.That(DoomProjection.Project(delta, forward, 800, out var x, out _), Is.True);
            Assert.That(x / 800, Is.EqualTo(screenX).Within(0.0001f));
        }
    }

    [Test]
    public void RejectsBehindAndFarPlane()
    {
        Assert.That(DoomProjection.Project(-Vector2.UnitY, Vector2.UnitY, 800, out _, out _), Is.False);
        Assert.That(DoomProjection.Project(Vector2.UnitY * 13, Vector2.UnitY, 800, out _, out _), Is.False);
        Assert.That(DoomProjection.Project(Vector2.Zero, Vector2.UnitY, 800, out _, out _), Is.False);
    }

    [Test]
    public void PlanarWallHasConstantDepth()
    {
        foreach (var screenX in new[] { 0f, 0.2f, 0.5f, 0.8f, 1f })
        {
            var ray = DoomProjection.Ray(Vector2.UnitY, screenX);
            var hit = ray * (4 / ray.Y);
            DoomProjection.Project(hit, Vector2.UnitY, 800, out _, out var depth);
            Assert.That(depth, Is.EqualTo(4).Within(0.0001f));
        }
    }
}
