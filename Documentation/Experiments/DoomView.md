# Doom view prototype

Branch: `experimental/doom-view`

A client-side raycasting experiment using the existing station and server simulation.
The feature starts disabled. No engine submodule changes or server gameplay changes.

## Try it

1. Build and run this branch's server and client using the usual scripts.
2. Join a small station map as an administrator and control a character.
3. Open the client console with F3, run `doomview`, then close the console.
4. Move with WASD. Turn with **Camera rotate left/right** in Controls
   (defaults: NumPad 7 and NumPad 9). Camera rotation must be enabled by the server.
5. Click a visible door to use the normal server-validated interaction. Automatic
   airlocks also retain their normal proximity behavior.
6. Run `doomview` again to return to the normal view.

The command uses the engine's debug-rendering access lock. It won't enable when
that lock is active, and disables on losing the controlled entity or debug access.

## Implemented

- 70-degree horizontal perspective with 240 wall rays and a 12-tile draw distance.
- Existing collision geometry for opaque anchored structures and closed doors.
- Existing entity icons as wall textures, with the standard solid-wall texture fallback.
- Distance shading and a placeholder perspective checker floor.
- Upright entity sprites, including clothing layers, with directional frames.
- Wall-depth clipping and back-to-front entity drawing.
- Camera direction tied to the same rotation used by normal movement prediction.
- Screen clicks translated into world positions for the existing interaction pipeline.
- Existing HUD and inventory retained.

## Prototype limits

This is a rendering experiment for local/admin testing, not a public gameplay mode.
It does not reproduce SS14 lighting, blindness, fog of war, or visibility rules yet.
The server still controls movement, collisions, access checks, and interaction range.

- Turning uses the existing camera rotation steps, not free mouse-look.
- Floors are placeholders; actual floor tiles, space, ceilings, and elevation are not rendered.
- Transparent windows and unusual wall shapes need dedicated rendering.
- Furniture remains billboard artwork made for the original overhead camera.
- Open doors currently disappear from the first-person view.
- Entity picking uses projected bounds, not sprite alpha. Overlapping objects can
  still need the existing context menu. Combat aiming needs separate in-game testing.
- Lighting, particles, held-weapon presentation, and world overlays need further work.
- First-person rendering adds work on top of the normal renderer; profile it on a
  small map before trying a populated station.
- No changes to field-of-view or server PVS limits are requested; distant content
  may be absent when the server has not sent it to the client.

## Verification checklist

- Toggle repeatedly; disconnect/reconnect; ensure the normal view returns.
- Approach walls at several angles and verify there is no fisheye distortion.
- Turn and move in all four directions; verify movement agrees with the view.
- Test a closed/open door, denied access, and an out-of-range door.
- Check a clothed player partially hidden behind a wall.
- Resize the window and try both HUD layouts; click positions must still align.
- Take damage/heal and use inventory while the view is enabled.

Projection unit tests live in `Content.Tests/Client/_PV/DoomProjectionTest.cs`.
