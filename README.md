# Simple Tweaks

**Disclaimer:** This project was generated with LLM assistance.

A BepInEx plugin for Solar Expanse that adds convenient UI tweaks and quality-of-life features.

## Features

- **In Control** — Ctrl+Click to add/remove/queue 100 at a time.
- **Mass Shift** — Shift+Click to cancel construction of all items of the same type.
- **Mass Shift II** — Allows removing 10/100 modules at once from cargo in mission planner.
- **Good Tip** — Object Search resource tooltip shows actually useful info.
- **Asteroid Tow** — Display Atlas/Engine requirements in Object Search.
- **Quick to Orbit** — ↑/↓ button inside the destination field to select body's orbit or a orbit's body.
- **Quick to Orbit II** — Ctrl+Click or Ctrl+Drag on the quick-access body bar to target orbit.
- **Unsticky Crew** — The crew slider is unlocked for all modules.
- **Space Bin** — Trash bin button in the Object Search list. 
- **Full Cycle** — Full info on cyclical mission in the Planet/Orbit view.
- **Leave No Trace** — Eliminate trace amounts of resources left when applying a build discount.
- **Fleet Scales** — Scales the amount of resources added to cargo at once based on the size of the fleet.
- **Mass Effect** — Removes negative-mass solid-phase fractions.
- **Keep Scanning** — Telescopes and observatories will keep scanning for new asteroids and resources.
- **Lift Me Off** — Lifting cargo from the surface to orbit will not show "Max capacity for optimal transfer" = 0 T.
- **Rapid Scheduled Disassembly** — Scrap multiple identical spacecraft or launch vehicles at once.
- **Torch Cycle** — Enable constant-acceleration (torch) mode for cyclical missions.
- **Road Closed** — Suppresses the roadmap window on startup.

## Installation

1. **Install BepInEx** — Follow the [BepInEx setup guide](https://docs.bepinex.dev/articles/user_guide/installation/index.html) for Solar Expanse.
2. **Extract plugin** — Unzip `SimpleTweaks.zip` into `BepInEx\plugins\`.

## Configuration

Each feature can be toggled on or off individually. The config file is created automatically on first launch at:

```
BepInEx\config\com.lazyranma.simpletweaks.cfg
```

It contains a single `[SimpleTweaks]` section with one boolean option per feature (for example, `FleetScales = true`), corresponding to the features listed above. All options default to `true` — every tweak is enabled out of the box. Set an option to `false` to turn off that specific tweak, then restart the game for the change to take effect.

## Download
Game version: 0.26.6.3.14. v0.6.1 [Download](https://github.com/lazyranma/SESimpleTweaks/releases/download/v0.6.1/SimpleTweaks_v0.6.1.zip)
