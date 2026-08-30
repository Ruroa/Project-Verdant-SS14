using Content.Server.Light.Components;
using Content.Server._PV.Weather.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

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
        // Dynamically spawned markers can start up before their transform has been
        // attached to the target grid. Defer the one-shot write until placement is
        // complete instead of deleting a marker that has not changed anything.
        Timer.Spawn(0, () => ApplyRoofFlag(ent));
    }

    private void ApplyRoofFlag(Entity<SetRoofComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

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
            var roofOverrides = EnsureComp<RoofOverrideComponent>(gridUid);

            if (ent.Comp.Value)
            {
                roofOverrides.ForceNoRoof.Remove(index);
                roofOverrides.ForceRoof.Add(index);
            }
            else
            {
                roofOverrides.ForceRoof.Remove(index);
                roofOverrides.ForceNoRoof.Add(index);
            }

            SetRoof((gridUid, grid, roof), index, ent.Comp.Value);
        }

        QueueDel(ent.Owner);
    }
}
