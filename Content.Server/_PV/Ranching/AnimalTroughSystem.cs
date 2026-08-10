using Content.Shared._PV.Ranching;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._PV.Ranching;

/// <summary>
/// Lets nearby hungry NPC animals consume stored feed and thirsty NPC animals drink trough water.
/// </summary>
public sealed class AnimalTroughSystem : EntitySystem
{
    private const string WaterReagent = "Water";

    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        UpdateFeedingTroughs();
        UpdateWaterTroughs();
    }

    private void UpdateFeedingTroughs()
    {
        var query = EntityQueryEnumerator<FeedingTroughComponent, StorageComponent>();
        while (query.MoveNext(out var uid, out var trough, out var storage))
        {
            if (_timing.CurTime < trough.NextUse || storage.Container.ContainedEntities.Count == 0)
                continue;

            foreach (var animal in _lookup.GetEntitiesInRange<NpcFactionMemberComponent>(uid, trough.UseRange))
            {
                if (!IsLivingAnimal(animal.Owner) ||
                    !TryComp<HungerComponent>(animal.Owner, out var hunger) ||
                    hunger.CurrentThreshold > HungerThreshold.Peckish)
                    continue;

                var feed = storage.Container.ContainedEntities[0];
                if (!_ingestion.CanIngest(animal.Owner, feed) ||
                    !_containers.Remove(feed, storage.Container, destination: Transform(uid).Coordinates))
                    continue;

                if (!_ingestion.TryIngest(animal.Owner, feed))
                {
                    _containers.Insert(feed, storage.Container);
                    continue;
                }

                trough.NextUse = _timing.CurTime + trough.UseCooldown;
                break;
            }
        }
    }

    private void UpdateWaterTroughs()
    {
        var query = EntityQueryEnumerator<WaterTroughComponent>();
        while (query.MoveNext(out var uid, out var trough))
        {
            if (_timing.CurTime < trough.NextUse ||
                !_solutions.TryGetSolution(uid, WaterTroughComponent.SolutionName, out var solutionEntity, out var solution))
                continue;

            var availableWater = solution.GetTotalPrototypeQuantity(WaterReagent);
            if (availableWater <= FixedPoint2.Zero)
                continue;

            foreach (var animal in _lookup.GetEntitiesInRange<NpcFactionMemberComponent>(uid, trough.UseRange))
            {
                if (!IsLivingAnimal(animal.Owner) ||
                    !TryComp<ThirstComponent>(animal.Owner, out var thirst) ||
                    thirst.CurrentThirstThreshold > ThirstThreshold.Thirsty)
                    continue;

                var amount = FixedPoint2.Min(availableWater, FixedPoint2.New(trough.DrinkAmount));
                var removed = _solutions.RemoveReagent(solutionEntity.Value, WaterReagent, amount);
                if (removed <= FixedPoint2.Zero)
                    break;

                _thirst.ModifyThirst(animal.Owner, thirst, removed.Float() * trough.ThirstPerUnit);
                trough.NextUse = _timing.CurTime + trough.UseCooldown;
                break;
            }
        }
    }

    private bool IsLivingAnimal(EntityUid uid)
    {
        return TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Alive;
    }
}
