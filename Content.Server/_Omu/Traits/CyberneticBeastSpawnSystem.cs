// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared._Omu.Traits;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Traits;

/// <summary>Gives cybernetic beasts the mantle variant appropriate to their job.</summary>
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
        ["Detective"] = "ClothingCyberneticBeastMantleSecurity",
        ["SecurityCadet"] = "ClothingCyberneticBeastMantleSecurity",
        ["SecurityOfficer"] = "ClothingCyberneticBeastMantleSecurity",
        ["Warden"] = "ClothingCyberneticBeastMantleSecurity",
        ["Chemist"] = "ClothingCyberneticBeastMantleMedicalChemist",
        ["MedicalDoctor"] = "ClothingCyberneticBeastMantleMedical",
        ["Surgeon"] = "ClothingCyberneticBeastMantleMedical",
        ["MedicalIntern"] = "ClothingCyberneticBeastMantleMedical",
        ["Paramedic"] = "ClothingCyberneticBeastMantleMedicalParamedic",
        ["Brigmedic"] = "ClothingCyberneticBeastMantleCorpsman",
        ["Captain"] = "ClothingCyberneticBeastMantleCommand",
        ["ChiefEngineer"] = "ClothingCyberneticBeastMantleWorkChiefEngineering",
        ["ChiefMedicalOfficer"] = "ClothingCyberneticBeastMantleMedicalCMO",
        ["HeadOfPersonnel"] = "ClothingCyberneticBeastMantleCommand",
        ["HeadOfSecurity"] = "ClothingCyberneticBeastMantleHeadofSecurity",
        ["Quartermaster"] = "ClothingCyberneticBeastMantleSupply",
        ["ResearchDirector"] = "ClothingCyberneticBeastMantleScienceCommand",
        ["AssistantManager"] = "ClothingCyberneticBeastMantleCommand",
        ["BlueShield"] = "ClothingCyberneticBeastMantleBlueshieldOfficer",
        ["NTNCBlueShield"] = "ClothingCyberneticBeastMantleBlueshieldOfficer",
        ["NanoTrasenRepresentative"] = "ClothingCyberneticBeastMantleCentralCommandNoThermal",
        ["Bartender"] = "ClothingCyberneticBeastMantleBar",
        ["Roboticist"] = "ClothingCyberneticBeastMantleRoboticist",
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
            : new EntProtoId("ClothingCyberneticBeastMantle");
        var coords = Transform(args.Mob).Coordinates;
        var mantle = Spawn(prototype, coords);
        _hands.PickupOrDrop(args.Mob, mantle);
    }
}
