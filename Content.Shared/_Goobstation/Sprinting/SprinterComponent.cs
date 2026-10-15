// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.Sprinting;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SprinterComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public bool IsSprinting;

    [DataField, AutoNetworkedField]
    public bool ScaleWithStamina = true;

    [DataField, AutoNetworkedField]
    public bool CanSprint = true;

    [DataField, AutoNetworkedField]
    public float StaminaDrainRate = 3f;

    [DataField, AutoNetworkedField]
    public float StaminaDrainMultiplier = 1.4f;

    [DataField, AutoNetworkedField]
    public float SprintSpeedMultiplier = 1.45f;

    [DataField]
    public EntProtoId SprintAnimation = "GoobstationSprintAnimation";

    [ViewVariables]
    public TimeSpan LastStep = TimeSpan.Zero;

    [DataField]
    public EntProtoId StepAnimation = "GoobstationSmallSprintAnimation";

    [DataField]
    public SoundSpecifier SprintStartupSound = new SoundPathSpecifier("/Audio/_Goobstation/Effects/Sprinting/sprint_puff.ogg");

    [DataField, AutoNetworkedField]
    public TimeSpan TimeBetweenSteps = TimeSpan.FromSeconds(0.6);

    [DataField]
    public DamageSpecifier SprintDamageSpecifier = new()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            { "Blunt", 10 },
        }
    };

    [DataField]
    public TimeSpan KnockdownDurationOnInterrupt = TimeSpan.FromSeconds(2f);

    [DataField]
    public float StaminaPenaltyOnShove = 25f;
}

[Serializable, NetSerializable]
public sealed class SprintToggleEvent(bool isSprinting) : EntityEventArgs
{
    public bool IsSprinting = isSprinting;
}

[Serializable, NetSerializable]
public sealed class SprintStartEvent : EntityEventArgs;

[ByRefEvent]
public sealed class SprintAttemptEvent : CancellableEntityEventArgs;
