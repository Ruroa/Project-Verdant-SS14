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
    public List<ZpmNodeController> Controllers { get; private set; } = new();
    public List<ZpmNodeShielding> ShieldingParts { get; private set; } = new();

    public bool IsComplete => Reactor != null && CoolingInlet != null && HeatOutlet != null && ShieldingParts.Count == 8;

    public override void Initialize(Node sourceNode, IEntityManager entMan)
    {
        base.Initialize(sourceNode, entMan);
        _entityManager = entMan;
    }

    public override void LoadNodes(List<Node> groupNodes)
    {
        var previousControllers = Controllers.Select(node => node.Owner).ToArray();
        base.LoadNodes(groupNodes);
        Reactor = groupNodes.OfType<ZpmNodeReactor>().SingleOrDefault();
        CoolingInlet = groupNodes.OfType<ZpmNodeCoolingInlet>().SingleOrDefault();
        HeatOutlet = groupNodes.OfType<ZpmNodeHeatOutlet>().SingleOrDefault();
        Controllers = groupNodes.OfType<ZpmNodeController>().ToList();
        ShieldingParts = groupNodes.OfType<ZpmNodeShielding>().ToList();

        if (_entityManager == null)
            return;

        var system = _entityManager.EntitySysManager.GetEntitySystem<ZpmSystem>();
        foreach (var oldController in previousControllers)
        {
            if (Controllers.All(node => node.Owner != oldController))
                system.RefreshController(oldController, null);
        }

        if (Reactor is { } reactor)
        {
            system.RefreshReactor(reactor.Owner);
            foreach (var controller in Controllers)
                system.RefreshController(controller.Owner, reactor.Owner);
        }
        else
        {
            foreach (var controller in Controllers)
                system.RefreshController(controller.Owner, null);
        }
    }
}

[DataDefinition]
public sealed partial class ZpmNodeShielding : Node
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
        foreach (var direction in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
        {
            foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, gridEnt, tile.Offset(direction), map))
            {
                if (node is ZpmNodeReactor)
                {
                    yield return node;
                    yield break;
                }
            }
        }
    }
}

[DataDefinition]
public sealed partial class ZpmNodeController : Node
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
        foreach (var direction in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
        {
            // The ZPM core is centered in a 3x3 socket, so a controller against
            // any outside edge is two tiles from the reactor node.
            var target = tile.Offset(direction).Offset(direction);
            foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, gridEnt, target, map))
            {
                if (node is ZpmNodeReactor)
                {
                    yield return node;
                    yield break;
                }
            }
        }

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

        foreach (var direction in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
        {
            var controllerTile = tile.Offset(direction).Offset(direction);
            foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, gridEnt, controllerTile, map))
            {
                if (node is ZpmNodeController)
                    yield return node;
            }
        }

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, gridEnt, tile + new Vector2i(x, y), map))
                {
                    if (node is ZpmNodeShielding)
                        yield return node;
                }
            }
        }

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
