using System.Numerics;
using Content.Shared.Decals;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Decals;

public sealed partial class DecalPlacementSystem
{
    [Dependency] private IEyeManager _brushEye = default!;
    [Dependency] private MapSystem _brushMap = default!;

    private bool _brushEnabled;
    private bool _brushRandomRotation;
    private float _brushSpacing = 1f;
    private bool _brushPainting;
    private EntityUid? _lastBrushGrid;
    private Vector2? _lastBrushPosition;
    private float _distanceSinceLastBrushStamp;

    public void UpdateBrushInfo(bool enabled, bool randomRotation, float spacing)
    {
        _brushEnabled = enabled;
        _brushRandomRotation = randomRotation;
        var newSpacing = MathF.Max(0.25f, spacing);
        if (MathF.Abs(newSpacing - _brushSpacing) > 0.001f)
            _distanceSinceLastBrushStamp = 0f;
        _brushSpacing = newSpacing;

        if (!enabled)
            EndBrush();
    }

    private bool BeginBrush(EntityCoordinates coordinates)
    {
        if (_placing)
            return true;

        _placing = true;
        _brushPainting = true;
        _lastBrushGrid = null;
        _lastBrushPosition = null;
        _distanceSinceLastBrushStamp = 0f;

        PaintBrushTo(coordinates);
        return true;
    }

    private void EndBrush()
    {
        _brushPainting = false;
        _placing = false;
        _lastBrushGrid = null;
        _lastBrushPosition = null;
        _distanceSinceLastBrushStamp = 0f;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_active || !_brushEnabled || !_brushPainting || _decalId == null)
            return;

        if (_inputManager.MouseScreenPosition is not { IsValid: true } screenPosition)
            return;

        var mapCoordinates = _brushEye.PixelToMap(screenPosition);
        if (!_brushMap.TryFindGridAt(mapCoordinates, out var gridUid, out _))
        {
            _lastBrushGrid = null;
            _lastBrushPosition = null;
            _distanceSinceLastBrushStamp = 0f;
            return;
        }

        PaintBrushTo(_transform.ToCoordinates(gridUid, mapCoordinates));
    }

    private void PaintBrushTo(EntityCoordinates sourceCoordinates)
    {
        var mapCoordinates = _transform.ToMapCoordinates(sourceCoordinates);
        if (!_brushMap.TryFindGridAt(mapCoordinates, out var gridUid, out _))
        {
            _lastBrushGrid = null;
            _lastBrushPosition = null;
            _distanceSinceLastBrushStamp = 0f;
            return;
        }

        var gridCoordinates = _transform.ToCoordinates(gridUid, mapCoordinates);
        if (!gridCoordinates.IsValid(EntityManager))
            return;

        var currentPosition = gridCoordinates.Position;
        if (_lastBrushGrid != gridUid || _lastBrushPosition is not { } lastPosition)
        {
            PlaceDecal(gridCoordinates, true);
            _lastBrushGrid = gridUid;
            _lastBrushPosition = currentPosition;
            _distanceSinceLastBrushStamp = 0f;
            return;
        }

        var delta = currentPosition - lastPosition;
        var distance = delta.Length();
        if (distance <= 0.001f)
            return;

        var spacing = _snap ? MathF.Max(1f, _brushSpacing) : _brushSpacing;
        var direction = delta / distance;
        var nextStampDistance = spacing - _distanceSinceLastBrushStamp;
        var traversed = 0f;

        while (distance - traversed >= nextStampDistance)
        {
            traversed += nextStampDistance;
            var stampPosition = lastPosition + direction * traversed;
            PlaceDecal(new EntityCoordinates(gridUid, stampPosition), true);
            nextStampDistance = spacing;
            _distanceSinceLastBrushStamp = 0f;
        }

        _distanceSinceLastBrushStamp += distance - traversed;
        _lastBrushPosition = currentPosition;
    }

    private void PlaceDecal(EntityCoordinates coordinates, bool fromBrush)
    {
        if (_decalId == null)
            return;

        if (_snap)
        {
            var snapped = new Vector2(
                (float) (MathF.Round(coordinates.X - 0.5f, MidpointRounding.AwayFromZero) + 0.5),
                (float) (MathF.Round(coordinates.Y - 0.5f, MidpointRounding.AwayFromZero) + 0.5)
            );
            coordinates = coordinates.WithPosition(snapped);
        }

        coordinates = coordinates.Offset(new Vector2(-0.5f, -0.5f));
        if (!coordinates.IsValid(EntityManager))
            return;

        var rotation = _decalAngle;
        if (fromBrush && _brushRandomRotation)
            rotation = Angle.FromDegrees(Random.Shared.NextDouble() * 360.0);

        var decal = new Decal(coordinates.Position, _decalId, _decalColor, rotation, _zIndex, _cleanable);
        RaiseNetworkEvent(new RequestDecalPlacementEvent(decal, GetNetCoordinates(coordinates)));
    }
}
