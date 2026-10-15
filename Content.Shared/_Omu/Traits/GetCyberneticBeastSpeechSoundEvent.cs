// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Shared._Omu.Traits;

/// <summary>Allows cybernetic beasts to select a different speech sound while wearing their mantle.</summary>
public sealed class GetCyberneticBeastSpeechSoundEvent(ProtoId<SpeechSoundsPrototype>? speechSound)
    : EntityEventArgs
{
    public ProtoId<SpeechSoundsPrototype>? SpeechSound = speechSound;
}
