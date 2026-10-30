using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Maps;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._PV.PlanetaryLayer;

[AdminCommand(AdminFlags.Mapping)]
public sealed class PlanetLayerTestCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;

    public string Command => "pvplanetlayer_test";
    public string Description => "Creates a planet terrain and station platform test map.";
    public string Help => "Usage: pvplanetlayer_test";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Help);
            return;
        }

        if (shell.Player?.AttachedEntity is not { Valid: true } player)
        {
            shell.WriteError("This command must be run by an in-game admin.");
            return;
        }

        var mapSystem = _entityManager.System<SharedMapSystem>();
        var tileSystem = _entityManager.System<TileSystem>();
        var transformSystem = _entityManager.System<SharedTransformSystem>();
        var mapUid = mapSystem.CreateMap(out _);
        var grid = _entityManager.GetComponent<MapGridComponent>(mapUid);

        var grass = (ContentTileDefinition)_tileDefinitions["FloorPlanetGrass"];
        var desert = (ContentTileDefinition)_tileDefinitions["FloorDesert"];
        var steel = (ContentTileDefinition)_tileDefinitions["FloorSteel"];
        var random = new Random();
        var terrain = new List<(Vector2i, Tile)>(49 * 49);

        for (var x = -24; x <= 24; x++)
        {
            for (var y = -24; y <= 24; y++)
            {
                var tileDefinition = x < -12 || y < -17 ? desert : grass;
                var variant = tileSystem.PickVariant(tileDefinition, random);
                terrain.Add((new Vector2i(x, y), new Tile(tileDefinition.TileId, variant: variant)));
            }
        }

        mapSystem.SetTiles(mapUid, grid, terrain);

        // Build a small steel pad over the terrain. Tile history keeps each original planet tile.
        for (var x = -3; x <= 3; x++)
        {
            for (var y = -3; y <= 3; y++)
            {
                var indices = new Vector2i(x, y);
                if (!mapSystem.TryGetTileRef(mapUid, grid, indices, out var tileRef))
                    continue;

                tileSystem.ReplaceTile(tileRef, steel, mapUid, grid, tileSystem.PickVariant(steel, random));
            }
        }

        transformSystem.SetCoordinates(player, mapSystem.GridTileToLocal(mapUid, grid, Vector2i.Zero));
        shell.WriteLine("Generated a 49x49 planet test map. The center 7x7 steel platform can be removed with the RCD to reveal its original terrain.");
    }
}
