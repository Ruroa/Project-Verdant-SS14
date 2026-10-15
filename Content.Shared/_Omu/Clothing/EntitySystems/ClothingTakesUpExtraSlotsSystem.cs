// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared._Omu.Clothing.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Omu.Clothing.EntitySystems;

/// <summary>Reserves inventory slots for garments that cover multiple areas.</summary>
public sealed class ClothingTakesUpExtraSlotsSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItems = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ClothingTakesUpExtraSlotsComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<ClothingTakesUpExtraSlotsComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ClothingTakesUpExtraSlotsComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipAttempt(Entity<ClothingTakesUpExtraSlotsComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        foreach (var slot in ent.Comp.Slots)
        {
            if (!_inventory.TryGetSlotContainer(args.EquipTarget, slot, out var container, out _) || container.ContainedEntity is not { } item)
                continue;

            args.Reason = Loc.GetString("slot-block-component-blocked", ("item", item));
            args.Cancel();
            return;
        }
    }

    private void OnEquipped(Entity<ClothingTakesUpExtraSlotsComponent> ent, ref GotEquippedEvent args)
    {
        if (_net.IsClient && _timing.ApplyingState)
            return;

        foreach (var slot in ent.Comp.Slots)
        {
            if (_inventory.TryGetSlotEntity(args.EquipTarget, slot, out var existing))
            {
                if (TryComp<VirtualItemComponent>(existing, out var virtualItem) && virtualItem.BlockingEntity == ent.Owner)
                    continue;

                // The equip-attempt handler prevents real items from being displaced.
                // If a slot changed between the attempt and this event, leave it alone.
                continue;
            }

            _virtualItems.TrySpawnVirtualItemInInventory(ent, args.EquipTarget, slot, true);
        }
    }

    private void OnUnequipped(Entity<ClothingTakesUpExtraSlotsComponent> ent, ref GotUnequippedEvent args)
    {
        if (_net.IsClient && _timing.ApplyingState)
            return;

        foreach (var slot in ent.Comp.Slots)
            _virtualItems.DeleteInSlotMatching(args.EquipTarget, ent, slot);
    }
}
