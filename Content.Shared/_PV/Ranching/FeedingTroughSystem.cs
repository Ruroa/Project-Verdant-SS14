using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Storage;

namespace Content.Shared._PV.Ranching;

public sealed class FeedingTroughSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FeedingTroughComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FeedingTroughComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
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
