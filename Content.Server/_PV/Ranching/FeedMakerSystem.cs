using Content.Server.Botany.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._PV.Ranching;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._PV.Ranching;

public sealed class FeedMakerSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize() => SubscribeLocalEvent<FeedMakerComponent, InteractUsingEvent>(OnInteractUsing);

    private void OnInteractUsing(Entity<FeedMakerComponent> ent, ref InteractUsingEvent args)
    {
        if (!this.IsPowered(ent.Owner, EntityManager) || !TryComp<ProduceComponent>(args.Used, out var produce))
            return;

        var color = Color.White;
        if (_solutions.TryGetSolution(args.Used, produce.SolutionName, out _, out var solution))
            color = solution.GetColor(_prototypes);

        for (var i = 0; i < ent.Comp.FeedAmount; i++)
        {
            var feed = Spawn(ent.Comp.FeedPrototype, Transform(ent).Coordinates);
            if (TryComp<AnimalFeedComponent>(feed, out var animalFeed))
            {
                animalFeed.Color = color;
                Dirty(feed, animalFeed);
            }
        }

        _popup.PopupClient(Loc.GetString("feed-maker-success", ("produce", args.Used)), ent, args.User, PopupType.Medium);
        QueueDel(args.Used);
        args.Handled = true;
    }
}
