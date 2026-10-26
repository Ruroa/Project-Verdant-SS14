using Content.Server.Damage.Systems;
using Content.Shared._PV.Weapons.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._PV.Weapons;

public sealed class StaminaDamageOnShootSystem : EntitySystem
{
    [Dependency] private readonly StaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StaminaDamageOnShootComponent, AmmoShotEvent>(OnShoot);
    }

    private void OnShoot(Entity<StaminaDamageOnShootComponent> ent, ref AmmoShotEvent args)
    {
        if (args.Shooter is not { } shooter)
            return;

        _stamina.TakeStaminaDamage(shooter, ent.Comp.Damage, source: ent.Owner, with: ent.Owner, visual: false);
    }
}
