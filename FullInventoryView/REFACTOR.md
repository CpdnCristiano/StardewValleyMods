# Refactor plan and current structure

This refactor removes the old `InventoryMenuPatcher` "god class" approach.

The previous file mixed Harmony patch registration, inventory sizing, scroll state, side-button layout, gamepad navigation, reflection, click/scroll input handling, and compatibility helpers. That made every small change risky because a navigation fix could accidentally touch layout, or a layout fix could accidentally rewrite vanilla neighbor IDs.

## Current rule

**Patches call services. Services contain behavior.**

The `Patcher/InventoryMenuPatcher.cs` file is now only the SMAPI/Harmony entry point. It keeps the public compatibility methods used by other patchers, but it does not contain layout, navigation, reflection, or scroll logic.

## Folder responsibilities

```text
FullInventoryView/
├─ Patcher/
│  ├─ InventoryMenuPatcher.cs
│  ├─ UiInfo2Patcher.cs
│  └─ UiInfo2AltPatcher.cs
│
├─ Patches/
│  ├─ PatchRegistration.cs
│  ├─ InventoryPagePatches.cs
│  ├─ InventoryMenuPatches.cs
│  ├─ SideButtonPatches.cs
│  └─ MenuInputPatches.cs
│
├─ Framework/
│  ├─ Layout/
│  │  ├─ GridViewport.cs
│  │  ├─ GridViewportLayoutHelpers.cs
│  │  ├─ InventoryMenuLayoutHelpers.cs
│  │  ├─ InventoryMenuScrollLayout.cs
│  │  ├─ InventoryPageButtonLayout.cs
│  │  └─ SideButtonLayout.cs
│  │
│  ├─ Navigation/
│  │  └─ InventoryPageNavigation.cs
│  │
│  ├─ Input/
│  │  └─ MenuWithInventoryInput.cs
│  │
│  ├─ Reflection/
│  │  ├─ StardewMenuFields.cs
│  │  ├─ MenuComponentFinder.cs
│  │  └─ MenuCachedFields.cs
│  │
│  ├─ State/
│  │  ├─ InventoryPatchState.cs
│  │  └─ BoxedInt.cs
│  │
│  └─ Collections/
│     └─ ScrollableInventoryList.cs
```

## What each layer can do

### `Patcher/`

Contains SMAPI patcher entry points and compatibility patchers.

`InventoryMenuPatcher` should remain small. It should only call `PatchRegistry.Apply(harmony)` and expose compatibility helpers such as `GetExtraHeight()` for old transpilers.

### `Patches/`

Contains Harmony callback wrappers and registration.

Files in this folder should not contain complex layout or navigation rules. A patch callback should be short and delegate to `Framework`.

Example:

```csharp
internal static void InventoryPagePostfix(InventoryPage page) =>
    InventoryPageButtonLayout.InventoryPagePostfix(page);
```

### `Framework/Layout/`

Only layout and drawing/position behavior goes here.

This includes:

- changing `bounds.X` / `bounds.Y`;
- scroll arrow layout;
- side button columns;
- collision spacing;
- menu height offsets;
- shop scroll-bar layout;
- `GridViewport` rendering and scroll geometry.

### `Framework/Navigation/`

Only gamepad neighbor behavior goes here.

This includes:

- `upNeighborID`;
- `downNeighborID`;
- `leftNeighborID`;
- `rightNeighborID`;
- side-column navigation;
- InventoryPage two-column button navigation.

Layout should run first. Navigation should run after layout.

### `Framework/Input/`

Input routing that is not pure navigation lives here.

This currently handles generic menu scroll/click/update interactions for menus with inventory.

### `Framework/Reflection/`

All Stardew private-field access belongs here.

Do not scatter `AccessTools.Field(...)` through patch files. If a new private field is needed, add it to `StardewMenuFields` or a focused reflection helper.

### `Framework/State/`

Shared caches and sizing constants belong here.

This includes:

- row/column constants;
- extra-height calculation;
- `ConditionalWeakTable` caches;
- current parent menu capture;
- layout hash state.

## Safety rules for future changes

1. Do not remap the whole inventory grid unless the bug is specifically in the grid.
2. Layout must finish before navigation is rebuilt.
3. If a button is invisible or not currently active, it must not be considered navigable.
4. Scroll arrows participate in vertical navigation only when scroll exists.
5. Scroll arrows do not count as logical rows for horizontal side-column pairing.
6. Lixeira/trash must remain reachable both with and without scroll.
7. The vanilla drop-item navigation must not be overwritten by side-button navigation.
8. Side-button collision/layout should move only the side-button columns, not random menu components.
9. The patch layer should remain thin; new behavior belongs in `Framework`.

## Next safe optimization pass

Only after this structure compiles and behaves the same, the next pass can consolidate duplicated code:

- create a reusable `DirectionalNavigation` helper for vertical/horizontal neighbor wiring;
- create `SideButtonColumnLayout` for shared column grouping and spacing;
- split ItemGrabMenu-specific side-button logic from generic side-button logic;
- make `GridViewport` usable by future custom chest/grid APIs.

## Manual test checklist

Before releasing any future change, test:

- InventoryPage without scroll;
- InventoryPage with scroll;
- lixeira reachable in both cases;
- Community Center/special button reachable;
- two side-button columns pair by logical row;
- baú vanilla;
- modded chest with buttons on the left;
- dropping item on the floor from chest still works;
- ShopMenu scroll/click behavior;
- MuseumMenu layout;
- controller navigation after first opening the menu, before any mouse click.
