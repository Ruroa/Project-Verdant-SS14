// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared._Omu.Clothing.Components;
using Content.Shared.Speech;
using Content.Shared.Inventory;

namespace Content.Shared._Omu.Traits;

/// <summary>Selects the Pai voice set only while a cybernetic beast is wearing a mantle.</summary>
public sealed class CyberneticBeastSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CyberneticBeastComponent, GetCyberneticBeastSpeechSoundEvent>(OnGetSpeechSound);
    }

    private void OnGetSpeechSound(Entity<CyberneticBeastComponent> ent, ref GetCyberneticBeastSpeechSoundEvent args)
    {
        if (!TryComp<InventoryComponent>(ent, out var inventory))
            return;

        if (_inventory.TryGetInventoryEntity<CyberneticMantleComponent>(new Entity<InventoryComponent?>(ent.Owner, inventory), out _))
            args.SpeechSound = ent.Comp.SpeechSoundWhileWearingMantle;
    }
}
