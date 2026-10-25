// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared._Goobstation.Sprinting;
using System.Numerics;

namespace Content.Server._Goobstation.Sprinting;

/// <summary>
/// Sprint stamina drain
/// </summary>
public sealed class SprintStaminaSystem : EntitySystem
{
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedMoverController _moverController = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<SprinterComponent, StaminaComponent, InputMoverComponent>();
        while (query.MoveNext(out var uid, out var sprint, out _, out var mover))
        {
            if (!sprint.IsSprinting || sprint.StaminaDrainRate <= 0f || frameTime <= 0f)
                continue;

            var (walkingInput, sprintingInput) = _moverController.GetVelocityInput(mover);
            if (walkingInput == Vector2.Zero && sprintingInput == Vector2.Zero)
                continue;

            var rate = sprint.StaminaDrainRate;
            if (sprint.ScaleWithStamina
                && TryComp<StaminaModifierStatusEffectComponent>(uid, out var modifier)
                && modifier.Modifier > 1f)
            {
                rate *= modifier.Modifier * sprint.StaminaDrainMultiplier;
            }

            _stamina.TakeStaminaDamage(uid, rate * frameTime, source: uid, visual: false, ignoreResist: true);
        }
    }
}
