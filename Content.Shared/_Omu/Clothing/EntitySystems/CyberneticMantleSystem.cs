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
using Content.Shared._Starlight.Overlay.Components;
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
        SubscribeLocalEvent<CyberneticMantleComponent, ItemToggledEvent>(OnToggled);
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
            if (TryComp<ItemToggleComponent>(ent, out var toggle) && toggle.Activated)
                EnableNightVision(args.EquipTarget);

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

        DisableNightVision(args.EquipTarget);
    }

    private void OnToggled(Entity<CyberneticMantleComponent> ent, ref ItemToggledEvent args)
    {
        if (_timing.ApplyingState && _net.IsClient)
            return;

        if (!args.Activated)
        {
            var parent = Transform(ent).ParentUid;
            if (parent.IsValid())
                DisableNightVision(parent);
            return;
        }

        if (TryGetWornBeast(ent.Owner, out var wearer))
            EnableNightVision(wearer);
    }

    private bool TryGetWornBeast(EntityUid mantle, out EntityUid wearer)
    {
        wearer = default;
        var parent = Transform(mantle).ParentUid;
        if (!parent.IsValid() || !HasComp<CyberneticBeastComponent>(parent))
            return false;

        if (!_inventory.TryGetSlotContainer(parent, "head", out var container, out _) || container.ContainedEntity != mantle)
            return false;

        wearer = parent;
        return true;
    }

    private void EnableNightVision(EntityUid wearer)
    {
        if (HasComp<NightVisionComponent>(wearer))
            return;

        AddComp<NightVisionComponent>(wearer);
        AddComp<CyberneticMantleNightVisionSourceComponent>(wearer);
    }

    private void DisableNightVision(EntityUid wearer)
    {
        if (!HasComp<CyberneticMantleNightVisionSourceComponent>(wearer))
            return;

        RemComp<NightVisionComponent>(wearer);
        RemComp<CyberneticMantleNightVisionSourceComponent>(wearer);
    }
}
