using Content.Server.Light.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.Server.Light.EntitySystems;

/// <inheritdoc/>
public sealed partial class RoofSystem : SharedRoofSystem
{
    [Dependency] private SharedMapSystem _maps = default!;

    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        SubscribeLocalEvent<SetRoofComponent, ComponentStartup>(OnFlagStartup);
    }

    private void OnFlagStartup(Entity<SetRoofComponent> ent, ref ComponentStartup args)
    {
        var xform = Transform(ent.Owner);

        if (_gridQuery.TryComp(xform.GridUid, out var grid))
        {
            var gridUid = xform.GridUid.Value;

            // Shuttles and other fully enclosed grids use ImplicitRoof and must not
            // be converted into a partially roofed grid by a single marker.
            if (HasComp<ImplicitRoofComponent>(gridUid))
            {
                QueueDel(ent.Owner);
                return;
            }

            var roof = EnsureComp<RoofComponent>(gridUid);
            var index = _maps.LocalToTile(gridUid, grid, xform.Coordinates);
            SetRoof((gridUid, grid, roof), index, ent.Comp.Value);
        }

        QueueDel(ent.Owner);
    }
}
