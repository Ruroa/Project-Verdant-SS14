using System.Linq;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._FarHorizons.LimbDamage.Components;
using Content.Shared._Starlight.Medical.Damage;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Rejuvenate;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server._FarHorizons.LimbDamage;

public sealed partial class LimbDamageSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly HashSet<EntityUid> _directDamageTargets = new();
    private readonly HashSet<string> _localizedDamageTypes = new(StringComparer.OrdinalIgnoreCase);

    public override void Initialize()
    {
        foreach (var groupId in new[] { "Brute", "Burn" })
        {
            if (!_prototypeManager.TryIndex<DamageGroupPrototype>(groupId, out var group))
                continue;

            foreach (var type in group.DamageTypes)
                _localizedDamageTypes.Add(type.ToString());
        }

        // Avoid duplicate subscriptions
        SubscribeLocalEvent<BodyComponent, ComponentStartup>(OnBodyStartup);
        SubscribeLocalEvent<BodyComponent, DamageBeforeApplyEvent>(OnDamage);
        SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeNetworkEvent<ChangeLimbTargetMessage>(OnChangeTarget);
    }

    private void OnBodyStartup(Entity<BodyComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<LimbDamageComponent>(ent);
        EnsureComp<LimbTargettingComponent>(ent);
    }

    private void OnChangeTarget(ChangeLimbTargetMessage ev, EntitySessionEventArgs args)
    {
        if (!Enum.IsDefined(typeof(LimbTarget), ev.Target) ||
            args.SenderSession.AttachedEntity is not { } user ||
            !HasComp<BodyComponent>(user))
            return;

        var targetting = EnsureComp<LimbTargettingComponent>(user);
        targetting.Target = ev.Target;
        Dirty(user, targetting);
    }

    private void OnRejuvenate(Entity<BodyComponent> ent, ref RejuvenateEvent args)
    {
        var root = _body.GetRootPartOrNull(ent);
        if (root is null)
            return;

        _damageable.ClearAllDamage(root.Value.Entity);
        _damageable.ClearAllDamage(ent.Owner);
        foreach (var part in _body.GetAllBodyPart(root.Value.Entity, root.Value.BodyPart))
            _damageable.ClearAllDamage(part.Owner);
    }

    /// <summary>
    /// Apply limb damage
    /// </summary>
    private void OnDamage(Entity<BodyComponent> ent, ref DamageBeforeApplyEvent args)
    {
        if (args.Cancelled || _directDamageTargets.Contains(ent.Owner) ||
            !args.Damage.DamageDict.Values.Any(value => value != FixedPoint2.Zero))
            return;

        var root = _body.GetRootPartOrNull(ent);
        if (root is null)
            return;

        var parts = new List<Entity<BodyPartComponent>> { (root.Value.Entity, root.Value.BodyPart) };
        parts.AddRange(_body.GetAllBodyPart(root.Value.Entity, root.Value.BodyPart));
        parts.RemoveAll(part => !HasComp<DamageableComponent>(part));
        if (parts.Count == 0)
            return;

        var healingTargets = GetHealingTargets(parts);
        foreach (var type in args.Damage.DamageDict.Keys.ToArray())
        {
            var amount = args.Damage.DamageDict[type];
            if (amount >= FixedPoint2.Zero)
                continue;

            if (_localizedDamageTypes.Contains(type))
                HealBodyParts(args.Origin, healingTargets, type, -amount);
        }

        var positiveTypes = args.Damage.DamageDict
            .Where(pair => pair.Value > FixedPoint2.Zero && _localizedDamageTypes.Contains(pair.Key))
            .Select(pair => pair.Key)
            .ToList();

        if (positiveTypes.Count > 0)
        {
            var fraction = Math.Clamp(TryComp<LimbDamageComponent>(ent, out var limbDamage)
                ? limbDamage.PartDamageFraction
                : 1.0f, 0f, 1f);

            if (fraction > 0f)
            {
                var part = GetSelectedPart(args.Origin, parts) ?? _random.Pick(parts);
                var localizedDamage = new DamageSpecifier();
                foreach (var type in positiveTypes)
                {
                    var localizedAmount = args.Damage.DamageDict[type] * fraction;
                    localizedDamage.DamageDict[type] = localizedAmount;
                }

                ChangeDamageDirect(part.Owner, localizedDamage, args.Origin);
            }
        }

        if (!args.Damage.DamageDict.Values.Any(value => value != FixedPoint2.Zero))
            args.Cancelled = true;
    }

    private Dictionary<LimbTarget, List<EntityUid>> GetHealingTargets(List<Entity<BodyPartComponent>> parts)
    {
        var targets = new Dictionary<LimbTarget, List<EntityUid>>();

        foreach (var part in parts)
        {
            var target = MapTarget(part.Comp.PartType, part.Comp.Symmetry);
            if (target is { } limb)
                Add(limb, part.Owner);
        }

        return targets;

        void Add(LimbTarget target, EntityUid entity)
        {
            if (!HasComp<DamageableComponent>(entity))
                return;

            if (!targets.TryGetValue(target, out var entities))
                targets[target] = entities = new List<EntityUid>();

            if (!entities.Contains(entity))
                entities.Add(entity);
        }
    }

    private void HealBodyParts(
        EntityUid? origin,
        Dictionary<LimbTarget, List<EntityUid>> targets,
        string damageType,
        FixedPoint2 amount)
    {
        var selected = GetSelectedTarget(origin);
        var order = targets.Keys.ToList();

        if (selected is { } selectedTarget && targets.ContainsKey(selectedTarget))
        {
            order.Remove(selectedTarget);
            order.Insert(0, selectedTarget);

            var remaining = order.Skip(1).ToList();
            var shuffled = new List<LimbTarget>();
            while (remaining.Count > 0)
            {
                var next = _random.Pick(remaining);
                remaining.Remove(next);
                shuffled.Add(next);
            }

            order = new List<LimbTarget> { selectedTarget };
            order.AddRange(shuffled);
        }
        else if (origin is null)
        {
            order = order
                .OrderByDescending(target => GetTargetDamage(targets[target], damageType))
                .ToList();
        }
        else
        {
            var shuffled = new List<LimbTarget>();
            while (order.Count > 0)
            {
                var next = _random.Pick(order);
                order.Remove(next);
                shuffled.Add(next);
            }

            order = shuffled;
        }

        var remainingHealing = amount;
        foreach (var target in order)
        {
            if (remainingHealing <= FixedPoint2.Zero)
                break;

            var entities = targets[target]
                .OrderByDescending(entity => GetDamage(entity, damageType))
                .ToList();

            foreach (var entity in entities)
            {
                if (remainingHealing <= FixedPoint2.Zero)
                    break;

                var available = GetDamage(entity, damageType);
                if (available <= FixedPoint2.Zero)
                    continue;

                var healing = available < remainingHealing ? available : remainingHealing;
                var damage = new DamageSpecifier();
                damage.DamageDict[damageType] = -healing;
                var applied = ChangeDamageDirect(entity, damage, origin);
                if (applied.DamageDict.TryGetValue(damageType, out var changed) && changed < FixedPoint2.Zero)
                    remainingHealing = FixedPoint2.Max(FixedPoint2.Zero, remainingHealing + changed);
            }
        }
    }

    private FixedPoint2 GetTargetDamage(List<EntityUid> entities, string damageType)
    {
        var total = FixedPoint2.Zero;
        foreach (var entity in entities)
            total += GetDamage(entity, damageType);

        return total;
    }

    private FixedPoint2 GetDamage(EntityUid entity, string damageType)
    {
        return TryComp<DamageableComponent>(entity, out var damageable) &&
               damageable.Damage.DamageDict.TryGetValue(damageType, out var damage)
            ? damage
            : FixedPoint2.Zero;
    }

    private DamageSpecifier ChangeDamageDirect(EntityUid target, DamageSpecifier damage, EntityUid? origin)
    {
        _directDamageTargets.Add(target);
        try
        {
            return _damageable.ChangeDamage(
                target,
                damage,
                ignoreResistances: true,
                interruptsDoAfters: false,
                origin: origin);
        }
        finally
        {
            _directDamageTargets.Remove(target);
        }
    }

    private LimbTarget? GetSelectedTarget(EntityUid? origin)
    {
        if (origin is not { } user ||
            !TryComp<LimbTargettingComponent>(user, out var targetting) ||
            targetting.Target == LimbTarget.Random)
            return null;

        return targetting.Target;
    }

    private static LimbTarget? MapTarget(BodyPartType type, BodyPartSymmetry symmetry) => (type, symmetry) switch
    {
        (BodyPartType.Torso, _) => LimbTarget.Torso,
        (BodyPartType.Head, _) => LimbTarget.Head,
        (BodyPartType.Arm, BodyPartSymmetry.Left) => LimbTarget.LeftArm,
        (BodyPartType.Arm, BodyPartSymmetry.Right) => LimbTarget.RightArm,
        (BodyPartType.Hand, BodyPartSymmetry.Left) => LimbTarget.LeftHand,
        (BodyPartType.Hand, BodyPartSymmetry.Right) => LimbTarget.RightHand,
        (BodyPartType.Leg, BodyPartSymmetry.Left) => LimbTarget.LeftLeg,
        (BodyPartType.Leg, BodyPartSymmetry.Right) => LimbTarget.RightLeg,
        (BodyPartType.Foot, BodyPartSymmetry.Left) => LimbTarget.LeftFoot,
        (BodyPartType.Foot, BodyPartSymmetry.Right) => LimbTarget.RightFoot,
        _ => null,
    };

    private Entity<BodyPartComponent>? GetSelectedPart(
        EntityUid? origin,
        List<Entity<BodyPartComponent>> parts)
    {
        if (origin is not { } attacker ||
            !TryComp<LimbTargettingComponent>(attacker, out var targetting) ||
            targetting.Target == LimbTarget.Random)
            return null;

        var (partType, symmetry) = targetting.Target switch
        {
            LimbTarget.Head => (BodyPartType.Head, BodyPartSymmetry.None),
            LimbTarget.LeftArm => (BodyPartType.Arm, BodyPartSymmetry.Left),
            LimbTarget.RightArm => (BodyPartType.Arm, BodyPartSymmetry.Right),
            LimbTarget.LeftHand => (BodyPartType.Hand, BodyPartSymmetry.Left),
            LimbTarget.RightHand => (BodyPartType.Hand, BodyPartSymmetry.Right),
            LimbTarget.LeftLeg => (BodyPartType.Leg, BodyPartSymmetry.Left),
            LimbTarget.RightLeg => (BodyPartType.Leg, BodyPartSymmetry.Right),
            LimbTarget.LeftFoot => (BodyPartType.Foot, BodyPartSymmetry.Left),
            LimbTarget.RightFoot => (BodyPartType.Foot, BodyPartSymmetry.Right),
            LimbTarget.Torso => (BodyPartType.Torso, BodyPartSymmetry.None),
            _ => (BodyPartType.Other, BodyPartSymmetry.None),
        };

        foreach (var part in parts)
        {
            if (part.Comp.PartType == partType && part.Comp.Symmetry == symmetry)
                return part;
        }

        return null;
    }
}
