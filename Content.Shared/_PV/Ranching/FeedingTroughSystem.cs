using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Shared._PV.Ranching;

public sealed class FeedingTroughSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FeedingTroughComponent, MapInitEvent>(OnMapInit,
            after: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<FeedingTroughComponent, EntInsertedIntoContainerMessage>(OnContentsChanged,
            after: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<FeedingTroughComponent, EntRemovedFromContainerMessage>(OnContentsChanged,
            after: [typeof(SharedStorageSystem)]);
    }

    private void OnMapInit(Entity<FeedingTroughComponent> ent, ref MapInitEvent args)
    {
        UpdateVisuals(ent.Owner);
    }

    private void OnContentsChanged(Entity<FeedingTroughComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == StorageComponent.ContainerId)
            UpdateVisuals(ent.Owner);
    }

    private void OnContentsChanged(Entity<FeedingTroughComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == StorageComponent.ContainerId)
            UpdateVisuals(ent.Owner);
    }

    private void UpdateVisuals(EntityUid uid)
    {
        if (!TryComp<StorageComponent>(uid, out var storage))
            return;

        var count = storage.Container.ContainedEntities.Count;
        var level = count switch
        {
            0 => 0,
            >= 6 => 2,
            _ => 1,
        };

        _appearance.SetData(uid, StorageFillVisuals.FillLevel, level);
    }
}
