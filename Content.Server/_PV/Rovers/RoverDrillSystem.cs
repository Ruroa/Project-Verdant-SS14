using Content.Shared._PV.Rovers;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using Content.Shared.Weapons.Melee.Events;
using Content.Server.Shuttles.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._PV.Rovers;

public sealed class RoverDrillSystem : EntitySystem
{
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly TimeSpan MinimumAttackInterval = TimeSpan.FromSeconds(0.05);
    private static readonly SoundSpecifier DrillHitSound = new SoundPathSpecifier("/Audio/Items/drill_hit.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoverDrillComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<RoverDrillComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RoverDrillComponent>();
        while (query.MoveNext(out var uid, out var drill))
        {
            if (!drill.Enabled || drill.NextAttack > _timing.CurTime)
                continue;

            drill.NextAttack = _timing.CurTime + GetAttackInterval(drill);
            TryDrillHit(uid, drill);
        }
    }

    private void OnInit(Entity<RoverDrillComponent> ent, ref ComponentInit args)
    {
        _deviceLink.EnsureSinkPorts(ent, ent.Comp.TogglePort, ent.Comp.OnPort, ent.Comp.OffPort);
        if (ent.Comp.Enabled)
            ent.Comp.NextAttack = _timing.CurTime + GetAttackInterval(ent.Comp);
        UpdateAppearance(ent);
    }

    private void OnSignalReceived(Entity<RoverDrillComponent> ent, ref SignalReceivedEvent args)
    {
        var state = SignalState.Momentary;
        args.Data?.TryGetValue(DeviceNetworkConstants.LogicState, out state);
        if (state is not (SignalState.High or SignalState.Momentary))
            return;

        if (args.Port == ent.Comp.TogglePort)
            SetEnabled(ent, !ent.Comp.Enabled);
        else if (args.Port == ent.Comp.OnPort)
            SetEnabled(ent, true);
        else if (args.Port == ent.Comp.OffPort)
            SetEnabled(ent, false);
        else
            return;
    }

    private void SetEnabled(Entity<RoverDrillComponent> ent, bool enabled)
    {
        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        ent.Comp.NextAttack = enabled ? _timing.CurTime + GetAttackInterval(ent.Comp) : TimeSpan.Zero;
        Dirty(ent);
        UpdateAppearance(ent);
    }

    private void TryDrillHit(EntityUid uid, RoverDrillComponent drill)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        var mapCoordinates = _transform.GetMapCoordinates(uid, xform);
        var forward = _transform.GetWorldRotation(uid).GetCardinalDir().ToIntVec();
        var targetPosition = mapCoordinates.Position + new Vector2(forward.X, forward.Y);

        EntityUid target = EntityUid.Invalid;
        var mapUid = _map.GetMapOrInvalid(mapCoordinates.MapId);

        // The planet root is the first target: the rover sits on another grid, but drills through
        // the world position in front of it to damage the actual layer-0 rock entity.
        if (HasComp<PlanetarySurfaceComponent>(mapUid) &&
            TryComp(mapUid, out MapGridComponent? surfaceGrid) &&
            TryGetDamageableAt(mapUid, surfaceGrid, targetPosition, uid, out target))
        {
            DrillTarget(uid, target, drill);
            return;
        }

        // On ordinary grids, keep the same one-tile-ahead behavior against the drill's own grid.
        if (xform.GridUid is not { } gridUid ||
            !TryComp(gridUid, out MapGridComponent? grid) ||
            !TryGetDamageableAt(gridUid, grid, targetPosition, uid, out target))
        {
            return;
        }

        DrillTarget(uid, target, drill);
    }

    private bool TryGetDamageableAt(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2 worldPosition,
        EntityUid drillUid,
        out EntityUid target)
    {
        var tile = _map.WorldToTile(gridUid, grid, worldPosition);
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (anchored == drillUid || !HasComp<DamageableComponent>(anchored))
                continue;

            target = anchored;
            return true;
        }

        target = EntityUid.Invalid;
        return false;
    }

    private void DrillTarget(EntityUid drillUid, EntityUid target, RoverDrillComponent drill)
    {
        if (!TryComp(target, out TransformComponent? targetXform))
            return;

        _audio.PlayPvs(DrillHitSound, drillUid);

        // This mirrors a handheld mining drill: Pickaxe-tagged tools trigger Gatherable/OreVein
        // behavior, then normal structural damage is applied for other destructible targets.
        var attacked = new AttackedEvent(drillUid, drillUid, targetXform.Coordinates);
        RaiseLocalEvent(target, attacked);
        _damageable.TryChangeDamage(target, drill.Damage, origin: drillUid);
    }

    private static TimeSpan GetAttackInterval(RoverDrillComponent drill)
    {
        return TimeSpan.FromSeconds(Math.Max(drill.AttackInterval, (float) MinimumAttackInterval.TotalSeconds));
    }

    private void UpdateAppearance(Entity<RoverDrillComponent> ent)
    {
        _appearance.SetData(ent, RoverDrillVisuals.Enabled, ent.Comp.Enabled);
    }
}
