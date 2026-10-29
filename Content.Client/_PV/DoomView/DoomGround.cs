using System.Linq;
using System.Numerics;
using Content.Shared.Decals;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._PV.DoomView;

internal sealed class DoomGround
{
    private readonly IEntityManager _entities;
    private readonly SharedMapSystem _maps;
    private readonly SharedTransformSystem _transforms;
    private readonly SpriteSystem _sprites;
    private readonly ITileDefinitionManager _tiles = IoCManager.Resolve<ITileDefinitionManager>();
    private readonly IResourceCache _resources = IoCManager.Resolve<IResourceCache>();
    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();
    private List<Entity<MapGridComponent>> _grids = new();

    public DoomGround(IEntityManager entities)
    {
        _entities = entities;
        _maps = entities.System<SharedMapSystem>();
        _transforms = entities.System<SharedTransformSystem>();
        _sprites = entities.System<SpriteSystem>();
    }

    public void Collect(MapCoordinates origin, Vector2 forward, DoomGeometry geometry)
    {
        var right = DoomProjection.Right(forward);
        var farCenter = origin.Position + forward * DoomProjection.Range;
        var farLeft = farCenter - right * (DoomProjection.Range * DoomProjection.Plane);
        var farRight = farCenter + right * (DoomProjection.Range * DoomProjection.Plane);
        var min = Vector2.Min(origin.Position, Vector2.Min(farLeft, farRight)) - new Vector2(2f);
        var max = Vector2.Max(origin.Position, Vector2.Max(farLeft, farRight)) + new Vector2(2f);
        var area = new Box2(min, max);
        _grids.Clear();
        _maps.FindGridsIntersecting(origin.MapId, area, ref _grids, approx: true);
        foreach (var grid in _grids)
        {
            var matrix = _transforms.GetWorldMatrix(grid.Owner);
            var rotation = _transforms.GetWorldRotation(grid.Owner);
            foreach (var tile in _maps.GetTilesIntersecting(grid.Owner, grid.Comp, area))
            {
                if (!_tiles.TryGetDefinition(tile.Tile.TypeId, out var definition))
                    continue;
                var x = tile.X * grid.Comp.TileSize;
                var y = tile.Y * grid.Comp.TileSize;
                if (definition.Sprite is { } path)
                {
                    var texture = _resources.GetResource<TextureResource>(path).Texture;
                    var variant = Math.Clamp((int) tile.Tile.Variant, 0, Math.Max(0, definition.Variants - 1));
                    var uv = DoomUv.Tile(definition.AllowRotationMirror ? tile.Tile.RotationMirroring : 0);
                    for (var i = 0; i < uv.Length; i++)
                        uv[i].X = (variant + uv[i].X) / Math.Max(1, (int) definition.Variants);
                    Tile(texture, 0, uv);
                }
                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0 || !_maps.TryGetTile(grid.Comp, tile.GridIndices + new Vector2i(dx, dy), out var neighbor) ||
                        neighbor.TypeId == tile.Tile.TypeId || !_tiles.TryGetDefinition(neighbor.TypeId, out var neighborDef) ||
                        neighborDef.EdgeSpritePriority <= definition.EdgeSpritePriority)
                        continue;
                    var direction = new Vector2i(dx, dy).AsDirection().GetOpposite();
                    if (neighborDef.EdgeSprites.TryGetValue(direction, out var edge))
                        Tile(_resources.GetResource<TextureResource>(edge).Texture, 1, null);
                }

                void Tile(Texture texture, int layer, Vector2[]? uv)
                {
                    var size = grid.Comp.TileSize;
                    geometry.Quad(default, texture, Color.White, World(x, y + size), World(x + size, y + size),
                        World(x + size, y), World(x, y), layer, uv);
                }
            }

            if (!_entities.TryGetComponent<DecalGridComponent>(grid.Owner, out var decals))
                continue;
            var localArea = _transforms.GetInvWorldMatrix(grid.Owner).TransformBox(area);
            var chunks = new ChunkIndicesEnumerator(localArea, SharedDecalSystem.ChunkSize);
            var visible = new List<(uint Id, Decal Decal)>();
            while (chunks.MoveNext(out var chunkIndex))
            {
                if (!decals.ChunkCollection.ChunkCollection.TryGetValue(chunkIndex.Value, out var chunk))
                    continue;
                foreach (var (id, decal) in chunk.Decals)
                {
                    if (localArea.Enlarged(2).Contains(decal.Coordinates))
                        visible.Add((id, decal));
                }
            }
            var order = 2;
            foreach (var (_, decal) in visible.OrderBy(d => d.Decal.ZIndex).ThenBy(d => d.Id))
            {
                if (!_prototypes.TryIndex<DecalPrototype>(decal.Id, out var prototype))
                    continue;
                var texture = _sprites.Frame0(prototype.Sprite);
                var size = (Vector2) texture.Size / EyeManager.PixelsPerMeter;
                var center = decal.Coordinates + size / 2;
                var angle = decal.Angle;
                if (prototype.SnapCardinals)
                    angle -= (-forward.ToAngle() + Angle.FromDegrees(90) + rotation).GetCardinalDir().ToAngle();
                geometry.Quad(default, texture, decal.Color ?? Color.White,
                    Corner(-0.5f, 0.5f), Corner(0.5f, 0.5f), Corner(0.5f, -0.5f), Corner(-0.5f, -0.5f), order++);

                Vector3 Corner(float x, float y)
                {
                    var point = center + angle.RotateVec(new Vector2(x, y) * size);
                    return World(point.X, point.Y);
                }
            }

            Vector3 World(float x, float y) => new(Vector2.Transform(new Vector2(x, y), matrix), 0);
        }
    }

}
