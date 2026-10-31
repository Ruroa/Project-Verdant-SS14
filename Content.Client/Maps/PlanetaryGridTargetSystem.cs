using Content.Shared.Station.Components;
using Robust.Shared.Map;

namespace Content.Client.Maps;

public sealed class PlanetaryGridTargetSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public bool IgnoreMapRootGrid { get; set; }

    public bool TryGetStationGridCoordinates(EntityCoordinates coordinates, out EntityCoordinates stationCoordinates)
    {
        stationCoordinates = default;
        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        var mapId = _transform.GetMapId(coordinates);
        var mapUid = _map.GetMapOrInvalid(mapId);
        EntityCoordinates? fallback = null;

        foreach (var grid in _map.GetAllGrids(mapId))
        {
            if (grid.Owner == mapUid)
                continue;

            var localPosition = _map.WorldToLocal(grid.Owner, grid.Comp, mapCoordinates.Position);
            // Include empty grid cells.
            if (!grid.Comp.LocalAABB.Contains(localPosition))
                continue;

            var candidate = new EntityCoordinates(grid.Owner, localPosition);
            if (HasComp<StationMemberComponent>(grid.Owner))
            {
                stationCoordinates = candidate;
                return true;
            }

            fallback ??= candidate;
        }

        if (fallback is not { } fallbackCoordinates)
            return false;

        stationCoordinates = fallbackCoordinates;
        return true;
    }
}
