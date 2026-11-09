using System.Numerics;
using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeviceLinking.Systems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared.Administration;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Maps;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server._PV.Zpm;

[AdminCommand(AdminFlags.Mapping)]
public sealed class ZpmTestPlatformCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;

    private const int PlatformHalfWidth = 15;
    private const int PlatformHalfHeight = 13;
    private const int ChamberHalfSize = 7;
    private const float LoopPressure = 4_500f;
    private const float CoolantTemperature = 253.15f;

    public string Command => "pvzpmtest";
    public string Description => "Spawns a complete ZPM reactor test platform beside you.";
    public string Help => "Usage: pvzpmtest";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Help);
            return;
        }

        if (shell.Player?.AttachedEntity is not { Valid: true } player)
        {
            shell.WriteError("Run this command in-game while attached to a character.");
            return;
        }

        var transform = _entities.System<SharedTransformSystem>();
        var maps = _entities.System<SharedMapSystem>();
        var playerMapCoordinates = transform.ToMapCoordinates(new EntityCoordinates(player, Vector2.Zero));
        if (playerMapCoordinates.MapId == MapId.Nullspace)
        {
            shell.WriteError("Your character must be on a map.");
            return;
        }

        var mapUid = maps.GetMap(playerMapCoordinates.MapId);
        var gridEntity = maps.CreateGridEntity(mapUid);
        var gridUid = gridEntity.Owner;
        transform.SetMapCoordinates(gridUid, new MapCoordinates(playerMapCoordinates.Position + new Vector2(12f, 0f), playerMapCoordinates.MapId));

        BuildPlatform(gridUid, gridEntity.Comp);

        var controller = Spawn(gridUid, "ZpmController", 0, 2);
        var reactor = Spawn(gridUid, "ZpmModuleSocket", 0, 0);
        var console = Spawn(gridUid, "ZpmConsole", 0, -10);
        if (controller == EntityUid.Invalid || reactor == EntityUid.Invalid || console == EntityUid.Invalid)
        {
            shell.WriteError("Could not spawn the ZPM test setup. Check that the ZPM prototypes are loaded.");
            return;
        }

        SpawnSocketShielding(gridUid);
        SpawnCoolingLoop(gridUid, transform);
        _entities.System<NodeGroupSystem>().ForceUpdate();

        var core = _entities.SpawnEntity("ZpmCore", new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
        var itemSlots = _entities.System<ItemSlotsSystem>();
        if (!itemSlots.TryInsert(controller, "coreSlot", core, null, excludeUserAudio: true, suppressSound: true))
            shell.WriteError("The ZPM platform spawned, but the core could not be inserted into its controller.");

        _entities.System<DeviceLinkSystem>().LinkDefaults(null, controller, console);

        if (_entities.TryGetComponent(console, out ApcPowerReceiverComponent? consolePower))
            consolePower.NeedsPower = false;

        // Evacuate the sealed chamber so the startup interlock passes as soon as pipe gas is supplied.
        var atmosphere = _entities.System<AtmosphereSystem>();
        for (var x = -ChamberHalfSize + 1; x < ChamberHalfSize; x++)
        {
            for (var y = -ChamberHalfSize + 1; y < ChamberHalfSize; y++)
            {
                var mixture = atmosphere.GetTileMixture(gridUid, null, new Vector2i(x, y), excite: true);
                mixture?.RemoveRatio(1f);
            }
        }

        transform.SetCoordinates(player, new EntityCoordinates(gridUid, new Vector2(0.5f, -8.5f)));
        shell.WriteLine($"ZPM test platform spawned on grid {gridUid}. The core is inserted, the console is linked, the chamber is evacuated, and the cooling loop is prefilled with oxygen.");
    }

    private void BuildPlatform(EntityUid gridUid, MapGridComponent grid)
    {
        var tileSystem = _entities.System<TileSystem>();
        var floor = (ContentTileDefinition) _tileDefinitions["FloorSteel"];
        var random = new Random();
        var tiles = new List<(Vector2i, Tile)>((PlatformHalfWidth * 2 + 1) * (PlatformHalfHeight * 2 + 1));

        for (var x = -PlatformHalfWidth; x <= PlatformHalfWidth; x++)
        {
            for (var y = -PlatformHalfHeight; y <= PlatformHalfHeight; y++)
            {
                tiles.Add((new Vector2i(x, y), new Tile(floor.TileId, variant: tileSystem.PickVariant(floor, random))));
            }
        }

        _entities.System<SharedMapSystem>().SetTiles(gridUid, grid, tiles);

        // Reinforced glass makes the 15x15 reactor room visible while keeping it airtight.
        for (var i = -ChamberHalfSize; i <= ChamberHalfSize; i++)
        {
            if (i == 0)
            {
                Spawn(gridUid, "AirlockGlass", i, -ChamberHalfSize);
                Spawn(gridUid, "ReinforcedWindow", i, ChamberHalfSize);
            }
            else
            {
                Spawn(gridUid, "ReinforcedWindow", i, -ChamberHalfSize);
                Spawn(gridUid, "ReinforcedWindow", i, ChamberHalfSize);
            }

            if (i != -ChamberHalfSize && i != ChamberHalfSize)
            {
                Spawn(gridUid, "ReinforcedWindow", -ChamberHalfSize, i);
                Spawn(gridUid, "ReinforcedWindow", ChamberHalfSize, i);
            }
        }
    }

    private void SpawnSocketShielding(EntityUid gridUid)
    {
        Spawn(gridUid, "ZpmSocketTopLeft", -1, 1);
        Spawn(gridUid, "ZpmSocketTopMiddle", 0, 1);
        Spawn(gridUid, "ZpmSocketTopRight", 1, 1);
        Spawn(gridUid, "ZpmSocketHeatOutlet", -1, 0);
        Spawn(gridUid, "ZpmSocketCoolingInlet", 1, 0);
        Spawn(gridUid, "ZpmSocketBottomLeft", -1, -1);
        Spawn(gridUid, "ZpmSocketBottomMiddle", 0, -1);
        Spawn(gridUid, "ZpmSocketBottomRight", 1, -1);
    }

    private void SpawnCoolingLoop(EntityUid gridUid, SharedTransformSystem transform)
    {
        // The loop leaves the sealed room through the window line so its radiators can
        // exchange heat with the surrounding atmosphere.
        for (var x = -7; x <= -2; x++)
            Spawn(gridUid, "PVZpmTestPipe", x, 0);

        Spawn(gridUid, "PVZpmTestPipe", -8, 0);
        for (var y = 1; y <= 8; y++)
            Spawn(gridUid, "PVZpmTestPipe", -8, y);

        for (var x = -8; x <= 10; x++)
        {
            if (x == -2 || x == 2)
            {
                var radiator = Spawn(gridUid, "HeatExchanger", x, 9);
                transform.SetLocalRotation(radiator, Angle.FromDegrees(90));
            }
            else if (x == 0)
            {
                var freezer = Spawn(gridUid, "GasThermoMachineFreezerEnabled", x, 9);
                transform.SetLocalRotation(freezer, Angle.FromDegrees(90));
                if (_entities.TryGetComponent(freezer, out GasThermoMachineComponent? thermo))
                    thermo.TargetTemperature = CoolantTemperature;
                if (_entities.TryGetComponent(freezer, out ApcPowerReceiverComponent? freezerPower))
                    freezerPower.NeedsPower = false;
            }
            else
            {
                Spawn(gridUid, "PVZpmTestPipe", x, 9);
            }
        }

        for (var y = 1; y <= 8; y++)
            Spawn(gridUid, "PVZpmTestPipe", 10, y);

        Spawn(gridUid, "PVZpmTestPipe", 10, 0);
        var pump = Spawn(gridUid, "GasPressurePump", 9, 0);
        transform.SetLocalRotation(pump, Angle.FromDegrees(270));
        if (_entities.TryGetComponent(pump, out GasPressurePumpComponent? pressurePump))
        {
            pressurePump.Enabled = true;
            pressurePump.TargetPressure = LoopPressure;
        }
        if (_entities.TryGetComponent(pump, out ApcPowerReceiverComponent? pumpPower))
            pumpPower.NeedsPower = false;
        for (var x = 2; x <= 8; x++)
            Spawn(gridUid, "PVZpmTestPipe", x, 0);
    }

    private EntityUid Spawn(EntityUid gridUid, string prototype, int x, int y)
    {
        return _entities.SpawnEntity(prototype, new EntityCoordinates(gridUid, new Vector2(x + 0.5f, y + 0.5f)));
    }
}
