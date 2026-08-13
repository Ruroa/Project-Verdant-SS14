using System.Linq;
using Content.Shared._PV.Ranching;
using Content.Shared.Animals;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Server._PV.Ranching;

/// <summary>
/// Keeps a reagent cow's visual reagent synchronized with the dominant reagent in its udder.
/// </summary>
public sealed class ReagentCowSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ReagentCowComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
    }

    private void OnSolutionChanged(Entity<ReagentCowComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (!TryComp<UdderComponent>(ent, out var udder) ||
            args.SolutionId != udder.SolutionName ||
            args.Solution.Contents.Count == 0)
            return;

        var dominant = args.Solution.Contents.MaxBy(entry => entry.Quantity);
        if (dominant.Reagent.Prototype == ent.Comp.Reagent.Id)
            return;

        ent.Comp.Reagent = dominant.Reagent.Prototype;
        Dirty(ent);
    }
}
