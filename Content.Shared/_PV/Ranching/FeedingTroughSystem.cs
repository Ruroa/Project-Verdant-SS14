using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;

namespace Content.Shared._PV.Ranching;

public sealed class FeedingTroughSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FeedingTroughComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FeedingTroughComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<FeedingTroughComponent, IngestedEvent>(OnFeedingTroughIngested,
            after: [typeof(IngestionSystem)]);
        SubscribeLocalEvent<WaterTroughComponent, IngestedEvent>(OnWaterTroughIngested,
            after: [typeof(IngestionSystem)]);
    }

    private void OnMapInit(Entity<FeedingTroughComponent> ent, ref MapInitEvent args)
    {
        UpdateVisuals(ent.Owner);
    }

    private void OnSolutionChanged(Entity<FeedingTroughComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId == FeedingTroughComponent.SolutionName)
            UpdateVisuals(ent.Owner);
    }

    private void OnFeedingTroughIngested(Entity<FeedingTroughComponent> ent, ref IngestedEvent args)
    {
        // Large troughs must not use Edible's normal "repeat until empty" behavior.
        // Replanning lets the animal stop once its hunger or thirst is satisfied.
        args.Repeat = false;
    }

    private void OnWaterTroughIngested(Entity<WaterTroughComponent> ent, ref IngestedEvent args)
    {
        args.Repeat = false;
    }

    private void UpdateVisuals(EntityUid uid)
    {
        if (!_solutions.TryGetSolution(uid, FeedingTroughComponent.SolutionName, out _, out var solution))
            return;

        var level = solution.Volume.Float() switch
        {
            <= 0 => 0,
            >= 60 => 2,
            _ => 1,
        };

        _appearance.SetData(uid, StorageFillVisuals.FillLevel, level);
    }
}
