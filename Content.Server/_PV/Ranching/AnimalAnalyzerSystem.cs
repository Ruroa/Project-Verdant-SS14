using System.Linq;
using Content.Shared._PV.Ranching;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.AnimalHusbandry;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._PV.Ranching;

public sealed class AnimalAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AnimalAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<AnimalAnalyzerComponent, AnimalAnalyzerDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<AnimalAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach || !HasComp<MobStateComponent>(target))
            return;

        if (!HasComp<HungerComponent>(target) && !HasComp<ThirstComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("animal-analyzer-not-an-animal"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.ScanDelay,
            new AnimalAnalyzerDoAfterEvent(), ent, target: target, used: ent)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnDoAfter(Entity<AnimalAnalyzerComponent> ent, ref AnimalAnalyzerDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        var food = TryComp<HungerComponent>(target, out var hunger)
            ? Percent(_hunger.GetHunger(hunger), hunger.Thresholds.Values.Max()) : -1f;
        var water = TryComp<ThirstComponent>(target, out var thirst)
            ? Percent(thirst.CurrentThirst, thirst.ThirstThresholds.Values.Max()) : -1f;
        var lifeStage = TryComp<InfantComponent>(target, out var infant)
            ? Loc.GetString("animal-analyzer-life-stage-young", ("time", Remaining(infant.InfantEndTime)))
            : Loc.GetString("animal-analyzer-life-stage-adult");

        var breeding = Loc.GetString("animal-analyzer-breeding-unavailable");
        if (TryComp<ReproductiveComponent>(target, out var reproductive))
            breeding = reproductive.Gestating && reproductive.GestationEndTime is { } end
                ? Loc.GetString("animal-analyzer-breeding-gestating", ("time", Remaining(end)))
                : Loc.GetString("animal-analyzer-breeding-ready");

        _audio.PlayPvs(ent.Comp.ScanSound, ent);
        _ui.OpenUi(ent.Owner, AnimalAnalyzerUiKey.Key, args.User);
        _ui.ServerSendUiMessage(ent.Owner, AnimalAnalyzerUiKey.Key,
            new AnimalAnalyzerScannedMessage(new AnimalAnalyzerUiState(Name(target), food, water, lifeStage, breeding)));
        args.Handled = true;
    }

    private string Remaining(TimeSpan end) => Math.Max(0, (end - _timing.CurTime).TotalSeconds).ToString("F0");
    private static float Percent(float value, float maximum) => maximum <= 0 ? -1f : Math.Clamp(value / maximum * 100f, 0f, 100f);
}
