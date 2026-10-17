# Project Verdant ammunition textures

Custom ammunition sprites for Project Verdant live under this folder. Keep texture assets grouped by what they depict:

```text
Ammunition/
├── Boxes/
│   ├── 9x19mm.rsi/
│   ├── 45acp.rsi/
│   ├── 44magnum.rsi/
│   ├── 57x28mm.rsi/
│   ├── 57x28mm-e.rsi/ (5.7x28 mm Energized)
│   ├── 57x28mm_caseless.rsi/
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
    ├── 57x28mm_caseless.rsi/
    ├── 127x55mm_rsh12.rsi/
    ├── 556x45mm.rsi/
    ├── 762x39mm.rsi/
    ├── 762x51mm_nato.rsi/
    ├── 762x54mm_r.rsi/
    └── 127x99mm.rsi/
```

- Keep conventional 5.7x28 mm, 5.7x28 mm Energized, and 5.7x28 mm caseless assets separate.
- Use one RSI folder per caliber and category. Add state entries to `meta.json` for every PNG frame in that folder.
- Keep ammo variants (such as ball, AP, or incendiary) as states or tintable layers within a caliber's RSI when the same casing or box design can be shared.
- Shotgun shells stay on the existing base-game texture path; do not duplicate them here.
- These are texture-only paths. Prototypes that use them should reference the matching RSI under `_PV/Objects/Weapons/Guns/Ammunition/`.
