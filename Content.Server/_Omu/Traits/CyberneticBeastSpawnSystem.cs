// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared._Omu.Traits;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Traits;

/// <summary>Gives cybernetic beasts one internals-capable mantle variant appropriate to their job.</summary>
public sealed class CyberneticBeastSpawnSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private static readonly Dictionary<string, EntProtoId> JobMantles = new()
    {
        ["AtmosphericTechnician"] = "ClothingCyberneticBeastMantleWorkAtmospherics",
        ["StationEngineer"] = "ClothingCyberneticBeastMantleWorkEngineering",
        ["TechnicalAssistant"] = "ClothingCyberneticBeastMantleWorkEngineering",
        ["SalvageSpecialist"] = "ClothingCyberneticBeastMantleWorkSalvage",
        ["SalvageLead"] = "ClothingCyberneticBeastMantleWorkSalvage",
        ["MiningSpecialist"] = "ClothingCyberneticBeastMantleWorkSalvage",
        ["Detective"] = "ClothingCyberneticBeastSpaceMantleSecurity",
        ["SecurityCadet"] = "ClothingCyberneticBeastSpaceMantleSecurity",
        ["SecurityOfficer"] = "ClothingCyberneticBeastSpaceMantleSecurity",
        ["Warden"] = "ClothingCyberneticBeastSpaceMantleSecurity",
        ["Chemist"] = "ClothingCyberneticBeastMantleWorkResearch",
        ["MedicalDoctor"] = "ClothingCyberneticBeastSpaceMantleCorpsman",
        ["Surgeon"] = "ClothingCyberneticBeastSpaceMantleCorpsman",
        ["MedicalIntern"] = "ClothingCyberneticBeastSpaceMantleCorpsman",
        ["Paramedic"] = "ClothingCyberneticBeastSpaceMantleCorpsman",
        ["Brigmedic"] = "ClothingCyberneticBeastSpaceMantleCorpsman",
        ["Captain"] = "ClothingCyberneticBeastMantleWorkResearch",
        ["ChiefEngineer"] = "ClothingCyberneticBeastMantleWorkChiefEngineering",
        ["ChiefMedicalOfficer"] = "ClothingCyberneticBeastSpaceMantleCorpsman",
        ["HeadOfPersonnel"] = "ClothingCyberneticBeastMantleWorkResearch",
        ["HeadOfSecurity"] = "ClothingCyberneticBeastSpaceMantleSecurity",
        ["Quartermaster"] = "ClothingCyberneticBeastMantleWorkSalvage",
        ["ResearchDirector"] = "ClothingCyberneticBeastMantleWorkResearch",
        ["AssistantManager"] = "ClothingCyberneticBeastMantleWorkResearch",
        ["BlueShield"] = "ClothingCyberneticBeastMantleBlueshieldOfficer",
        ["NTNCBlueShield"] = "ClothingCyberneticBeastMantleBlueshieldOfficer",
        ["NanoTrasenRepresentative"] = "ClothingCyberneticBeastMantleWorkResearch",
        ["Bartender"] = "ClothingCyberneticBeastMantleWork",
        ["Roboticist"] = "ClothingCyberneticBeastMantleWorkResearch",
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete, after: new[] { typeof(Content.Server._Starlight.Traits.TraitSystem) });
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!HasComp<CyberneticBeastComponent>(args.Mob))
            return;

        var prototype = args.JobId != null && JobMantles.TryGetValue(args.JobId, out var jobMantle)
            ? jobMantle
            : new EntProtoId("ClothingCyberneticBeastMantleWork");
        var coords = Transform(args.Mob).Coordinates;
        var mantle = Spawn(prototype, coords);
        _hands.PickupOrDrop(args.Mob, mantle);
    }
}
