// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared.Speech;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Omu.Traits;

/// <summary>Marks a character whose vision is corrected by a cybernetic mantle.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticBeastComponent : Component
{
    [DataField]
    public ProtoId<SpeechSoundsPrototype> SpeechSoundWhileWearingMantle = "Pai";
}
