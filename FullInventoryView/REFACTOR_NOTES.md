# Refactor notes

## Why this refactor exists

`InventoryMenuPatcher` had too much responsibility: Harmony patches, Chests Anywhere compatibility, external-button reflection, side-button state, gamepad activation, chest transfer, and grid scrolling.

This pass extracts the Chests Anywhere SortInventoryButton handling into a compatibility adapter and introduces an internal registry for external side buttons.

## What changed

- Added `ExternalSideButtonRegistry`.
- Added `ExternalSideButton` model.
- Added `ChestsAnywhereSortButtonCompat`.
- Added a first public `IFullInventoryViewApi` draft for registering side buttons.
- `InventoryMenuPatcher` now asks the registry for external buttons instead of knowing Chests Anywhere details directly.
- Gamepad activation now uses the generic external-button registry instead of a Chests Anywhere-specific method.

## What did not change yet

- Grid registration for custom inventories is not public yet.
- Manual chest transfer is still inside `InventoryMenuPatcher`.
- Side-button layout engine is still inside `GridViewport` / `InventoryMenuPatcher`.

## Next cleanups

1. Extract chest transfer into `ManualChestTransferService`.
2. Extract side-button placement into `SideButtonLayoutEngine`.
3. Extract navigation wiring into `NavigationGraphBuilder`.
4. Add a second public API surface for registering custom scrollable grids.

## Architecture service split

- Introduced service boundaries around the old monolithic patcher while keeping Harmony entrypoints stable.
- Added `ViewportRegistry` to own `InventoryMenu -> GridViewport` state.
- Added `SideButtonLayoutEngine` as the side-button layout facade.
- Added `NavigationGraphBuilder` as the directional navigation facade.
- Consolidated `GridViewport` as a concrete non-partial viewport type with clearer layout/input sections.
- Kept behavior stable: this refactor is intentionally structural and should not change the public user-facing behavior.

## 2026-06-26 — Remove `partial` classes

- Removed all C# `partial` class declarations from the refactor build.
- Consolidated the `InventoryMenuPatcher` Harmony entrypoints into one concrete class so Harmony method names stay stable.
- Consolidated `GridViewport` into one concrete class so viewport state and methods live in a single type without compiler-side partial merging.
- Kept the service boundaries introduced by the previous refactor (`ViewportRegistry`, external button registry, Chests Anywhere compat, side-button/navigation facades) while avoiding partial class indirection.

This is still behavior-preserving: no new feature was added in this pass.


### UI Info Suite compatibility

UI Info Suite 2 and UI Info Suite 2 Alternative are treated as height-only integrations. Their buttons are already laid out correctly by those mods; Full Inventory View only adds the expanded inventory height to their bottom-edge calculations. This avoids moving or re-registering UI Info buttons unnecessarily.
