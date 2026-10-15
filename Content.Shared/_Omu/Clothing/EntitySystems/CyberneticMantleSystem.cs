// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared._Omu.Clothing.Components;
using Content.Shared._Omu.Traits;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Inventory;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Omu.Clothing.EntitySystems;

/// <summary>Turns the mantle's vision systems on only while worn by a cybernetic beast.</summary>
public sealed class CyberneticMantleSystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CyberneticMantleComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<CyberneticMantleComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<CyberneticMantleComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipAttempt(Entity<CyberneticMantleComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled || !HasComp<CyberneticBeastComponent>(args.EquipTarget))
            return;

        foreach (var slot in new[] { "mask", "eyes" })
        {
            if (!_inventory.TryGetSlotContainer(args.EquipTarget, slot, out var container, out _) || container.ContainedEntity is not { } existing)
                continue;

            args.Reason = Loc.GetString("slot-block-component-blocked", ("item", existing));
            args.Cancel();
            return;
        }
    }

    private void OnEquipped(Entity<CyberneticMantleComponent> ent, ref GotEquippedEvent args)
    {
        if (_timing.ApplyingState && _net.IsClient)
            return;

        if (HasComp<CyberneticBeastComponent>(args.EquipTarget))
        {
            if (TryComp<ItemToggleComponent>(ent, out var toggle))
                _toggle.TrySetActive(new Entity<ItemToggleComponent?>(ent.Owner, toggle), true, args.EquipTarget, true);

            if (TryComp<HumanoidAppearanceComponent>(args.EquipTarget, out var appearance))
            {
                var hsv = Color.ToHsv(appearance.EyeColor);
                hsv.Z = Math.Clamp(hsv.Z, ent.Comp.MinEyeColorLevel, 1f);
                var color = Color.FromHsv(hsv);
                if (TryComp<ClothingComponent>(ent, out var clothing))
                    _clothing.SetLayerColor(clothing, "head", "mantle-eyes", color);
                _lights.SetColor(ent, color);
            }
        }
    }

    private void OnUnequipped(Entity<CyberneticMantleComponent> ent, ref GotUnequippedEvent args)
    {
        if (_timing.ApplyingState && _net.IsClient)
            return;
        if (TryComp<ItemToggleComponent>(ent, out var toggle))
            _toggle.TrySetActive(new Entity<ItemToggleComponent?>(ent.Owner, toggle), false, args.EquipTarget, true);
    }
}
