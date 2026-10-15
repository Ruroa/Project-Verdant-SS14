// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Omu.Clothing.Components;

/// <summary>Identifies a cybernetic beast mantle and controls the minimum visor eye brightness.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticMantleComponent : Component, IClothingSlots
{
    [DataField]
    public SlotFlags Slots { get; set; } = SlotFlags.HEAD;
    [DataField]
    public float MinEyeColorLevel = 0.5f;
}
