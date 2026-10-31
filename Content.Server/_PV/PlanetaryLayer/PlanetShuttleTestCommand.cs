using Content.Server.Administration;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Administration;
using Content.Shared.Maps;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server._PV.PlanetaryLayer;

[AdminCommand(AdminFlags.Mapping)]
public sealed class PlanetShuttleTestCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private ThrusterSystem _thrusterSystem = default!;

    public string Command => "pvplanetshuttle_test";
    public string Description => "Spawns and pilots a test shuttle over a planet surface.";
    public string Help => "Usage: pvplanetshuttle_test [stop]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1 || args.Length == 1 && !string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
        {
            shell.WriteLine(Help);
            return;
        }

        if (shell.Player?.AttachedEntity is not { Valid: true } player)
        {
            shell.WriteError("This command must be run by an in-game admin.");
            return;
        }

        var shuttleConsoleSystem = _entityManager.System<ShuttleConsoleSystem>();
        if (args.Length == 1)
        {
            if (_entityManager.TryGetComponent(player, out PilotComponent? activePilot))
                shuttleConsoleSystem.RemovePilot(player, activePilot);

            shell.WriteLine("Stopped piloting. The test shuttle grid remains on the map.");
            return;
        }

        var transform = _entityManager.System<SharedTransformSystem>();
        var map = _entityManager.System<SharedMapSystem>();
        var playerMapCoordinates = transform.ToMapCoordinates(new EntityCoordinates(player, Vector2.Zero));
        if (playerMapCoordinates.MapId == MapId.Nullspace)
        {
            shell.WriteError("Your character must be on a planet map.");
            return;
        }

        var mapUid = map.GetMap(playerMapCoordinates.MapId);
        var hasPlanetSurface = _entityManager.HasComponent<PlanetarySurfaceComponent>(mapUid) ||
            map.GetAllGrids(playerMapCoordinates.MapId).Any(grid => _entityManager.HasComponent<PlanetarySurfaceComponent>(grid.Owner));
        if (!hasPlanetSurface)
        {
            shell.WriteError("No PlanetarySurfaceComponent found on this map. Run this on the planetary test map.");
            return;
        }

        if (_entityManager.TryGetComponent(player, out PilotComponent? activePilot))
            shuttleConsoleSystem.RemovePilot(player, activePilot);

        var shuttle = map.CreateGridEntity(mapUid);
        var shuttleUid = shuttle.Owner;
        var shuttleGrid = shuttle.Comp;
        var shuttlePosition = new MapCoordinates(playerMapCoordinates.Position + new Vector2(8, 0), playerMapCoordinates.MapId);
        transform.SetCoordinates(shuttleUid, transform.ToCoordinates(shuttlePosition));

        var tileSystem = _entityManager.System<TileSystem>();
        var floor = (ContentTileDefinition) _tileDefinitions["FloorSteel"];
        var random = new Random();
        var tiles = new List<(Vector2i, Tile)>(49);
        for (var x = -3; x <= 3; x++)
        {
            for (var y = -3; y <= 3; y++)
            {
                var variant = tileSystem.PickVariant(floor, random);
                tiles.Add((new Vector2i(x, y), new Tile(floor.TileId, variant: variant)));
            }
        }

        map.SetTiles(shuttleUid, shuttleGrid, tiles);

        var consoleUid = _entityManager.SpawnEntity("ComputerShuttle", new EntityCoordinates(shuttleUid, new Vector2(0.5f, 0.5f)));
        if (_entityManager.TryGetComponent(consoleUid, out ApcPowerReceiverComponent? powerReceiver))
            powerReceiver.NeedsPower = false;

        var thrusters = new (Vector2 Position, Angle Rotation)[]
        {
            (new Vector2(0.5f, 3.5f), Angle.Zero),
            (new Vector2(3.5f, 0.5f), Angle.FromDegrees(270)),
            (new Vector2(0.5f, -2.5f), Angle.FromDegrees(180)),
            (new Vector2(-2.5f, 0.5f), Angle.FromDegrees(90)),
        };

        foreach (var (position, rotation) in thrusters)
        {
            var thrusterUid = _entityManager.SpawnEntity("Thruster", new EntityCoordinates(shuttleUid, position));
            transform.SetLocalRotation(thrusterUid, rotation);
            if (_entityManager.TryGetComponent(thrusterUid, out ApcPowerReceiverComponent? thrusterPower))
                thrusterPower.NeedsPower = false;

            if (_entityManager.TryGetComponent(thrusterUid, out ThrusterComponent? thrusterComponent) &&
                _thrusterSystem.CanEnable(thrusterUid, thrusterComponent))
                _thrusterSystem.EnableThruster(thrusterUid, thrusterComponent);
        }

        var gyroscopeUid = _entityManager.SpawnEntity("Gyroscope", new EntityCoordinates(shuttleUid, new Vector2(2.5f, 2.5f)));
        if (_entityManager.TryGetComponent(gyroscopeUid, out ApcPowerReceiverComponent? gyroscopePower))
            gyroscopePower.NeedsPower = false;
        if (_entityManager.TryGetComponent(gyroscopeUid, out ThrusterComponent? gyroscopeComponent) &&
            _thrusterSystem.CanEnable(gyroscopeUid, gyroscopeComponent))
            _thrusterSystem.EnableThruster(gyroscopeUid, gyroscopeComponent);

        transform.SetCoordinates(player, new EntityCoordinates(shuttleUid, new Vector2(0.5f, -0.5f)));
        if (!_entityManager.TryGetComponent(consoleUid, out ShuttleConsoleComponent? shuttleConsole))
        {
            shell.WriteError("The test console prototype is missing ShuttleConsoleComponent.");
            return;
        }

        _entityManager.EnsureComponent<PilotComponent>(player);
        shuttleConsoleSystem.AddPilot(consoleUid, player, shuttleConsole);

        shell.WriteLine($"Test shuttle {shuttleUid} spawned over the planetary surface. You are piloting it. Use your shuttle movement bindings; run 'pvplanetshuttle_test stop' to stop piloting.");
    }
}
c82940c8a5de30b413acd19eb1d9b6ea4f048977
