using System.Linq;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._PV.Zpm;

[NodeGroup(NodeGroupID.Zpm)]
public sealed class ZpmNodeGroup : BaseNodeGroup
{
    private IEntityManager? _entityManager;

    public ZpmNodeReactor? Reactor { get; private set; }
    public ZpmNodeCoolingInlet? CoolingInlet { get; private set; }
    public ZpmNodeHeatOutlet? HeatOutlet { get; private set; }

    public bool IsComplete => Reactor != null && CoolingInlet != null && HeatOutlet != null;

    public override void Initialize(Node sourceNode, IEntityManager entMan)
    {
        base.Initialize(sourceNode, entMan);
        _entityManager = entMan;
    }

    public override void LoadNodes(List<Node> groupNodes)
    {
        base.LoadNodes(groupNodes);
        Reactor = groupNodes.OfType<ZpmNodeReactor>().SingleOrDefault();
        CoolingInlet = groupNodes.OfType<ZpmNodeCoolingInlet>().SingleOrDefault();
        HeatOutlet = groupNodes.OfType<ZpmNodeHeatOutlet>().SingleOrDefault();

        if (_entityManager == null)
            return;

        if (Reactor is { } reactor)
            _entityManager.EntitySysManager.GetEntitySystem<ZpmSystem>().RefreshReactor(reactor.Owner);
    }
}

/// <summary>Center node. The cooling port is one tile east; the hot outlet is one tile west.</summary>
[DataDefinition]
public sealed partial class ZpmNodeReactor : Node
{
    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        if (!xform.Comp.Anchored || grid is not { } gridEnt)
            yield break;

        var map = entMan.System<SharedMapSystem>();
        var tile = map.TileIndicesFor(gridEnt, xform.Comp.Coordinates);
        var cooling = FindPort(tile.Offset(Direction.East), true);
        var heating = FindPort(tile.Offset(Direction.West), false);
        if (cooling != null)
            yield return cooling;
        if (heating != null)
            yield return heating;

        Node? FindPort(Vector2i target, bool inlet)
        {
            foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, gridEnt, target, map))
            {
                if (inlet && node is ZpmNodeCoolingInlet || !inlet && node is ZpmNodeHeatOutlet)
                    return node;
            }

            return null;
        }
    }
}

[DataDefinition]
public sealed partial class ZpmNodeCoolingInlet : Node
{
    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        if (!xform.Comp.Anchored || grid is not { } gridEnt)
            yield break;

        var map = entMan.System<SharedMapSystem>();
        var tile = map.TileIndicesFor(gridEnt, xform.Comp.Coordinates);
        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, gridEnt, tile.Offset(Direction.West), map))
        {
            if (node is ZpmNodeReactor)
            {
                yield return node;
                yield break;
            }
        }
    }
}

[DataDefinition]
public sealed partial class ZpmNodeHeatOutlet : Node
{
    public override IEnumerable<Node> GetReachableNodes(
        Entity<TransformComponent> xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        Entity<MapGridComponent>? grid,
        IEntityManager entMan)
    {
        if (!xform.Comp.Anchored || grid is not { } gridEnt)
            yield break;

        var map = entMan.System<SharedMapSystem>();
        var tile = map.TileIndicesFor(gridEnt, xform.Comp.Coordinates);
        foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, gridEnt, tile.Offset(Direction.East), map))
        {
            if (node is ZpmNodeReactor)
            {
                yield return node;
                yield break;
            }
        }
    }
}
