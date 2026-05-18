# Simple Tweaks

**Disclaimer:** This project was generated with LLM assistance.

A BepInEx plugin for Solar Expanse that adds convenient UI tweaks and quality-of-life features.

## Features

- **Ctrl+Click x100** — Ctrl+Click to add/remove 100 items for all buttons where Shift+Click adds/removes 10: build queue; selecting spacecraft, adding modules to cargo in mission planner.
- **Module Multi-Removal** — Remove 10 or 100 modules at once from cargo in mission planner.
- **Clear Build Queue** — Shift+Click the "Cancel Building" button or the X button on a facility/spacecraft row to cancel all items of the same type from the construction queue. Also removes stale duplicate Object Info window.
- **Deposit Tooltips** — Hover over resource icons in the Object Search list to see the actual info for the largest deposit.
- **Asteroid Tow** — Display Atlas/Engine requirements for asteroids and comets directly in Object Search.
- **Mirror/Shade Bug Fix** — Corrects solar mirror thermal transfer physics on save load.
- **Quick to Orbit** — Small ↑/↓ button inside the destination field in Plan Mission that selects a body's orbit or a orbit's body.
- **Unsticky Crew** — When clicking + to add crew compartment modules, newly added modules inherit the same crew count as the last module. The crew slider is unlocked for all modules.
- **Space Bin** — Trash bin button in the Object Search list for every asteroid that can be deleted.
- **Full Cycle** — Replaces the bare-bones cyclical mission row in the Planet/Orbit view with the full info from the Mission List window with pause/edit/delete buttons.
- **Leave No Trace** — Eliminates floating-point noise from construction cost discounts by rounding at key arithmetic points, preventing trace amounts from ever appearing.

## Installation

1. **Install BepInEx** — Follow the [BepInEx setup guide](https://docs.bepinex.dev/articles/user_guide/installation/index.html) for Solar Expanse.
2. **Extract plugin** — Unzip `SimpleTweaks.zip` into `BepInEx\plugins\`.

## Download
Game version: 0.26.4.29.11. v0.4.1 [Download](https://github.com/lazyranma/SESimpleTweaks/releases/download/v0.4.1/SimpleTweaks_v0.4.1.zip)

Game version: 0.26.5.15.14. v0.5.2 [Download](https://github.com/lazyranma/SESimpleTweaks/releases/download/v0.5.2/SimpleTweaks_v0.5.2.zip) (open beta)
