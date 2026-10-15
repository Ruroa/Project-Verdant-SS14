// SPDX-License-Identifier: AGPL-3.0-or-later
using Robust.Shared.GameStates;

namespace Content.Shared._Omu.Clothing.Components;

/// <summary>Blocks additional inventory slots while this garment is worn.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ClothingTakesUpExtraSlotsComponent : Component
{
    [DataField(required: true)]
    public string[] Slots = [];
}
