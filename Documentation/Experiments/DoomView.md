# Doom view prototype

Branch: `experimental/doom-view`. Enable with `doomview` in the client console (F3).
Use the same branch for both client and server. The feature starts disabled.

## Controls

- WASD: move relative to the camera.
- Click middle mouse once over the game view to lock free-look. Move the mouse to turn horizontally and look slightly up/down. Click it again to release.
- Right-click still opens context menus. Losing window focus or focusing a text field also releases free-look.
- While looking, interaction aims at the center crosshair.
- NumPad 7/9: existing camera rotation bindings, still available.
- Run `doomview` again: return to the normal view.

Mouse turning requires Debug admin permission and server camera rotation enabled.
The cursor is also released on lost window focus, text-field focus, disconnect,
or disabling Doom view. Mouse turning is predicted on the client and applied on
both client and server through the normal movement camera rotation system.

## Rendering

- 70-degree horizontal perspective, 240 wall rays, 12-tile draw distance.
- Walls retain raycast geometry and existing wall icons.
- Ordinary airlocks use a near-full-tile footprint while windoors keep their thin edge-mounted footprint; door textures are chosen in local space so the rotated geometry does not double-rotate directional artwork.
- Full windows occupy a full tile with textured faces, while directional/diagonal windows remain thin and edge-mounted. Enabled occluders control tint.
- Grilles occupy nearly a full tile, with girders set slightly smaller and lower than surrounding glass.
- Wall-mounted fixtures (including lights and cameras) use elevated thin textures instead of cubes.
- Unsupported decoration objects remain billboards instead of becoming oversized boxes.
- Furniture and machinery use shallow boxes with existing artwork stretched over
  sides and tops. Climbable furniture and seats use lower profiles.
- Characters remain layered directional sprites.
- Loose items remain directional sprites at floor level. Anchored vents and atmospheric pipes (which also have an ItemComponent) use flat floor geometry. Pipes, cables, vents, and floor objects
  use flat quads and retain existing per-layer visibility, including covered cables.
- Floor textures come from the actual placed tiles: variants, rotation/mirroring,
  grid transforms, and tile-edge transitions are preserved.
- Floor decals use their existing textures, coordinates, rotation, tint, and order.
- Clicks are projected back to visible objects or floor positions. The server still
  validates interaction range, access, movement, and collisions.

## Limits

This remains a local/admin rendering experiment. Exact native lightmaps, blindness, FOV,
ceilings, terrain elevation, particles, and held-weapon presentation are unfinished.
Empty tiles draw no floor; they are not filled by a checkerboard.

Doom View now approximates darkness using the map's ambient color and nearby enabled PointLight components with radial falloff and occluder ray checks. This is separate from the native 2D light buffer; masked lights, sprite billboard tint, animated shadows, and full lightmap fidelity are not yet supported.

Models are approximations, not authored 3D assets. Thin panels and projected meshes
use painter ordering and subdivided textures; intersections, transparent overlaps,
and close-up texture distortion still need in-game review. Picking uses projected
geometry rather than texture alpha. Special machinery and unusual shapes may need
individual model rules. Door frames depend on the available open-state artwork.

Server PVS limits remain unchanged. The renderer adds work to the normal view, so
profile it on a small map before testing on a populated station.

## Checks

- Toggle, disconnect/reconnect, open chat/console, and alt-tab during mouse-look.
- Turn and walk in every direction, including on a rotated grid.
- Use closed/open doors facing north, south, east and west (including a windoor) and inspect both sides. Test a denied-access door and glass/tinted window.
- Check a full window over a grille, a girder alone, a directional window, and a diagonal window from all four directions.
- Compare wall-mounted lights, cameras, APCs and signs at eye level and from behind the wall.
- Place railings with side, corner, short-corner, and round variants along a long corridor; check for missing horizontal rails.
- Check powered lights, toggled-off lights, occluded rooms, and daylight ambient maps. Confirm that vents and scrubbers are flat even when marked as Item.
- Compare tile variants, rotated arrows, colored markings, and grass transitions.
- Remove and replace a floor tile over cables and pipes; vents remain visible.
- Place an item on the floor; compare furniture scale with a nearby character.
- Resize the window and try both HUD layouts; click positions must stay aligned.

Projection and tile-orientation tests: `Content.Tests/Client/_PV/`.
