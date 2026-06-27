# Full Inventory View architecture split

This build removes the C# `partial` class layer while keeping the service-oriented boundaries introduced by the previous refactor. Harmony patch entrypoints still live in one concrete `InventoryMenuPatcher` class so method names remain stable, but framework state, integrations, external buttons, and layout facades are separated.

## Main pieces

| Responsibility | File(s) | Notes |
|---|---|---|
| Viewport registry | `Framework/State/ViewportRegistry.cs` | Owns the `InventoryMenu -> GridViewport` mapping. Harmony code no longer owns the viewport table directly. |
| Grid viewport | `Framework/Layout/GridViewport.cs` | Concrete non-partial viewport type. Holds scroll state, visible slot window, draw/click helpers, InventoryPage layout, and chest/shop side-button layout helpers. |
| Side-button layout facade | `Framework/Layout/SideButtonLayoutEngine.cs` | Small facade used by the patcher to request side-button layout without knowing implementation details. |
| Navigation graph facade | `Framework/Layout/NavigationGraphBuilder.cs` | Small facade for directional navigation wiring and edge-scroll routing. |
| Layout/math helpers | `Framework/Layout/*Helpers.cs`, `InventoryGridMetrics.cs`, `MenuWithInventoryDropNavigation.cs` | Pure helper logic used by viewport and patcher. |
| External button registry | `Framework/ExternalButtons/*` | Registered side buttons from API/compat/fallback discovery, including optional gamepad activation. |
| Chests Anywhere compat | `Framework/Integrations/ChestsAnywhereSortButtonCompat.cs` | Known integration isolated from the main patcher. |
| Public API | `Framework/Api/*` | First API surface for external side-button registration. |
| Vanilla Harmony routing | `Patcher/InventoryMenuPatcher.cs` | Concrete non-partial Harmony patcher. Still the routing point for vanilla menu hooks. |

## Important design rule

Scroll is state, not layout.

`GridViewport.ScrollRow` updates the visible slot window and slot navigation. It should not rebuild `ItemGrabMenu`, call `populateClickableComponentList`, or force a side-button layout pass.

## Why the patcher is still one class

Harmony patches are referenced by method name and many vanilla hooks are static. Keeping those entrypoints in one concrete class reduces risk while the underlying responsibilities move behind registries/facades. The next major cleanup can move more logic into independent services once this build is verified in-game.

## No partial classes

There are no C# `partial` classes in this build. `InventoryMenuPatcher` and `GridViewport` are concrete classes.


### UI Info Suite compatibility

UI Info Suite 2 and UI Info Suite 2 Alternative are treated as height-only integrations. Their buttons are already laid out correctly by those mods; Full Inventory View only adds the expanded inventory height to their bottom-edge calculations. This avoids moving or re-registering UI Info buttons unnecessarily.
