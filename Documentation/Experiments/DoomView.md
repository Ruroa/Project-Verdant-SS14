# Doom view prototype

Branch: `experimental/doom-view`. Enable with `doomview` in the client console (F3).
Use the same branch for both client and server. The feature starts disabled.

## Controls

- WASD: move relative to the camera.
- Hold right mouse over the game view and move the mouse: turn horizontally.
- Release right mouse: release the cursor for inventory and menus.
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
- Doors use thin panels and their current visible sprite layers/animation frames.
- Windows use thin glass panels, with opacity for enabled occluders.
- Furniture and machinery use shallow boxes with existing artwork stretched over
  sides and tops. Climbable furniture and seats use lower profiles.
- Characters remain layered directional sprites.
- Loose items use smaller flat floor quads. Pipes, cables, vents, and floor objects
  use flat quads and retain existing per-layer visibility, including covered cables.
- Floor textures come from the actual placed tiles: variants, rotation/mirroring,
  grid transforms, and tile-edge transitions are preserved.
- Floor decals use their existing textures, coordinates, rotation, tint, and order.
- Clicks are projected back to visible objects or floor positions. The server still
  validates interaction range, access, movement, and collisions.

## Limits

This remains a local/admin rendering experiment. Full lighting, blindness, FOV,
ceilings, terrain elevation, particles, and held-weapon presentation are unfinished.
Empty tiles draw no floor; they are not filled by a checkerboard.

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
- Use a closed/open door, denied-access door, windoor, and glass/tinted window.
- Compare tile variants, rotated arrows, colored markings, and grass transitions.
- Remove and replace a floor tile over cables and pipes; vents remain visible.
- Place an item on the floor; compare furniture scale with a nearby character.
- Resize the window and try both HUD layouts; click positions must stay aligned.

Projection and tile-orientation tests: `Content.Tests/Client/_PV/`.
