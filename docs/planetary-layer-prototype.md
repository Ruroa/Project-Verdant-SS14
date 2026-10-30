# Planet surface and station construction prototype

This prototype uses one gameplay grid. Each tile has one active turf, so movement, atmosphere, collision, and rendering keep using the normal grid rules. Station flooring placed on planet turf uses the existing tile history stack to remember what was there. Removing the station floor restores the saved planet turf. This avoids overlapping grids competing for the same coordinates.

## Test it in game

1. Run `pvplanetlayer_test` as an admin with the Mapping flag.
2. The command creates a separate 49 by 49 test map and moves you to its center.
3. The center has a 7 by 7 steel pad over grass. Grass and desert surround it.
4. Use an RCD and open the **Planetary Construction** menu to place steel flooring, underplating, or walls on the terrain.
5. Select the RCD deconstruct mode and remove a platform tile. Its original grass or desert turf should return.

The ordinary **Walls and Flooring** menu remains usable too. Its existing steel floor recipe also stores and restores terrain because both routes go through the same tile replacement history.

## Current scope

This is a working construction-layer prototype, not two physical grids occupying one location. A real second-grid overlay would require engine changes to define how the grids share coordinates, collision, atmosphere, entity lookup, and rendering. This prototype keeps those systems stable while preserving the planet surface beneath floors. Natural terrain generation is currently a small test command; it does not yet replace the production biome or planet generation pipeline.
