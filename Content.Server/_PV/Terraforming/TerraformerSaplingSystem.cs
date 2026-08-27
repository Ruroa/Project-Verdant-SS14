using Content.Server.Power.Components;
using Content.Shared._PV.Terraforming;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._PV.Terraforming;

public sealed class TerraformerSaplingSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const float MinimumProcessDelay = 5f;
    private const float MaximumProcessDelay = 10f;
    private const float NoValidTileRetryCooldown = 1f;

    private readonly Dictionary<EntityUid, Queue<QueuedSapling>> _saplingQueues = new();

    private sealed class QueuedSapling
    {
        public readonly string TreePrototype;
        public float ProcessDelayRemaining;
        public float RetryAccumulator;

        public QueuedSapling(string treePrototype, float processDelay)
        {
            TreePrototype = treePrototype;
            ProcessDelayRemaining = processDelay;
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        // The main TerraformerSystem already subscribes to TerraformerComponent events.
        // Subscribe through TransformComponent and filter for terraformers to avoid duplicate subscriptions.
        SubscribeLocalEvent<TransformComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TransformComponent, ComponentShutdown>(OnTransformShutdown);
    }

    private void OnTransformShutdown(EntityUid uid, TransformComponent component, ComponentShutdown args)
    {
        _saplingQueues.Remove(uid);
    }

    private void OnInteractUsing(EntityUid uid, TransformComponent xformComp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<TerraformerComponent>(uid, out _))
            return;

        if (!TryComp<TerraformerSaplingComponent>(args.Used, out var sapling))
            return;

        var treePrototype = sapling.TreePrototypes.Count > 0
            ? _random.Pick(sapling.TreePrototypes)
            : sapling.TreePrototype;

        if (string.IsNullOrWhiteSpace(treePrototype))
        {
            _popup.PopupEntity("This sapling has no tree prototype configured.", uid, args.User);
            args.Handled = true;
            return;
        }

        if (!_saplingQueues.TryGetValue(uid, out var queue))
        {
            queue = new Queue<QueuedSapling>();
            _saplingQueues[uid] = queue;
        }

        // Each sapling gets its own 5-10 second processing delay. Only the front item is processed,
        // so loading several saplings never causes them to grow all at once.
        var processDelay = _random.NextFloat(MinimumProcessDelay, MaximumProcessDelay);
        queue.Enqueue(new QueuedSapling(treePrototype, processDelay));

        // The physical sapling is now stored logically in the Terraformer's queue.
        QueueDel(args.Used);
        args.Handled = true;

        _popup.PopupEntity($"You load the sapling into the terraformer. Queue: {queue.Count}", uid, args.User);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TerraformerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var terraformer, out var xform))
        {
            if (!_saplingQueues.TryGetValue(uid, out var queue) || queue.Count == 0)
                continue;

            // Saplings only process while the Terraformer is active and powered.
            // Pausing power also pauses the current sapling's processing delay.
            if (!terraformer.Active || !IsPowered(uid))
                continue;

            var current = queue.Peek();

            if (current.ProcessDelayRemaining > 0f)
            {
                current.ProcessDelayRemaining -= frameTime;

                if (current.ProcessDelayRemaining > 0f)
                    continue;

                current.ProcessDelayRemaining = 0f;
                current.RetryAccumulator = NoValidTileRetryCooldown;
            }

            current.RetryAccumulator += frameTime;

            if (current.RetryAccumulator < NoValidTileRetryCooldown)
                continue;

            current.RetryAccumulator = 0f;

            // Do not dequeue the sapling when there is no valid grass tile.
            // It remains at the front of the queue and retries until a spot opens up.
            if (!TrySpawnSaplingTree(terraformer, xform, current.TreePrototype))
                continue;

            queue.Dequeue();

            if (queue.Count == 0)
                _saplingQueues.Remove(uid);
        }
    }

    private bool TrySpawnSaplingTree(
        TerraformerComponent terraformer,
        TransformComponent xform,
        string treePrototype)
    {
        if (xform.GridUid == null)
            return false;

        var gridUid = xform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var validTiles = new List<TileRef>();

        foreach (var tile in GetTilesInRadius(gridUid, grid, xform.Coordinates, terraformer.Radius))
        {
            var tileDefinition = _tileDefinition[tile.Tile.TypeId];

            if (!terraformer.TreeSpawnTiles.Contains(tileDefinition.ID))
                continue;

            if (!IsTileFreeForTree(gridUid, grid, tile.GridIndices))
                continue;

            validTiles.Add(tile);
        }

        if (validTiles.Count == 0)
            return false;

        var selectedTile = _random.Pick(validTiles);
        var coords = _map.GridTileToLocal(gridUid, grid, selectedTile.GridIndices);

        Spawn(treePrototype, coords);
        return true;
    }

    private bool IsTileFreeForTree(EntityUid gridUid, MapGridComponent grid, Vector2i tileIndices)
    {
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tileIndices))
        {
            if (Deleted(anchored))
                continue;

            // Sub-floor cables, pipes, atmosphere-fix markers and similar anchored utility entities
            // should not make an otherwise valid grass tile unusable. Only real colliding occupants
            // block tree placement. Existing trees and normal structures are collidable, so this also
            // prevents trees from spawning on top of each other or inside walls/machines.
            if (!TryComp<PhysicsComponent>(anchored, out var physics))
                continue;

            if (!physics.CanCollide)
                continue;

            return false;
        }

        return true;
    }

    private bool IsPowered(EntityUid uid)
    {
        if (!TryComp<ApcPowerReceiverComponent>(uid, out var power))
            return true;

        return power.Powered;
    }

    private IEnumerable<TileRef> GetTilesInRadius(
        EntityUid gridUid,
        MapGridComponent grid,
        EntityCoordinates center,
        float radius)
    {
        var centerTile = _map.GetTileRef(gridUid, grid, center);
        var radiusInt = (int) MathF.Ceiling(radius);

        for (var x = -radiusInt; x <= radiusInt; x++)
        {
            for (var y = -radiusInt; y <= radiusInt; y++)
            {
                if (x * x + y * y > radius * radius)
                    continue;

                var tileIndices = centerTile.GridIndices + new Vector2i(x, y);

                if (!_map.TryGetTileRef(gridUid, grid, tileIndices, out var tile))
                    continue;

                yield return tile;
            }
        }
    }
}
