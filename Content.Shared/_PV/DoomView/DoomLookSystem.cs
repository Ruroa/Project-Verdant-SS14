using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.Movement.Systems;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared._PV.DoomView;

public sealed partial class DoomLookSystem : EntitySystem
{
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private ISharedAdminManager _admins = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<DoomLookEvent>(OnLook);
    }

    private void OnLook(DoomLookEvent message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player ||
            !_admins.HasAdminFlag(args.SenderSession, AdminFlags.Debug) || !float.IsFinite(message.Radians))
            return;
        _mover.RotateCamera(player, new Angle(Math.Clamp(message.Radians, -0.5f, 0.5f)));
    }
}

[Serializable, NetSerializable]
public sealed class DoomLookEvent(float radians) : EntityEventArgs
{
    public float Radians = radians;
}
