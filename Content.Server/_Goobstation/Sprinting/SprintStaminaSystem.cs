// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared._Goobstation.Sprinting;

namespace Content.Server._Goobstation.Sprinting;

/// <summary>
/// Applies sprint stamina costs on the authoritative server. PV has no keyed
/// continuous-drain API, so the OMU drain is translated into stamina damage per tick.
/// </summary>
public sealed class SprintStaminaSystem : EntitySystem
{
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<SprinterComponent, StaminaComponent>();
        while (query.MoveNext(out var uid, out var sprint, out _))
        {
            if (!sprint.IsSprinting || sprint.StaminaDrainRate <= 0f || frameTime <= 0f)
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
