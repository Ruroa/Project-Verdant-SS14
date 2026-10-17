# Project Verdant ammunition textures

Custom ammunition artwork is grouped by category and caliber:

```text
Ammunition/
├── Boxes/
│   ├── 9x19mm.rsi/
│   ├── 45acp.rsi/
│   ├── 44magnum.rsi/
│   ├── 57x28mm.rsi/
│   ├── 57x28mm-e.rsi/ (5.7x28 mm Energized)
│   ├── 127x55mm_rsh12.rsi/
│   ├── 556x45mm.rsi/
│   ├── 762x39mm.rsi/
│   ├── 762x51mm_nato.rsi/
│   ├── 762x54mm_r.rsi/
│   └── 127x99mm.rsi/
└── Casings/
    ├── 9x19mm.rsi/
    ├── 45acp.rsi/
    ├── 44magnum.rsi/
    ├── 57x28mm.rsi/
    ├── 57x28mm-e.rsi/ (5.7x28 mm Energized)
    ├── 127x55mm_rsh12.rsi/
    ├── 556x45mm.rsi/
    ├── 762x39mm.rsi/
    ├── 762x51mm_nato.rsi/
    ├── 762x54mm_r.rsi/
    └── 127x99mm.rsi/
```

- Keep conventional 5.7x28 mm, 5.7x28 mm Energized, and 5.7x28 mm caseless ammunition distinct.
- The caseless round has no casing, and there is no custom caseless box sprite in this folder yet.
- Each RSI `meta.json` should list every state that has a matching PNG frame. Names must match exactly.
- Keep ammunition variants (such as ball, AP, or incendiary) as states within a caliber's RSI when they share the same box or casing design.
- Shotgun shells stay on the existing base-game texture path.
- These are texture-only paths. Prototypes that use them should reference the matching RSI under `_PV/Objects/Weapons/Guns/Ammunition/`.
