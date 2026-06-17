using Content.Server.Popups;
using Content.Shared.Changeling;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.Changeling;

public sealed class ChangelingProtogenDisguiseSystem : EntitySystem
{
    [Dependency] private readonly ChangelingSystem _changeling = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private static readonly EntProtoId ProtogenDisguisePrototype = "ChangelingClothingProtogenArmor";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChangelingComponent, ActionProtogenDisguiseEvent>(OnProtogenDisguise);
    }

    private void OnProtogenDisguise(EntityUid uid, ChangelingComponent comp, ref ActionProtogenDisguiseEvent args)
    {
        if (!_changeling.TryToggleItem(uid, ProtogenDisguisePrototype, comp, "outerClothing2"))
        {
            _popup.PopupEntity(Loc.GetString("changeling-equip-protogen-fail"), uid, uid);
            comp.Chemicals += Comp<ChangelingActionComponent>(args.Action).ChemicalCost;
            return;
        }

        _changeling.PlayMeatySound(uid, comp);
    }
}
