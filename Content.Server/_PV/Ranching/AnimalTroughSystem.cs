using Content.Shared._PV.Ranching;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server._PV.Ranching;

/// <summary>
/// Consumes animal feed into the feeding trough's internal food reservoir.
/// Animals consume both trough types through the normal Edible/HTN systems.
/// </summary>
public sealed class AnimalTroughSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FeedingTroughComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<FeedingTroughComponent> ent, ref InteractUsingEvent args)
    {
        if (!HasComp<AnimalFeedComponent>(args.Used) ||
            !_solutions.TryGetSolution(args.Used, FeedingTroughComponent.SolutionName, out _, out var feed) ||
            !_solutions.TryGetSolution(ent.Owner, FeedingTroughComponent.SolutionName, out var troughEntity, out var trough))
            return;

        if (feed.Volume <= 0 || feed.Volume > trough.AvailableVolume)
        {
            _popup.PopupClient(Loc.GetString("feeding-trough-full"), ent, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (!_solutions.TryTransferSolution(troughEntity.Value, feed, feed.Volume))
            return;

        QueueDel(args.Used);
        _popup.PopupClient(Loc.GetString("feeding-trough-filled"), ent, args.User, PopupType.Small);
        args.Handled = true;
    }
}
