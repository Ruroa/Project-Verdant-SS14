using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._FarHorizons.LimbDamage.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Client._FarHorizons.LimbDamage.UI;

[UsedImplicitly]
public sealed class LimbStatusUIController : UIController
{
    private const float RefreshInterval = 0.25f;

    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private SpriteSystem? _sprites;
    private float _refreshTimer;

    public override void Initialize()
    {
        base.Initialize();

        var screenLoad = UIManager.GetUIController<GameplayStateLoadController>();
        screenLoad.OnScreenLoad += Refresh;
        screenLoad.OnScreenUnload += Hide;
        _player.LocalPlayerAttached += OnPlayerAttached;
        _player.LocalPlayerDetached += OnPlayerDetached;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _refreshTimer += args.DeltaSeconds;
        if (_refreshTimer < RefreshInterval)
            return;

        _refreshTimer = 0;
        Refresh();
    }

    private void OnPlayerAttached(EntityUid _) => Refresh();

    private void OnPlayerDetached(EntityUid _) => Hide();

    private void Refresh()
    {
        var widget = UIManager.GetActiveUIWidgetOrNull<LimbStatusUI>();
        if (widget == null)
            return;

        if (_player.LocalEntity is not { } player || !_entities.HasComponent<BodyComponent>(player))
        {
            widget.Visible = false;
            return;
        }

        _sprites ??= _entities.System<SpriteSystem>();
        if (!widget.HasTargetDisplay)
            widget.InitTarget(_prototypes, _sprites, "LimbTargetHuman");

        widget.SetHealth(GetBodyDamage(player));
        widget.Visible = true;
    }

    private Dictionary<LimbTarget, float> GetBodyDamage(EntityUid player)
    {
        var damage = new Dictionary<LimbTarget, float>();
        var body = _entities.System<SharedBodySystem>();
        var root = body.GetRootPartOrNull(player);
        if (root is not { } rootPart)
            return damage;

        AddPartDamage(rootPart.Entity, rootPart.BodyPart);
        foreach (var part in body.GetAllBodyPart(rootPart.Entity, rootPart.BodyPart))
            AddPartDamage(part.Owner, part.Comp);

        return damage;

        void AddPartDamage(EntityUid partUid, BodyPartComponent part)
        {
            var target = MapTarget(part.PartType, part.Symmetry);
            if (target == null || !_entities.TryGetComponent<DamageableComponent>(partUid, out var damageable))
                return;

            AddDamage(partUid, target.Value);
        }

        void AddDamage(EntityUid entity, LimbTarget target)
        {
            if (!_entities.TryGetComponent<DamageableComponent>(entity, out var damageable))
                return;

            damage.TryGetValue(target, out var currentDamage);
            damage[target] = currentDamage + (float) damageable.Damage.GetTotal();
        }
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

    private void Hide()
    {
        var widget = UIManager.GetActiveUIWidgetOrNull<LimbStatusUI>();
        if (widget == null)
            return;

        widget.ShutdownTarget();
        widget.Visible = false;
    }
}
