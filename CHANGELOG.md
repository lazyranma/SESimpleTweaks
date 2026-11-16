# Changelog

## Unreleased

- Restored **Don't Blink**. Object Info section scrollbars still flash during refreshes on the Workshop and experimental builds.

## v0.11.0: Support for version 0.26.11.13.14

- Reverted the **Don't Blink** and **Don't Tip Out** tweaks, because the corresponding bugs were fixed.
- Added workshop logo and description.

## v0.10.2: Don't Tip Out and Don't Blink

- Added the **Don't Blink** tweak — section scrollbars in the Object Info window no longer blink
  when the window refreshes.
- Added the **Don't Tip Out** tweak — deposit and stored-resource tooltips no longer disappear
  when the Object Info window refreshes, and their numbers stay live.
- Clarified the **Keep Scanning** description.

## v0.10.1: Support for game version 0.26.10.9.13

- Merged **Mass Shift II** into **In Control**.
- Added a Nexus-format release archive.

## v0.10.0: Support for game version 0.26.9.7.13

- Added support for game versions 0.26.9.7.13 and 0.26.9.3.9.

## v0.9.0: Support for game version 0.26.9.3.9

- Added support for game version 0.26.9.3.9.

## v0.7.1: Support for game version 0.26.7.16.10

- Removed the **Mass Effect** feature.
- Added a line break between download links in the README.

## v0.7.0: Support for game version 0.26.7.3.9

- Added support for game version 0.26.7.3.9.
- Clarified a config option description in the README.

## v0.6.2: Per-feature configuration and a Full Cycle fix

- Added per-feature enable/disable configuration.
- Dropped support for game version 0.26.5.21.13.
- Updated the plugin and Harmony identifier prefix.
- Removed the unnecessary parent-row `LayoutElement` stretch in **Full Cycle**.
- Merged PR #1 (`fix/fullcycle-row-recycle-duplication`).
- Reworded the README disclaimer.

## v0.6.1: Two New Tweaks

- Added **Torch It** — constant-acceleration transfers for cyclical missions (later renamed
  **Torch Cycle**).
- Added the **Road Closed** tweak — suppresses the startup roadmap popup.
- Renamed tweaks.
- Refactored `Patches.cs` into separate feature files.

## v0.6.0: Support for game version 0.26.6.1.10

- Added support for game version 0.26.6.1.10.
- Added the **Rapid Scheduled Disassembly** tweak.
- Added the **Lift Me Off** tweak.

## v0.5.7: Keep Scanning

- Added the **Keep Scanning** tweak — automatic observatory task assignment.
- Replaced reflection-based field access with public properties.

## v0.5.6: Updated to support the latest open beta

- Fixed **Fleet Scales** broken by an open-beta update.
- Refactored and cleaned up code.

## v0.5.5: Enhanced Quick to Orbit II tweak

- **Quick to Orbit II**: Ctrl+Drop opens the mission planner for orbit.

## v0.5.4: Quick to Orbit II

- Added the **Quick to Orbit II** tweak.

## v0.5.3: 1 changed and 2 new tweaks

- Added the **Fleet Scales** tweak.
- Added the **Mass Effect** tweak.
- **Leave No Trace**: replaced the UI filter with rounding.
- Updated zip file naming.
- Catch and log Harmony patch failures in BepInEx.

## v0.5.2

- Added the **Leave No Trace** tweak.

## v0.5.1

- Added the **Full Cycle** tweak.
- Filter **Clear Build Queue** by item type.

## v0.5: new game version + Space Bin

- Added support for game version 0.26.5.15.14 BETA.
- Added **Space Bin** and the cancel-all queue.

## v0.4: Unsticky Crew

- Added the **Unsticky Crew** tweak — copies the crew count from the last module when
  multi-adding and unlocks the crew slider on all module rows.

## v0.3: Quick to orbit

- Added a Plan Mission UI button to quickly set destinations to orbit or surface.

## v0.2: Tooltip fixes

- Fixed tooltips: restored asteroid type info and show the largest deposit by mass.

## v0.1

- Initial release.

---

The `v0.4.1`–`v0.4.4` tags are backports maintained on the `game-0.26.4.29` branch for the older
game version and are not part of the main release line.
