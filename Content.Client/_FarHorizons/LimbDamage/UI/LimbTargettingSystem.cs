using Content.Shared._FarHorizons.LimbDamage.Components;
using Content.Shared.Body.Components;
using Robust.Client.Player;

namespace Content.Client._FarHorizons.LimbDamage.UI;

public sealed class LimbTargettingSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    public event Action<LimbTarget>? TargetChanged;
    public event Action<EntityUid>? LocalBodyInitialized;

    public LimbTarget CurrentTarget { get; private set; } = LimbTarget.Random;

    public override void Initialize()
    {
        SubscribeLocalEvent<LimbTargettingComponent, AfterAutoHandleStateEvent>(OnTargetState);
        // SharedBodySystem owns the BodyComponent + ComponentInit subscription.
        // Use startup to refresh after the body has been initialized without
        // registering the same component/event pair twice.
        SubscribeLocalEvent<BodyComponent, ComponentStartup>(OnBodyStartup);
    }

    private void OnBodyStartup(Entity<BodyComponent> ent, ref ComponentStartup args)
    {
        if (ent.Owner == _player.LocalEntity)
            LocalBodyInitialized?.Invoke(ent.Owner);
    }

    private void OnTargetState(Entity<LimbTargettingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Owner != _player.LocalEntity)
            return;

        CurrentTarget = ent.Comp.Target;
        TargetChanged?.Invoke(CurrentTarget);
    }

    public void SetTarget(LimbTarget target)
    {
        if (_player.LocalEntity is not { } player)
            return;

        CurrentTarget = target;
        TargetChanged?.Invoke(target);
        RaiseNetworkEvent(new ChangeLimbTargetMessage(target));
    }
}
