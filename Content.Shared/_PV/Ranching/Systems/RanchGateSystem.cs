using Content.Shared._PV.Ranching.Components;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._PV.Ranching.Systems;

/// <summary>
/// Allows ranch gates to remain open for people while containing unpulled livestock.
/// </summary>
public sealed class RanchGateSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RanchGateComponent, CollisionChangeEvent>(OnCollisionChange);
        SubscribeLocalEvent<RanchGateComponent, PreventCollideEvent>(OnPreventCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RanchGateComponent, DoorComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var gate, out var door, out var physics))
        {
            var passable = door.State == DoorState.Open
                || door.State == DoorState.Opening && door.Partial
                || door.State == DoorState.Closing && !door.Partial;

            gate.AnimalBarrierActive = passable;

            if (passable && !physics.CanCollide)
                _physics.SetCanCollide(uid, true, body: physics);
        }
    }

    private void OnCollisionChange(Entity<RanchGateComponent> entity, ref CollisionChangeEvent args)
    {
        if (args.CanCollide || !TryComp(entity, out DoorComponent? door))
            return;

        // The regular door system has reached the passable portion of opening and disabled collision.
        // Keep the body enabled so PreventCollideEvent can selectively block livestock.
        if (door.State != DoorState.Opening && door.State != DoorState.Open)
            return;

        entity.Comp.AnimalBarrierActive = true;

        if (TryComp(entity, out PhysicsComponent? physics))
            _physics.SetCanCollide(entity, true, body: physics);
    }

    private void OnPreventCollide(Entity<RanchGateComponent> entity, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !entity.Comp.AnimalBarrierActive)
            return;

        var isAnimal = _whitelist.IsValid(entity.Comp.AnimalWhitelist, args.OtherEntity);
        var isBeingPulled = IsActivelyPulled(args.OtherEntity);

        // Cancelling means there is no collision. Everyone can cross an open gate except
        // configured livestock that is not actively being dragged.
        if (!isAnimal || isBeingPulled)
            args.Cancelled = true;
    }

    private bool IsActivelyPulled(EntityUid animal)
    {
        // Pull state is networked on both participants. Accept either side so prediction does not
        // temporarily treat a dragged animal as free-roaming while one component catches up.
        if (TryComp(animal, out PullableComponent? pullable) && pullable.Puller != null)
            return true;

        var query = EntityQueryEnumerator<PullerComponent>();
        while (query.MoveNext(out _, out var puller))
        {
            if (puller.Pulling == animal)
                return true;
        }

        return false;
    }
}
