using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._FarHorizons.LimbDamage.Components;
using Content.Shared._Starlight.Medical.Damage;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Rejuvenate;
using Robust.Shared.Random;

namespace Content.Server._FarHorizons.LimbDamage;

public sealed partial class LimbDamageSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BodyComponent, ComponentInit>(OnBodyInit);
        SubscribeLocalEvent<BodyComponent, DamageBeforeApplyEvent>(OnDamage);
        SubscribeLocalEvent<BodyComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeNetworkEvent<ChangeLimbTargetMessage>(OnChangeTarget);
    }

    private void OnBodyInit(Entity<BodyComponent> ent, ref ComponentInit args)
    {
        EnsureComp<LimbDamageComponent>(ent);
        EnsureComp<LimbTargettingComponent>(ent);
    }

    private void OnChangeTarget(ChangeLimbTargetMessage ev, EntitySessionEventArgs args)
    {
        if (!Enum.IsDefined(typeof(LimbTarget), ev.Target) ||
            args.SenderSession.AttachedEntity is not { } user ||
            !TryComp<LimbTargettingComponent>(user, out var targetting))
            return;

        targetting.Target = ev.Target;
        Dirty(user, targetting);
    }

    private void OnRejuvenate(Entity<BodyComponent> ent, ref RejuvenateEvent args)
    {
        var root = _body.GetRootPartOrNull(ent);
        if (root is null)
            return;

        _damageable.ClearAllDamage(root.Value.Entity);
        foreach (var part in _body.GetAllBodyPart(root.Value.Entity, root.Value.BodyPart))
            _damageable.ClearAllDamage(part.Owner);
    }

    /// <summary>
    /// Records a random locational portion of each hit on an attached body part.
    /// The remainder stays on the main body entity, preserving existing mob-state
    /// thresholds while giving each part its own damage pool.
    /// </summary>
    private void OnDamage(Entity<BodyComponent> ent, ref DamageBeforeApplyEvent args)
    {
        if (args.Cancelled || args.Damage.GetTotal() == 0)
            return;

        var root = _body.GetRootPartOrNull(ent);
        if (root is null)
            return;

        var parts = new List<Entity<BodyPartComponent>> { (root.Value.Entity, root.Value.BodyPart) };
        parts.AddRange(_body.GetAllBodyPart(root.Value.Entity, root.Value.BodyPart));
        parts.RemoveAll(part => !HasComp<DamageableComponent>(part));
        if (parts.Count == 0)
            return;

        var fraction = Math.Clamp(TryComp<LimbDamageComponent>(ent, out var limbDamage)
            ? limbDamage.PartDamageFraction
            : 0.5f, 0f, 1f);
        if (fraction <= 0f)
            return;

        var part = GetSelectedPart(args.Origin, parts) ?? _random.Pick(parts);
        _damageable.ChangeDamage(
            part.Owner,
            args.Damage * fraction,
            ignoreResistances: true,
            interruptsDoAfters: false,
            origin: args.Origin);

        args.Damage *= 1f - fraction;
    }

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
