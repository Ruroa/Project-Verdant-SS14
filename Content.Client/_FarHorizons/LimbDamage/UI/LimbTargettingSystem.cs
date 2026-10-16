using Content.Shared._FarHorizons.LimbDamage.Components;
using Robust.Client.Player;

namespace Content.Client._FarHorizons.LimbDamage.UI;

public sealed class LimbTargettingSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    public event Action<LimbTarget>? TargetChanged;

    public LimbTarget CurrentTarget { get; private set; } = LimbTarget.Random;

    public override void Initialize()
    {
        SubscribeLocalEvent<LimbTargettingComponent, AfterAutoHandleStateEvent>(OnTargetState);
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
