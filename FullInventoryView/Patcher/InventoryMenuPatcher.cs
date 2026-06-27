using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Diagnostics;
using CpdnCristiano.StardewValleyMod.FullInventoryView;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Collections;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.ExternalButtons;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Integrations;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Reflection;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.State;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Objects;
using static StardewValley.Menus.InventoryMenu;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher
{
    internal class InventoryMenuPatcher : BasePatcher
    {
        // ---- Core / Harmony registration ----
        private const int ArrowIdUp = 11001;
        private const int ArrowIdDown = 11002;

        private static readonly ConditionalWeakTable<IClickableMenu, ChestMenuLayoutState> ChestLayoutStates =
            new();
        private sealed class ChestMenuLayoutState
        {
            public int LayoutHash { get; set; } = int.MinValue;
            public int PendingLayoutPasses { get; set; } = 0;
            public int PendingBottomProbePasses { get; set; } = 0;
            public string LastTrigger { get; set; } = "none";
            public bool IsInsideLayout { get; set; } = false;
            public int LastPlayerScrollRow { get; set; } = 0;
            public int LastPlayerMaxScrollRow { get; set; } = 0;
            public int LastPlayerMenuHash { get; set; } = int.MinValue;
            public List<InventoryMenu> InventoryMenus { get; set; } = new();
            public Dictionary<string, List<Rectangle>> SideButtonTargets { get; set; } = new();
        }

        public static IClickableMenu? CurrentParentMenu = null;


        private static string DescribeComponent(ClickableComponent? component)
        {
            if (component == null)
                return "<null>";

            string name = string.IsNullOrWhiteSpace(component.name) ? "<no-name>" : component.name;
            Rectangle bounds = component.bounds;
            return $"id={component.myID}, name={name}, x={bounds.X}, y={bounds.Y}, w={bounds.Width}, h={bounds.Height}";
        }


        private static string DescribeComponentsForLog(IEnumerable<ClickableComponent> components, int max = 8)
        {
            var list = components.Where(c => c != null).Take(max + 1).ToList();
            int shown = Math.Min(max, list.Count);
            string body = string.Join(" | ", list.Take(shown).Select(DescribeComponent));
            int extra = list.Count > max ? list.Count - max : 0;
            return $"count={list.Count}{(extra > 0 ? "+" : string.Empty)} [{body}{(extra > 0 ? $", ... +{extra}" : string.Empty)}]";
        }

        internal static void NotifyMenuOpened(IClickableMenu menu)
        {
            NotifyMenuChanged(null, menu);
        }

        internal static void NotifyExternalButtonRegistered(IClickableMenu menu, string sourceId)
        {
            RequestExternalButtonLayout(menu, $"ExternalButton:{sourceId}");
        }

        private static void RequestExternalButtonLayout(IClickableMenu menu, string reason)
        {
            if (!IsSideLayoutMenu(menu))
                return;

            var state = GetChestLayoutState(menu);
            state.PendingBottomProbePasses = 0;
            state.PendingLayoutPasses = 0;

            if (menu is ItemGrabMenu && !IsItemGrabMenuClickableListReady(menu))
            {
                Log.Debug($"[FIV/ChestLifecycle] skip external layout: menu={menu.GetType().Name}#{menu.GetHashCode()}, reason={reason}, components={menu.allClickableComponents?.Count ?? 0}, not-populated-yet");
                return;
            }

            Log.Debug($"[FIV/ChestLifecycle] one-shot external layout: menu={menu.GetType().Name}#{menu.GetHashCode()}, reason={reason}, components={menu.allClickableComponents?.Count ?? 0}");
            RepositionAndWireSideButtons(menu, reason, force: true);
        }

        internal static void NotifyMenuChanged(IClickableMenu? oldMenu, IClickableMenu newMenu)
        {
            if (!IsSideLayoutMenu(newMenu))
                return;

            bool inheritedState = TryInheritChestLayoutState(newMenu, oldMenu, "MenuChanged");

            if (newMenu is ItemGrabMenu)
            {
                var state = GetChestLayoutState(newMenu);
                state.PendingBottomProbePasses = 0;
                state.PendingLayoutPasses = 0;

                // Chest menus are rebuilt by vanilla/other mods during color-picker and tab changes.
                // Do one deterministic layout on the live menu only; don't keep a multi-frame probe
                // alive, because that made FIV and Chests Anywhere fight over trash/sort positions.
                RepositionAndWireSideButtons(newMenu, inheritedState ? "MenuChanged:handoff" : "MenuChanged", force: true);
                return;
            }

            ScheduleChestLayout(newMenu, "MenuChanged", 2, resetHash: !inheritedState);
            RepositionAndWireSideButtons(newMenu, "MenuChanged", force: true);
        }

        private static void ScheduleChestBottomProbe(IClickableMenu menu, string trigger, int passes)
        {
            if (passes <= 0 || menu is not ItemGrabMenu)
                return;

            var state = GetChestLayoutState(menu);
            state.PendingBottomProbePasses = Math.Max(state.PendingBottomProbePasses, passes);
            state.LastTrigger = trigger;
            Log.Debug($"[FIV/ChestLifecycle] schedule bottom probe: menu={menu.GetType().Name}#{menu.GetHashCode()}, trigger={trigger}, passes={state.PendingBottomProbePasses}, components={menu.allClickableComponents?.Count ?? 0}");
        }

        private static bool IsSideLayoutMenu(IClickableMenu menu)
        {
            return menu is ItemGrabMenu or ShopMenu or MenuWithInventory or MuseumMenu;
        }

        private static ChestMenuLayoutState GetChestLayoutState(IClickableMenu menu)
        {
            return ChestLayoutStates.GetValue(menu, _ => new ChestMenuLayoutState());
        }

        private static bool TryInheritChestLayoutState(
            IClickableMenu targetMenu,
            IClickableMenu? sourceMenu,
            string reason
        )
        {
            if (!IsSideLayoutMenu(targetMenu))
                return false;

            var targetState = GetChestLayoutState(targetMenu);
            if (targetState.SideButtonTargets.Count > 0 || targetState.LastPlayerScrollRow > 0)
                return true;

            if (sourceMenu == null || ReferenceEquals(sourceMenu, targetMenu))
                return false;

            if (!IsSideLayoutMenu(sourceMenu))
                return false;

            if (!ChestLayoutStates.TryGetValue(sourceMenu, out var sourceState))
                return false;

            CopyChestLayoutState(sourceState, targetState);
            Log.Debug(
                $"[FIV/ChestLifecycle] inherited layout state: reason={reason}, oldMenu={sourceMenu.GetType().Name}#{sourceMenu.GetHashCode()}, newMenu={targetMenu.GetType().Name}#{targetMenu.GetHashCode()}, scrollRow={targetState.LastPlayerScrollRow}, maxScroll={targetState.LastPlayerMaxScrollRow}, targetKeys={targetState.SideButtonTargets.Count}, layoutHash={targetState.LayoutHash}"
            );
            return true;
        }

        private static bool TryInheritChestLayoutStateFromActiveMenu(IClickableMenu targetMenu, string reason)
        {
            IClickableMenu? active = Game1.activeClickableMenu;
            if (active == null || ReferenceEquals(active, targetMenu))
                return false;

            // During `new ItemGrabMenu(oldMenu)` the new menu calls populateClickableComponentList
            // before Game1.activeClickableMenu is swapped. That is the only chance to give the
            // new nested player InventoryMenu the old scroll row/cache before it draws at row 0.
            return TryInheritChestLayoutState(targetMenu, active, reason);
        }

        private static void CopyChestLayoutState(ChestMenuLayoutState source, ChestMenuLayoutState target)
        {
            target.LayoutHash = source.LayoutHash;
            target.LastPlayerScrollRow = source.LastPlayerScrollRow;
            target.LastPlayerMaxScrollRow = source.LastPlayerMaxScrollRow;
            target.LastPlayerMenuHash = source.LastPlayerMenuHash;
            target.LastTrigger = source.LastTrigger;
            target.SideButtonTargets = source.SideButtonTargets.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToList()
            );
        }

        private static void ScheduleChestLayout(
            IClickableMenu menu,
            string trigger,
            int passes = 1,
            bool resetHash = false
        )
        {
            if (!IsSideLayoutMenu(menu) || passes <= 0)
                return;

            var state = GetChestLayoutState(menu);
            state.PendingLayoutPasses = Math.Max(state.PendingLayoutPasses, passes);
            state.LastTrigger = trigger;
            if (resetHash)
                state.LayoutHash = int.MinValue;

            LayoutDiagnostics.DebugChanged(
                $"ChestLifecycle:schedule:{menu.GetHashCode()}:{trigger}",
                $"[FIV/ChestLifecycle] schedule layout: menu={menu.GetType().Name}, trigger={trigger}, passes={state.PendingLayoutPasses}, resetHash={resetHash}"
            );
        }

        private static List<InventoryMenu> GetKnownInventoryMenus(IClickableMenu menu)
        {
            if (IsSideLayoutMenu(menu) && ChestLayoutStates.TryGetValue(menu, out var state) && state.InventoryMenus.Count > 0)
                return state.InventoryMenus;

            return MenuComponentFinder.FindInventoryMenus(menu);
        }

        public static int GetExtraHeight()
        {
            return InventoryGridMetrics.GetExtraHeight();
        }

        public static int GetDoubleExtraHeight()
        {
            return GetExtraHeight() * 2;
        }

        public static int GetBillboardOffset()
        {
            return InventoryGridMetrics.GetBillboardOffset();
        }

        private static int GetExtraRow()
        {
            return InventoryGridMetrics.GetExtraRow();
        }

        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            harmony.Patch(
                original: this.RequireConstructor<InventoryMenu>(
                    new Type[]
                    {
                        typeof(int),
                        typeof(int),
                        typeof(bool),
                        typeof(IList<Item>),
                        typeof(highlightThisItem),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(bool),
                    }
                ),
                prefix: this.GetHarmonyMethod(nameof(InventoryMenuPrefix))
            );

            harmony.Patch(
                original: this.RequireConstructor<InventoryPage>(
                    new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) }
                ),
                prefix: this.GetHarmonyMethod(nameof(InventoryPagePrefix)),
                postfix: this.GetHarmonyMethod(nameof(InventoryPagePostfix))
            );

            harmony.Patch(
                original: this.RequireMethod<IClickableMenu>(
                    nameof(IClickableMenu.isWithinBounds),
                    new Type[] { typeof(int), typeof(int) }
                ),
                prefix: this.GetHarmonyMethod(nameof(isWithinBoundsPrefix))
            );

            harmony.Patch(
                original: this.RequireConstructor<CraftingPage>(
                    new Type[]
                    {
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(bool),
                        typeof(bool),
                        typeof(List<IInventory>),
                    }
                ),
                prefix: this.GetHarmonyMethod(nameof(CraftingPagePrefix))
            );

            harmony.Patch(
                original: this.RequireConstructor<IClickableMenu>(
                    new Type[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool) }
                ),
                prefix: this.GetHarmonyMethod(nameof(iClickableMenuPrefix))
            );

            harmony.Patch(
                original: this.RequireMethod<ShopMenu>(nameof(ShopMenu.updatePosition)),
                postfix: this.GetHarmonyMethod(nameof(updatePositionPostfix))
            );

            harmony.Patch(
                original: this.RequireMethod<ShopMenu>(nameof(ShopMenu.drawCurrency)),
                prefix: this.GetHarmonyMethod(nameof(drawCurrencyPrefix))
            );

            harmony.Patch(
                original: this.RequireMethod<ItemGrabMenu>(
                    nameof(ItemGrabMenu.initializeShippingBin)
                ),
                postfix: this.GetHarmonyMethod(nameof(initializeShippingBinPostfix))
            );

            // RepositionSideButtons is called by setSourceItem (triggered by behaviorOnItemGrab
            // on every item click/transfer) and by gameWindowSizeChanged — both without calling
            // populateClickableComponentList. This means our side-button layout gets silently
            // overwritten back to vanilla positions after every item grab, with no event for
            // the mod to observe. Patching it directly is the only reliable intercept point.
            harmony.Patch(
                original: this.RequireMethod<ItemGrabMenu>(
                    nameof(ItemGrabMenu.RepositionSideButtons)
                ),
                postfix: this.GetHarmonyMethod(nameof(ItemGrabMenuRepositionSideButtonsPostfix))
            );

            SafePatchMethod(
                harmony,
                typeof(ItemGrabMenu),
                nameof(ItemGrabMenu.gameWindowSizeChanged),
                new Type[] { typeof(Rectangle), typeof(Rectangle) },
                postfixName: nameof(ItemGrabMenuRepositionSideButtonsPostfix)
            );

            harmony.Patch(
                original: this.RequireMethod<ShopMenu>(
                    nameof(ShopMenu.draw),
                    new Type[] { typeof(SpriteBatch) }
                ),
                transpiler: this.GetHarmonyMethod(nameof(drawTranspiler))
            );

            harmony.Patch(
                original: this.RequireMethod<ShopMenu>(
                    nameof(ShopMenu.receiveLeftClick),
                    new Type[] { typeof(int), typeof(int), typeof(bool) }
                ),
                transpiler: this.GetHarmonyMethod(nameof(receiveLeftClickTranspiler))
            );

            PatchInventoryMenuMethods(harmony);
            PatchInventoryPageMethods(harmony);

            SafePatchMethod(
                harmony,
                typeof(IClickableMenu),
                nameof(IClickableMenu.receiveScrollWheelAction),
                new Type[] { typeof(int) },
                prefixName: nameof(MenuReceiveScrollWheelActionPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(IClickableMenu),
                nameof(IClickableMenu.receiveLeftClick),
                new Type[] { typeof(int), typeof(int), typeof(bool) },
                prefixName: nameof(MenuReceiveLeftClickPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(ShopMenu),
                nameof(ShopMenu.receiveScrollWheelAction),
                new Type[] { typeof(int) },
                prefixName: nameof(MenuReceiveScrollWheelActionPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(ShopMenu),
                nameof(ShopMenu.receiveLeftClick),
                new Type[] { typeof(int), typeof(int), typeof(bool) },
                prefixName: nameof(MenuReceiveLeftClickPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(ItemGrabMenu),
                nameof(ItemGrabMenu.receiveScrollWheelAction),
                new Type[] { typeof(int) },
                prefixName: nameof(MenuReceiveScrollWheelActionPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(ItemGrabMenu),
                nameof(ItemGrabMenu.receiveLeftClick),
                new Type[] { typeof(int), typeof(int), typeof(bool) },
                prefixName: nameof(MenuReceiveLeftClickPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(ItemGrabMenu),
                nameof(ItemGrabMenu.receiveRightClick),
                new Type[] { typeof(int), typeof(int), typeof(bool) },
                prefixName: nameof(MenuReceiveRightClickPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(MuseumMenu),
                nameof(MuseumMenu.receiveLeftClick),
                new Type[] { typeof(int), typeof(int), typeof(bool) },
                prefixName: nameof(MenuReceiveLeftClickPrefix)
            );

            SafePatchMethod(
                harmony,
                typeof(IClickableMenu),
                nameof(IClickableMenu.update),
                new Type[] { typeof(GameTime) },
                postfixName: nameof(MenuUpdatePostfix)
            );
            SafePatchMethod(
                harmony,
                typeof(ShopMenu),
                nameof(ShopMenu.update),
                new Type[] { typeof(GameTime) },
                postfixName: nameof(MenuUpdatePostfix)
            );
            SafePatchMethod(
                harmony,
                typeof(ItemGrabMenu),
                nameof(ItemGrabMenu.update),
                new Type[] { typeof(GameTime) },
                postfixName: nameof(MenuUpdatePostfix)
            );
            SafePatchMethod(
                harmony,
                typeof(MuseumMenu),
                nameof(MuseumMenu.update),
                new Type[] { typeof(GameTime) },
                postfixName: nameof(MenuUpdatePostfix)
            );
            SafePatchMethod(
                harmony,
                typeof(ItemGrabMenu),
                nameof(ItemGrabMenu.draw),
                new Type[] { typeof(SpriteBatch) },
                prefixName: nameof(ItemGrabMenuDrawPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(MuseumMenu),
                nameof(MuseumMenu.draw),
                new Type[] { typeof(SpriteBatch) },
                prefixName: nameof(MuseumMenuDrawPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(ShopMenu),
                nameof(ShopMenu.draw),
                new Type[] { typeof(SpriteBatch) },
                prefixName: nameof(ShopMenuDrawPrefix)
            );
            // Capture the active parent menu while vanilla constructors build nested inventory layouts.
            harmony.Patch(
                original: this.RequireConstructor<MenuWithInventory>(
                    new Type[]
                    {
                        typeof(highlightThisItem),
                        typeof(bool),
                        typeof(bool),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(ItemExitBehavior),
                        typeof(bool),
                    }
                ),
                prefix: this.GetHarmonyMethod(nameof(CaptureMenuInstancePrefix)),
                postfix: this.GetHarmonyMethod(nameof(ReleaseMenuInstancePostfix))
            );

            ChestsAnywhereSortButtonCompat.RequestLayout = RequestExternalButtonLayout;
            ChestsAnywhereSortButtonCompat.Apply(harmony);
            RemoteFridgeStorageCompat.RequestLayout = RequestExternalButtonLayout;
            RemoteFridgeStorageCompat.Apply(harmony);
            BetterGameMenuCompat.Apply(harmony);
        }

        private void SafePatchMethod(
            Harmony harmony,
            Type type,
            string methodName,
            Type[] parameters,
            string? prefixName = null,
            string? postfixName = null
        )
        {
            MethodInfo? method = type.GetMethod(
                methodName,
                BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance
                    | BindingFlags.DeclaredOnly,
                null,
                parameters,
                null
            );
            if (method != null)
            {
                harmony.Patch(
                    original: method,
                    prefix: prefixName == null ? null : this.GetHarmonyMethod(prefixName),
                    postfix: postfixName == null ? null : this.GetHarmonyMethod(postfixName)
                );
            }
        }

        private static void CaptureMenuInstancePrefix(MenuWithInventory __instance)
        {
            CurrentParentMenu = __instance;
        }

        private static void ReleaseMenuInstancePostfix()
        {
            CurrentParentMenu = null;
        }

        private void PatchInventoryMenuMethods(Harmony harmony)
        {
            string[] methodNames =
            {
                "draw",
                "leftClick",
                "rightClick",
                "hover",
                "receiveLeftClick",
                "performHoverAction",
                "getItemAt",
                "getItemFromClickableComponent",
                "tryToAddItem",
            };

            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(InventoryMenu)))
            {
                if (!methodNames.Contains(method.Name))
                    continue;

                harmony.Patch(
                    original: method,
                    prefix: this.GetHarmonyMethod(nameof(InventoryMenuMethodPrefix)),
                    postfix: this.GetHarmonyMethod(nameof(InventoryMenuMethodPostfix))
                );
            }

            harmony.Patch(
                original: this.RequireMethod<InventoryMenu>(
                    nameof(InventoryMenu.draw),
                    new Type[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(int) }
                ),
                postfix: this.GetHarmonyMethod(nameof(InventoryMenuDrawPostfix))
            );

            SafePatchMethod(
                harmony,
                typeof(InventoryMenu),
                nameof(InventoryMenu.performHoverAction),
                new Type[] { typeof(int), typeof(int) },
                postfixName: nameof(InventoryMenuPerformHoverActionPostfix)
            );
            SafePatchMethod(
                harmony,
                typeof(InventoryMenu),
                "hover",
                new Type[] { typeof(int), typeof(int) },
                postfixName: nameof(InventoryMenuPerformHoverActionPostfix)
            );

            SafePatchMethod(
                harmony,
                typeof(InventoryMenu),
                nameof(InventoryMenu.receiveLeftClick),
                new Type[] { typeof(int), typeof(int), typeof(bool) },
                prefixName: nameof(InventoryMenuReceiveLeftClickPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(InventoryMenu),
                "leftClick",
                new Type[] { typeof(int), typeof(int), typeof(Item), typeof(bool) },
                prefixName: nameof(InventoryMenuLeftClickPrefix)
            );
        }

        private void PatchInventoryPageMethods(Harmony harmony)
        {
            harmony.Patch(
                original: this.RequireMethod<GameMenu>(
                    nameof(GameMenu.receiveScrollWheelAction),
                    new Type[] { typeof(int) }
                ),
                prefix: this.GetHarmonyMethod(nameof(GameMenuReceiveScrollWheelActionPrefix))
            );
            harmony.Patch(
                original: this.RequireMethod<GameMenu>(
                    nameof(GameMenu.update),
                    new Type[] { typeof(GameTime) }
                ),
                postfix: this.GetHarmonyMethod(nameof(GameMenuUpdatePostfix), (int)Priority.Last)
            );

            // Run after other mods so custom arrows and neighbors can be finalized last.
            harmony.Patch(
                original: this.RequireMethod<InventoryPage>(
                    nameof(InventoryPage.setUpForGamePadMode)
                ),
                postfix: this.GetHarmonyMethod(
                    nameof(InventoryPageSetUpForGamePadModePostfix),
                    (int)Priority.Last
                )
            );

            harmony.Patch(
                original: this.RequireMethod<GameMenu>(nameof(GameMenu.setUpForGamePadMode)),
                postfix: this.GetHarmonyMethod(
                    nameof(GameMenuSetUpForGamePadModePostfix),
                    (int)Priority.Last
                )
            );

            harmony.Patch(
                original: this.RequireMethod<IClickableMenu>(
                    nameof(IClickableMenu.populateClickableComponentList)
                ),
                postfix: this.GetHarmonyMethod(
                    nameof(PopulateClickableComponentListPostfix),
                    (int)Priority.Last
                )
            );

            SafePatchMethod(
                harmony,
                typeof(IClickableMenu),
                nameof(IClickableMenu.receiveGamePadButton),
                new Type[] { typeof(Buttons) },
                prefixName: nameof(MenuReceiveGamePadButtonPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(InventoryPage),
                nameof(InventoryPage.receiveGamePadButton),
                new Type[] { typeof(Buttons) },
                prefixName: nameof(MenuReceiveGamePadButtonPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(ShopMenu),
                nameof(ShopMenu.receiveGamePadButton),
                new Type[] { typeof(Buttons) },
                prefixName: nameof(MenuReceiveGamePadButtonPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(ItemGrabMenu),
                nameof(ItemGrabMenu.receiveGamePadButton),
                new Type[] { typeof(Buttons) },
                prefixName: nameof(MenuReceiveGamePadButtonPrefix)
            );

            SafePatchMethod(
                harmony,
                typeof(IClickableMenu),
                nameof(IClickableMenu.applyMovementKey),
                new Type[] { typeof(int) },
                prefixName: nameof(IClickableMenuApplyMovementKeyPrefix)
            );
            SafePatchMethod(
                harmony,
                typeof(ItemGrabMenu),
                "customSnapBehavior",
                new Type[] { typeof(int), typeof(int), typeof(int) },
                prefixName: nameof(ItemGrabMenuCustomSnapBehaviorPrefix)
            );
        }
    

        // ---- Navigation graph routing ----
        private static void GameMenuSetUpForGamePadModePostfix(GameMenu __instance)
        {
            if (__instance.allClickableComponents == null)
                return;
            if (__instance.GetCurrentPage() is not InventoryPage page)
                return;

            EnsureScrollButtons(page);

            // EnsureScrollButtons already adds/removes the scroll arrows from both the page and
            // the active GameMenu list depending on whether the InventoryPage actually has scroll.
            WireGamepadNavigation(page, __instance.allClickableComponents);
        }

        private static void PopulateClickableComponentListPostfix(IClickableMenu __instance)
        {
            if (__instance is InventoryPage page)
            {
                EnsureScrollButtons(page);
                return;
            }

            if (__instance is ItemGrabMenu)
            {
                bool inherited = TryInheritChestLayoutStateFromActiveMenu(
                    __instance,
                    "populateClickableComponentList"
                );

                var state = GetChestLayoutState(__instance);
                state.PendingBottomProbePasses = 0;
                state.PendingLayoutPasses = 0;

                // One layout per rebuild. Chests Anywhere will register its live SortInventoryButton
                // through its own postfix and request exactly one second layout pass if needed.
                RepositionAndWireSideButtons(__instance, inherited ? "populateClickableComponentList:handoff" : "populateClickableComponentList", force: true);
                return;
            }

            if (IsSideLayoutMenu(__instance))
            {
                TryInheritChestLayoutStateFromActiveMenu(__instance, "populateClickableComponentList");
                ScheduleChestLayout(__instance, "populateClickableComponentList", 2);
                RepositionAndWireSideButtons(__instance, "populateClickableComponentList", force: true);
            }
        }

        private static void InventoryPageSetUpForGamePadModePostfix(InventoryPage __instance)
        {
            if (__instance.allClickableComponents == null)
                return;

            EnsureScrollButtons(__instance);
            WireGamepadNavigation(__instance, __instance.allClickableComponents);
        }

        private static void RefreshInventoryPageGamepadNavigation(InventoryPage page)
        {
            EnsureScrollButtons(page);

            if (Game1.activeClickableMenu is GameMenu gameMenu && gameMenu.allClickableComponents != null)
            {
                WireGamepadNavigation(page, gameMenu.allClickableComponents);
                return;
            }

            if (page.allClickableComponents != null)
                WireGamepadNavigation(page, page.allClickableComponents);
        }

        internal static void WireGamepadNavigation(
            InventoryPage page,
            List<ClickableComponent> activeComponents
        )
        {
            if (activeComponents == null || activeComponents.Count == 0)
                return;
            if (StardewMenuFields.InventoryPageInventory.GetValue(page) is not InventoryMenu inventoryMenu)
                return;
            if (
                StardewMenuFields.Inventory.GetValue(inventoryMenu) is not List<ClickableComponent> slots
                || slots.Count == 0
            )
                return;

            // Use a HashSet for O(1) lookups of components currently in activeComponents
            var activeSet = new HashSet<ClickableComponent>(activeComponents);

            // Sincroniza qualquer componente tardio adicionado por outros mods na página para a lista mestre
            if (
                Game1.activeClickableMenu is GameMenu gameMenu
                && gameMenu.allClickableComponents == activeComponents
            )
            {
                if (page.allClickableComponents != null)
                {
                    foreach (var c in page.allClickableComponents)
                    {
                        if (c != null && activeSet.Add(c))
                        {
                            activeComponents.Add(c);
                        }
                    }
                }
            }

            // 1. Coleta as setas de rolagem apenas quando o InventoryPage realmente tem scroll.
            // Se não há scroll, elas não podem ficar como componentes invisíveis na navegação.
            ViewportRegistry.TryGet(inventoryMenu, out var grid);
            bool showScrollButtons = grid != null
                && InventoryPageShouldShowScrollButtons(page, inventoryMenu, grid);
            if (grid != null)
            {
                if (showScrollButtons)
                {
                    if (grid.UpArrow != null && activeSet.Add(grid.UpArrow))
                        activeComponents.Add(grid.UpArrow);
                    if (grid.DownArrow != null && activeSet.Add(grid.DownArrow))
                        activeComponents.Add(grid.DownArrow);
                }
                else
                {
                    activeComponents.Remove(grid.UpArrow);
                    activeComponents.Remove(grid.DownArrow);
                    activeSet.Remove(grid.UpArrow);
                    activeSet.Remove(grid.DownArrow);
                }
            }

            // 2. Coleta TODOS os botões que estão à direita do inventário (Lixeira, Organizar, Setas e Mods)
            int maxRight = int.MinValue;
            int inventoryBottom = int.MinValue;
            foreach (var slot in slots)
            {
                if (slot.bounds.Right > maxRight)
                    maxRight = slot.bounds.Right;
                if (slot.bounds.Bottom > inventoryBottom)
                    inventoryBottom = slot.bounds.Bottom;
            }
            int rightEdge = maxRight - 16;

            var rightColumn = new List<ClickableComponent>();
            var bottomComponents = new List<ClickableComponent>();

            // Keep arrows in the side column only when they are visible/clickable.
            if (grid != null && showScrollButtons)
            {
                if (grid.UpArrow != null)
                    rightColumn.Add(grid.UpArrow);
                if (grid.DownArrow != null)
                    rightColumn.Add(grid.DownArrow);
            }
            if (page.organizeButton != null)
                rightColumn.Add(page.organizeButton);

            var rightColumnSet = new HashSet<ClickableComponent>(rightColumn);

            // Now categorize the rest of activeComponents
            foreach (var c in activeComponents)
            {
                if (c == null || GridViewportLayoutHelpers.IsProtectedComponent(c) || rightColumnSet.Contains(c))
                    continue;
                if (c.name == "charPortrait" || c == page.portrait)
                    continue; // Portrait is not focusable, bypass it

                // If this is the trash can, force it into rightColumn
                if (c == page.trashCan)
                {
                    rightColumn.Add(c);
                    rightColumnSet.Add(c);
                    continue;
                }

                // If this is a component we stack, it belongs to the rightColumn
                if (grid != null && grid.OriginalButtonBounds.ContainsKey(c))
                {
                    rightColumn.Add(c);
                    rightColumnSet.Add(c);
                    continue;
                }

                if (c.bounds.Top >= inventoryBottom - 16)
                {
                    bottomComponents.Add(c);
                }
                else if (
                    c.bounds.Center.X > rightEdge
                    && c.bounds.Center.X < rightEdge + 300
                    && c.bounds.Center.Y >= slots[0].bounds.Y - 16
                )
                {
                    rightColumn.Add(c);
                    rightColumnSet.Add(c);
                }
            }

            rightColumn = rightColumn.Distinct().ToList();

            // 3. Atribuir IDs válidos aos botões de mods (que normalmente usam -1).
            int dynamicId = 150000;
            foreach (var comp in rightColumn)
            {
                if (!GridViewportLayoutHelpers.IsProtectedComponent(comp) && comp.myID == -1)
                    comp.myID = dynamicId++;
            }
            foreach (var comp in bottomComponents)
            {
                if (!GridViewportLayoutHelpers.IsProtectedComponent(comp) && comp.myID == -1)
                    comp.myID = dynamicId++;
            }

            // Encontra o upNeighborID original que aponta para um componente acima do inventário (abas do menu)
            int originalUpNeighbor = -1;
            foreach (var c in rightColumn)
            {
                if (c.upNeighborID != -1)
                {
                    var target = activeComponents.FirstOrDefault(tc =>
                        tc != null && !GridViewportLayoutHelpers.IsProtectedComponent(tc) && tc.myID == c.upNeighborID
                    );
                    if (target != null && target.bounds.Center.Y < slots[0].bounds.Y - 16)
                    {
                        originalUpNeighbor = c.upNeighborID;
                        break;
                    }
                }
            }
            if (originalUpNeighbor == -1 && page.organizeButton != null)
            {
                originalUpNeighbor = page.organizeButton.upNeighborID;
            }
            int upNeighbor = -1;
            if (
                originalUpNeighbor != -1
                && activeComponents.Any(c => c != null && !GridViewportLayoutHelpers.IsProtectedComponent(c) && c.myID == originalUpNeighbor)
            )
            {
                upNeighbor = originalUpNeighbor;
            }
            else if (
                page.upperRightCloseButton != null
                && activeSet.Contains(page.upperRightCloseButton)
            )
            {
                upNeighbor = page.upperRightCloseButton.myID;
            }
            else
            {
                // Busca dinamicamente qualquer componente que esteja acima do inventário como aba fallback
                var firstPresentTab = activeComponents.FirstOrDefault(c =>
                    c != null
                    && !GridViewportLayoutHelpers.IsProtectedComponent(c)
                    && c.bounds.Center.Y < slots[0].bounds.Y - 16
                    && c != page.upperRightCloseButton
                );
                upNeighbor = firstPresentTab != null ? firstPresentTab.myID : 12340;
            }

            // Pre-calcula os slots mais à direita da grade do inventário
            var rightmostSlots = new List<ClickableComponent>();
            for (int idx = InventoryGridMetrics.DefaultColumnCount - 1; idx < slots.Count; idx += InventoryGridMetrics.DefaultColumnCount)
            {
                rightmostSlots.Add(slots[idx]);
            }

            // Pre-calcula os slots da última linha do inventário
            int visibleRows = slots.Count / InventoryGridMetrics.DefaultColumnCount;
            int lastRowStart = (visibleRows - 1) * InventoryGridMetrics.DefaultColumnCount;
            var lastRowSlots = new List<ClickableComponent>();
            for (int col = 0; col < InventoryGridMetrics.DefaultColumnCount; col++)
            {
                int idx = lastRowStart + col;
                if (idx < slots.Count)
                {
                    lastRowSlots.Add(slots[idx]);
                }
            }

            // 4. Mapeamento lateral do InventoryPage respeitando múltiplas colunas.
            // A organização visual agora pode criar uma segunda coluna; então o gamepad
            // também precisa enxergar colunas reais: cima/baixo dentro da mesma coluna,
            // direita/esquerda entre colunas, e slots -> primeira coluna lateral.
            var rightColumns = GroupNavigationColumns(rightColumn, 32);
            if (rightColumns.Count > 0)
            {
                // Logical side rows exclude scroll arrows and the trash can.
                // Vertical navigation may still pass through arrows/trash, but horizontal
                // row matching must only count the real side buttons.
                var nonArrowColumns = rightColumns
                    .Select(column => column
                        .Where(c => !IsInventoryPageScrollArrow(grid, c) && c != page.trashCan)
                        .ToList())
                    .ToList();

                var upArrow = grid?.UpArrow;
                var downArrow = grid?.DownArrow;

                for (int colIndex = 0; colIndex < rightColumns.Count; colIndex++)
                {
                    var column = rightColumns[colIndex];
                    var logicalColumn = nonArrowColumns[colIndex];

                    for (int i = 0; i < column.Count; i++)
                    {
                        var comp = column[i];

                        if (upArrow != null && ReferenceEquals(comp, upArrow))
                        {
                            // InventoryPage: vertical movement stays inside the side column, but
                            // horizontal movement is resolved by geometry. This makes the top
                            // right visible slot (normally slot 11) reach the up-arrow without
                            // hardcoding slot IDs.
                            GridViewportLayoutHelpers.SetUpNeighbor(comp, -1);
                            var firstLogical = logicalColumn.FirstOrDefault();
                            GridViewportLayoutHelpers.SetDownNeighbor(comp, firstLogical != null ? firstLogical.myID : -1);
                            var closestSlot = FindClosestByGeometry(
                                rightmostSlots,
                                comp.bounds.Center.X,
                                comp.bounds.Center.Y,
                                requireLeftOfSource: true
                            );
                            GridViewportLayoutHelpers.SetLeftNeighbor(comp, closestSlot != null ? closestSlot.myID : -1);
                            GridViewportLayoutHelpers.SetRightNeighbor(comp, -1);
                            if (closestSlot != null)
                                GridViewportLayoutHelpers.SetRightNeighbor(closestSlot, comp.myID);
                            continue;
                        }

                        if (downArrow != null && ReferenceEquals(comp, downArrow))
                        {
                            var lastLogical = logicalColumn.LastOrDefault();
                            GridViewportLayoutHelpers.SetUpNeighbor(comp, lastLogical != null ? lastLogical.myID : -1);
                            // InventoryPage: pressing DOWN on the down-arrow must reach the trash can.
                            // The arrow's own action still scrolls when pressing A; this is only directional navigation.
                            GridViewportLayoutHelpers.SetDownNeighbor(comp, page.trashCan != null ? page.trashCan.myID : -1);
                            var closestSlot = FindClosestByGeometry(
                                rightmostSlots,
                                comp.bounds.Center.X,
                                comp.bounds.Center.Y,
                                requireLeftOfSource: true
                            );
                            GridViewportLayoutHelpers.SetLeftNeighbor(comp, closestSlot != null ? closestSlot.myID : -1);
                            GridViewportLayoutHelpers.SetRightNeighbor(comp, -1);
                            if (closestSlot != null)
                                GridViewportLayoutHelpers.SetRightNeighbor(closestSlot, comp.myID);
                            if (page.trashCan != null)
                                GridViewportLayoutHelpers.SetUpNeighbor(page.trashCan, comp.myID);
                            continue;
                        }

                        int logicalIndex = logicalColumn.IndexOf(comp);

                        if (logicalIndex >= 0)
                        {
                            if (logicalIndex > 0)
                                GridViewportLayoutHelpers.SetUpNeighbor(comp, logicalColumn[logicalIndex - 1].myID);
                            else if (upArrow != null && column.Contains(upArrow))
                                GridViewportLayoutHelpers.SetUpNeighbor(comp, upArrow.myID);
                            else
                                GridViewportLayoutHelpers.SetUpNeighbor(comp, -1);

                            if (logicalIndex < logicalColumn.Count - 1)
                                GridViewportLayoutHelpers.SetDownNeighbor(comp, logicalColumn[logicalIndex + 1].myID);
                            else if (downArrow != null && showScrollButtons && column.Contains(downArrow))
                                GridViewportLayoutHelpers.SetDownNeighbor(comp, downArrow.myID);
                            else if (page.trashCan != null)
                                GridViewportLayoutHelpers.SetDownNeighbor(comp, page.trashCan.myID);
                            else
                                GridViewportLayoutHelpers.SetDownNeighbor(comp, -1);
                        }
                        else
                        {
                            GridViewportLayoutHelpers.SetUpNeighbor(comp, i > 0 ? column[i - 1].myID : -1);
                            GridViewportLayoutHelpers.SetDownNeighbor(comp, i < column.Count - 1 ? column[i + 1].myID : -1);
                        }

                        if (colIndex > 0)
                        {
                            var leftButton = PickSameLogicalRow(nonArrowColumns[colIndex - 1], logicalIndex, comp.bounds.Center.Y);
                            if (leftButton != null)
                                GridViewportLayoutHelpers.SetLeftNeighbor(comp, leftButton.myID);
                        }
                        else
                        {
                            var closestSlot = FindClosestByGeometry(
                                rightmostSlots,
                                comp.bounds.Center.X,
                                comp.bounds.Center.Y,
                                requireLeftOfSource: true
                            );
                            if (closestSlot != null)
                                GridViewportLayoutHelpers.SetLeftNeighbor(comp, closestSlot.myID);
                        }

                        if (colIndex < rightColumns.Count - 1)
                        {
                            var rightButton = PickSameLogicalRow(nonArrowColumns[colIndex + 1], logicalIndex, comp.bounds.Center.Y);
                            if (rightButton != null)
                                GridViewportLayoutHelpers.SetRightNeighbor(comp, rightButton.myID);
                        }
                    }
                }

                var primaryRightColumn = rightColumns[0]
                    .Where(c => c != page.trashCan)
                    .ToList();
                if (primaryRightColumn.Count == 0)
                {
                    primaryRightColumn = nonArrowColumns[0].Count > 0
                        ? nonArrowColumns[0]
                        : rightColumns[0];
                }

                foreach (var slot in rightmostSlots)
                {
                    var closestRightComp = FindClosestByGeometry(
                        primaryRightColumn,
                        slot.bounds.Center.X,
                        slot.bounds.Center.Y,
                        requireRightOfSource: true
                    );
                    if (closestRightComp != null)
                        GridViewportLayoutHelpers.SetRightNeighbor(slot, closestRightComp.myID);
                }

                LayoutDiagnostics.DebugChanged(
                    $"InventoryPageNav:arrow-links:{inventoryMenu.GetHashCode()}",
                    $"[FIV/InventoryPageNav] geometry links: up={DescribeComponent(upArrow)}, up.left={DescribeComponent(FindComponentById(activeComponents, upArrow?.leftNeighborID ?? -1))}, down={DescribeComponent(downArrow)}, down.left={DescribeComponent(FindComponentById(activeComponents, downArrow?.leftNeighborID ?? -1))}, rightEdgeSlots={rightmostSlots.Count}, primaryRight={string.Join(",", primaryRightColumn.Select(c => c.myID))}"
                );
            }

            // 5. Corrigir navegação vertical entre as linhas do inventário e os componentes de baixo
            foreach (var slot in lastRowSlots)
            {
                ClickableComponent? bestDown = null;
                int minXDiff = int.MaxValue;
                int minY = int.MaxValue;
                foreach (var c in bottomComponents)
                {
                    int xDiff = Math.Abs(c.bounds.Center.X - slot.bounds.Center.X);
                    if (xDiff < minXDiff || (xDiff == minXDiff && c.bounds.Y < minY))
                    {
                        minXDiff = xDiff;
                        minY = c.bounds.Y;
                        bestDown = c;
                    }
                }
                if (bestDown != null)
                {
                    GridViewportLayoutHelpers.SetDownNeighbor(slot, bestDown.myID);
                }
            }

            foreach (var c in bottomComponents)
            {
                ClickableComponent? bestUpComp = null;
                int minUpDist = int.MaxValue;
                ClickableComponent? bestDownComp = null;
                int minDownDist = int.MaxValue;

                ClickableComponent? bestLeftComp = null;
                int minLeftDist = int.MaxValue;
                ClickableComponent? bestRightComp = null;
                int minRightDist = int.MaxValue;

                foreach (var c2 in bottomComponents)
                {
                    if (c2 == c)
                        continue;

                    int xDiff = c2.bounds.Center.X - c.bounds.Center.X;
                    int yDiff = c2.bounds.Center.Y - c.bounds.Center.Y;
                    int dist = (int)Math.Sqrt(xDiff * xDiff + yDiff * yDiff);

                    bool isSameColumn = Math.Abs(xDiff) < 32;

                    if (isSameColumn)
                    {
                        if (yDiff < 0)
                        {
                            if (-yDiff < minUpDist)
                            {
                                minUpDist = -yDiff;
                                bestUpComp = c2;
                            }
                        }
                        else if (yDiff > 0)
                        {
                            if (yDiff < minDownDist)
                            {
                                minDownDist = yDiff;
                                bestDownComp = c2;
                            }
                        }
                    }

                    if (xDiff < -16)
                    {
                        if (dist < minLeftDist)
                        {
                            minLeftDist = dist;
                            bestLeftComp = c2;
                        }
                    }
                    else if (xDiff > 16)
                    {
                        if (dist < minRightDist)
                        {
                            minRightDist = dist;
                            bestRightComp = c2;
                        }
                    }
                }

                if (bestUpComp != null)
                {
                    GridViewportLayoutHelpers.SetUpNeighbor(c, bestUpComp.myID);
                }
                else
                {
                    ClickableComponent? closestSlot = null;
                    int minXDiff = int.MaxValue;
                    foreach (var slot in lastRowSlots)
                    {
                        int xDiff = Math.Abs(slot.bounds.Center.X - c.bounds.Center.X);
                        if (xDiff < minXDiff)
                        {
                            minXDiff = xDiff;
                            closestSlot = slot;
                        }
                    }
                    if (closestSlot != null)
                    {
                        GridViewportLayoutHelpers.SetUpNeighbor(c, closestSlot.myID);
                    }
                }

                if (bestDownComp != null)
                {
                    GridViewportLayoutHelpers.SetDownNeighbor(c, bestDownComp.myID);
                }

                if (bestLeftComp != null)
                {
                    GridViewportLayoutHelpers.SetLeftNeighbor(c, bestLeftComp.myID);
                }
                else
                {
                    ClickableComponent? closestSlot = null;
                    int minXDiff = int.MaxValue;
                    foreach (var slot in lastRowSlots)
                    {
                        int xDiff = Math.Abs(slot.bounds.Center.X - c.bounds.Center.X);
                        if (xDiff < minXDiff)
                        {
                            minXDiff = xDiff;
                            closestSlot = slot;
                        }
                    }
                    if (closestSlot != null)
                    {
                        GridViewportLayoutHelpers.SetLeftNeighbor(c, closestSlot.myID);
                    }
                }

                if (bestRightComp != null)
                {
                    GridViewportLayoutHelpers.SetRightNeighbor(c, bestRightComp.myID);
                }
            }
        }

        private static List<List<ClickableComponent>> GroupNavigationColumns(
            IEnumerable<ClickableComponent> components,
            int tolerance
        )
        {
            var columns = new List<List<ClickableComponent>>();
            foreach (var component in components
                .Where(c => c != null)
                .Distinct()
                .OrderBy(c => c.bounds.Center.X)
                .ThenBy(c => c.bounds.Center.Y))
            {
                var column = columns.FirstOrDefault(c =>
                    Math.Abs(c[0].bounds.Center.X - component.bounds.Center.X) <= tolerance
                );
                if (column == null)
                {
                    column = new List<ClickableComponent>();
                    columns.Add(column);
                }
                column.Add(component);
            }

            foreach (var column in columns)
                column.Sort((a, b) => a.bounds.Center.Y.CompareTo(b.bounds.Center.Y));

            return columns;
        }

        private static bool IsInventoryPageScrollArrow(GridViewport? grid, ClickableComponent component)
        {
            return grid != null && (component == grid.UpArrow || component == grid.DownArrow);
        }

        private static ClickableComponent? FindComponentById(
            IEnumerable<ClickableComponent> components,
            int id
        )
        {
            if (id == -1 || GridViewportLayoutHelpers.IsProtectedComponentId(id))
                return null;

            return components.FirstOrDefault(c => c != null && !GridViewportLayoutHelpers.IsProtectedComponent(c) && c.myID == id);
        }

        private static ClickableComponent? FindClosestByGeometry(
            IEnumerable<ClickableComponent> components,
            int sourceCenterX,
            int sourceCenterY,
            bool requireLeftOfSource = false,
            bool requireRightOfSource = false
        )
        {
            ClickableComponent? best = null;
            long bestScore = long.MaxValue;

            foreach (var component in components)
            {
                if (component == null)
                    continue;

                int centerX = component.bounds.Center.X;
                int centerY = component.bounds.Center.Y;
                if (requireLeftOfSource && centerX >= sourceCenterX)
                    continue;
                if (requireRightOfSource && centerX <= sourceCenterX)
                    continue;

                int dx = Math.Abs(centerX - sourceCenterX);
                int dy = Math.Abs(centerY - sourceCenterY);
                long score = ((long)dy * dy * 4L) + ((long)dx * dx);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = component;
                }
            }

            return best;
        }

        private static ClickableComponent? PickSameLogicalRow(
            List<ClickableComponent> column,
            int logicalIndex,
            int fallbackY
        )
        {
            if (column.Count == 0)
                return null;

            if (logicalIndex >= 0 && logicalIndex < column.Count)
                return column[logicalIndex];

            if (logicalIndex >= column.Count)
                return column[column.Count - 1];

            return FindClosestByY(column, fallbackY);
        }

        private static ClickableComponent? FindClosestByY(
            IEnumerable<ClickableComponent> components,
            int y
        )
        {
            ClickableComponent? best = null;
            int bestDistance = int.MaxValue;
            foreach (var component in components)
            {
                if (component == null)
                    continue;

                int distance = Math.Abs(component.bounds.Center.Y - y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = component;
                }
            }
            return best;
        }
    

        // ---- Side-button layout routing ----
        private static bool drawCurrencyPrefix(ShopMenu __instance, SpriteBatch b)
        {
            return InventoryMenuLayoutHelpers.DrawCurrencyPrefix(
                __instance,
                b,
                GetExtraHeight,
                GetExtraRow
            );
        }

        public static IEnumerable<CodeInstruction> drawTranspiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            return InventoryMenuLayoutHelpers.InjectExtraHeightIntoShopDraw(instructions);
        }

        public static IEnumerable<CodeInstruction> receiveLeftClickTranspiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            return InventoryMenuLayoutHelpers.InjectExtraHeightIntoShopClick(instructions);
        }

        private static void initializeShippingBinPostfix(ItemGrabMenu __instance)
        {
            if (__instance.lastShippedHolder is not null)
            {
                __instance.lastShippedHolder.bounds.Y -= GetExtraHeight() / 2;
            }
        }

        // Called after ItemGrabMenu.RepositionSideButtons() and ItemGrabMenu.gameWindowSizeChanged().
        // Both reset button positions to vanilla coordinates without calling
        // populateClickableComponentList, so our layout cache is never invalidated and the
        // buttons stay at vanilla positions until the next frame where the pending-passes update
        // fires. This postfix re-applies the cached layout immediately so there is no visible
        // flash of vanilla positions.
        private static void ItemGrabMenuRepositionSideButtonsPostfix(ItemGrabMenu __instance)
        {
            if (!IsSideLayoutMenu(__instance))
                return;

            var state = GetChestLayoutState(__instance);
            state.PendingBottomProbePasses = 0;
            state.PendingLayoutPasses = 0;

            // ItemGrabMenu.RepositionSideButtons can run while vanilla/Chests Anywhere are still
            // building a new menu. At that point allClickableComponents may be empty or contain only
            // helper controls, so laying out side buttons here creates the visible open/rebuild
            // flicker and fights with the later populateClickableComponentList pass. Wait for the
            // populated menu pass instead.
            if (!IsItemGrabMenuClickableListReady(__instance))
            {
                Log.Debug($"[FIV/SideLayout] skip RepositionSideButtons: menu={__instance.GetType().Name}#{__instance.GetHashCode()}, components={__instance.allClickableComponents?.Count ?? 0}, not-populated-yet");
                return;
            }

            RepositionAndWireSideButtons(__instance, "RepositionSideButtons", force: true);
        }

        private static bool IsItemGrabMenuClickableListReady(IClickableMenu menu)
        {
            int componentCount = menu.allClickableComponents?.Count ?? 0;
            if (componentCount == 0)
                return false;

            var inventoryMenus = MenuComponentFinder.FindInventoryMenus(menu)
                .Where(m => m?.inventory != null && m.inventory.Count > 0)
                .ToList();
            int expectedSlots = inventoryMenus.Sum(m => m.inventory?.Count ?? 0);
            if (expectedSlots <= 0)
                return componentCount >= 16;

            // The populated ItemGrabMenu list normally contains the chest/player slots plus side
            // controls. During early RepositionSideButtons it can be 0-2; that is not safe to mutate.
            int minimumReadyComponents = Math.Max(16, expectedSlots / 2);
            return componentCount >= minimumReadyComponents;
        }

        private static void IClickableMenuUpdatePositionPostfix(IClickableMenu __instance)
        {
            if (IsSideLayoutMenu(__instance))
            {
                ScheduleChestLayout(__instance, "updatePosition", 1);
                RepositionAndWireSideButtons(__instance, "updatePosition", force: true);
            }
        }

        private static void RepositionAndWireSideButtons(
            IClickableMenu menu,
            string reason = "manual",
            bool force = false
        )
        {
            if (menu == null || !IsSideLayoutMenu(menu))
                return;

            var state = GetChestLayoutState(menu);
            if (state.IsInsideLayout)
                return;

            state.IsInsideLayout = true;
            try
            {
                RepositionAndWireSideButtonsCore(menu, state, reason, force);
            }
            finally
            {
                state.IsInsideLayout = false;
            }
        }

        private static void RepositionAndWireSideButtonsCore(
            IClickableMenu menu,
            ChestMenuLayoutState state,
            string reason,
            bool force
        )
        {
            LayoutDiagnostics.DebugChanged($"SideLayout:entry:{menu.GetHashCode()}:{reason}", $"[FIV/SideLayout] entry: menu={menu.GetType().FullName}, reason={reason}, force={force}, components={menu.allClickableComponents?.Count ?? 0}");

            if (menu is ShopMenu shopMenu)
            {
                InventoryMenuLayoutHelpers.AdjustShopMenuScrollLayout(
                    shopMenu,
                    StardewMenuFields.ShopMenuScrollBarRunner
                );
            }

            var menus = MenuComponentFinder.FindInventoryMenus(menu);
            LayoutDiagnostics.DebugChanged($"SideLayout:menus:{menu.GetType().FullName}", $"[FIV/SideLayout] found inventory menus: count={menus.Count}");
            if (menus.Count == 0)
                return;
            var orderedMenus = menus
                .Where(m => m?.inventory != null && m.inventory.Count > 0)
                .OrderBy(m => m.yPositionOnScreen)
                .ToList();
            if (orderedMenus.Count == 0)
                return;

            var chestMenu = orderedMenus.First();
            state.InventoryMenus = orderedMenus;

            var playerMenu = orderedMenus.Last();
            var chestSlots = chestMenu.inventory;
            var playerSlots = playerMenu.inventory;
            if (
                chestSlots == null
                || chestSlots.Count == 0
                || playerSlots == null
                || playerSlots.Count == 0
            )
            {
                LayoutDiagnostics.DebugChanged($"SideLayout:skip:slots:{menu.GetType().FullName}", "[FIV/SideLayout] skip: missing chest/player slots");
                return;
            }

            LayoutDiagnostics.DebugChanged($"SideLayout:slots:{menu.GetType().FullName}", $"[FIV/SideLayout] slots: chestCount={chestSlots.Count}, chestRows={chestMenu.rows}, chestCapacity={chestMenu.capacity}, chestTop=({chestSlots[0].bounds.X},{chestSlots[0].bounds.Y}); playerCount={playerSlots.Count}, playerRows={playerMenu.rows}, playerCapacity={playerMenu.capacity}, playerTop=({playerSlots[0].bounds.X},{playerSlots[0].bounds.Y})");

            int chestColumns = chestMenu.capacity / chestMenu.rows;
            if (chestColumns <= 0)
                chestColumns = InventoryGridMetrics.DefaultColumnCount;
            int playerColumns = playerMenu.capacity / playerMenu.rows;
            if (playerColumns <= 0)
                playerColumns = InventoryGridMetrics.DefaultColumnCount;

            var playerGrid = ViewportRegistry.GetOrCreate(playerMenu);
            playerGrid.CustomArrowLayout = true;
            playerGrid.UpArrow.myID = ArrowIdUp;
            playerGrid.DownArrow.myID = ArrowIdDown;
            RestoreChestPlayerScroll(state, playerGrid, playerMenu, reason);
            IList<Item>? playerSource = RefreshInventorySource(playerMenu, playerGrid);
            bool isPlayerSource = playerSource != null && ReferenceEquals(playerSource, Game1.player?.Items);
            int effectivePlayerSlots = InventoryGridMetrics.GetEffectiveSlotCount(playerSource, isPlayerSource);
            bool showScrollButtons = effectivePlayerSlots > playerMenu.capacity;
            RememberChestPlayerScroll(state, playerGrid, playerMenu, playerSource);
            LayoutDiagnostics.DebugChanged($"SideLayout:scroll-decision:{menu.GetHashCode()}", $"[FIV/SideLayout] player grid scroll decision: menu={menu.GetType().Name}, fullInventory={playerSource?.Count ?? -1}, effectiveSlots={effectivePlayerSlots}, playerCapacity={playerMenu.capacity}, showScroll={showScrollButtons}, scrollRow={playerGrid.ScrollRow}");
            playerGrid.SetScrollButtonsClickable(menu, showScrollButtons);

            // ItemGrabMenu side buttons must be calculated from the live menu every pass.
            // Cached target replay is unsafe here because vanilla/mod rebuilds reuse anonymous
            // ClickableComponent shapes and Chests Anywhere recreates external overlay buttons.
            bool useChestSideTargetCache = false;

            if (useChestSideTargetCache && !playerGrid.HasCachedSideButtonTargets && state.SideButtonTargets.Count > 0)
            {
                playerGrid.ImportSideButtonTargets(state.SideButtonTargets);
                LayoutDiagnostics.DebugChanged(
                    $"SideLayoutCache:import:{menu.GetHashCode()}",
                    $"[FIV/SideLayoutCache] imported parent cache into player grid: menu={menu.GetType().Name}, targetKeys={state.SideButtonTargets.Count}, playerMenuHash={playerMenu.GetHashCode()}"
                );
            }

            var fields = BuildMenuCachedFields(menu);
            Rectangle? originalOkBoundsBeforeCache = fields.OkBtn?.bounds;
            Rectangle? originalTrashBoundsBeforeCache = fields.TrashBtn?.bounds;
            Log.Debug($"[FIV/ChestLowerTrace] live-before-cache: reason={reason}, menu={menu.GetType().Name}#{menu.GetHashCode()}, showScroll={showScrollButtons}, ok={DescribeComponent(fields.OkBtn)}, trash={DescribeComponent(fields.TrashBtn)}, playerTop={playerSlots.Min(slot => slot.bounds.Top)}, playerBottom={playerSlots.Max(slot => slot.bounds.Bottom)}, chestBottom={chestSlots.Max(slot => slot.bounds.Bottom)}");
            Func<ClickableComponent, bool> chestCachePredicate = button => ShouldApplyChestCachedTarget(button, fields.OkBtn, fields.TrashBtn, playerGrid, playerSlots);
            if (useChestSideTargetCache && playerGrid.HasCachedSideButtonTargets)
            {
                bool earlyApplied = SideButtonLayoutEngine.ApplyCachedTargets(playerGrid, menu, $"prehash:{reason}", chestCachePredicate);
                if (earlyApplied)
                {
                    RestoreLowerVanillaButtonPosition(fields.OkBtn, originalOkBoundsBeforeCache, showScrollButtons);
                    RestoreLowerVanillaButtonPosition(fields.TrashBtn, originalTrashBoundsBeforeCache, showScrollButtons);
                    fields = BuildMenuCachedFields(menu);
                    Log.Debug($"[FIV/ChestLowerTrace] after-prehash-cache: reason={reason}, ok={DescribeComponent(fields.OkBtn)}, trash={DescribeComponent(fields.TrashBtn)}");
                }
            }

            var colorBtn = fields.ColorBtn;
            var fillBtn = fields.FillBtn;
            var organizeBtn = fields.OrganizeBtn;
            var specialBtn = fields.SpecialBtn;
            var okBtn = fields.OkBtn;
            var trashBtn = fields.TrashBtn;
            var pickerToggle = fields.PickerToggle;
            var colorPicker = fields.ColorPicker;
            LayoutDiagnostics.DebugChanged($"SideLayout:fields:{menu.GetType().FullName}", $"[FIV/SideLayout] fields: color={DescribeComponent(colorBtn)}, fill={DescribeComponent(fillBtn)}, organize={DescribeComponent(organizeBtn)}, special={DescribeComponent(specialBtn)}, ok={DescribeComponent(okBtn)}, trash={DescribeComponent(trashBtn)}, pickerToggle={DescribeComponent(pickerToggle)}");
            var preferredSide = InventoryMenuLayoutHelpers.GetPreferredSide(menu);
            int preferredSideOffsetPixels =
                InventoryMenuLayoutHelpers.GetPreferredSideOffsetPixels(menu);
            int? arrowAnchorCenterXOverride = null;
            var arrowAnchorComponentOverride =
                InventoryMenuLayoutHelpers.GetArrowAnchorComponentOverride(menu);
            int anchorCenterX =
                (okBtn ?? trashBtn ?? organizeBtn ?? fillBtn ?? colorBtn ?? specialBtn)?.bounds.Center.X ?? 0;
            var extraLayoutComponents = FindExternalSideLayoutComponents(menu, chestSlots, playerSlots, fields);

            var layoutContext = new GridViewport.SideButtonLayoutContext
            {
                Menu = menu,
                ChestSlots = chestSlots,
                PlayerSlots = playerSlots,
                ChestColumns = chestColumns,
                PlayerColumns = playerColumns,
                PreferredSide = preferredSide,
                PreferredSideOffsetPixels = preferredSideOffsetPixels,
                ArrowAnchorCenterXOverride = arrowAnchorCenterXOverride,
                ArrowAnchorComponentOverride = arrowAnchorComponentOverride,
                CenterOkBetweenArrows =
                    menu is MuseumMenu
                    || CurrentParentMenu is MuseumMenu
                    || IsCalledFromMuseum(),
                ColorButton = colorBtn,
                FillButton = fillBtn,
                OrganizeButton = organizeBtn,
                SpecialButton = specialBtn,
                OkButton = okBtn,
                TrashButton = trashBtn,
                OkButtonOriginalBounds = originalOkBoundsBeforeCache,
                TrashButtonOriginalBounds = originalTrashBoundsBeforeCache,
                PickerToggle = pickerToggle,
                ColorPicker = colorPicker,
                ShowScrollButtons = showScrollButtons,
                ExtraClickableComponents = extraLayoutComponents,
            };
            bool hasUncachedBottomButtons = SideButtonLayoutEngine.HasUncachedBottomCandidates(playerGrid, layoutContext);
            bool hasExternalLayoutComponents = extraLayoutComponents.Count > 0;
            int layoutHash = ComputeChestLayoutHash(
                anchorCenterX,
                new List<ClickableComponent>(),
                colorBtn,
                fillBtn,
                organizeBtn,
                specialBtn,
                okBtn,
                trashBtn,
                chestSlots,
                playerSlots,
                showScrollButtons
            );

            LayoutDiagnostics.DebugChanged($"SideLayout:computed:{menu.GetType().FullName}", $"[FIV/SideLayout] computed hash={layoutHash}, anchorCenterX={anchorCenterX}, preferredSide={preferredSide}, preferredOffset={preferredSideOffsetPixels}, showScroll={showScrollButtons}, uncachedBottomButtons={hasUncachedBottomButtons}, externalButtons={extraLayoutComponents.Count}");

            // Keep the vanilla drop-item target reachable even when the side-column layout
            // did not change this frame. Other mods and populateClickableComponentList can
            // rebuild neighbors after our last layout pass.
            MenuWithInventoryDropNavigation.Preserve(menu, orderedMenus);

            bool layoutChanged = UpdateCachedLayoutHash(state, menu, layoutHash);
            if (!layoutChanged)
            {
                bool applied = false;
                if (useChestSideTargetCache)
                    applied = SideButtonLayoutEngine.ApplyCachedTargets(playerGrid, menu, $"cache-hit:{reason}", chestCachePredicate);
                if (applied)
                {
                    RestoreLowerVanillaButtonPosition(okBtn, originalOkBoundsBeforeCache, showScrollButtons);
                    RestoreLowerVanillaButtonPosition(trashBtn, originalTrashBoundsBeforeCache, showScrollButtons);
                }

                // ItemGrabMenu's OK/trash/lower button cluster depends on the live player grid
                // height and on whether scroll arrows are visible. A cached top-button replay is
                // not enough after Chests Anywhere/color-picker/vanilla rebuilds; run the layout
                // pass again so OK/trash keep their original X but get the correct Y and navigation.
                bool needsLiveLowerLayout = menu is ItemGrabMenu && (okBtn != null || trashBtn != null || showScrollButtons);
                LayoutDiagnostics.DebugChanged($"SideLayout:cache-hit:{menu.GetHashCode()}", $"[FIV/SideLayout] cache hit: menu={menu.GetType().Name}, reason={reason}, appliedCachedTargets={applied}, force={force}, hash={layoutHash}, uncachedBottomButtons={hasUncachedBottomButtons}, externalButtons={extraLayoutComponents.Count}, needsLiveLowerLayout={needsLiveLowerLayout}");
                if (!needsLiveLowerLayout && !hasUncachedBottomButtons && !hasExternalLayoutComponents && (applied || !force))
                {
                    MenuWithInventoryDropNavigation.Preserve(menu, orderedMenus);
                    return;
                }
            }
            else
            {
                Log.Debug($"[FIV/SideLayout] cache miss: menu={menu.GetType().Name}, reason={reason}, relayout=true, hash={layoutHash}");
            }

            SideButtonLayoutEngine.Layout(playerGrid, layoutContext);
            Log.Debug($"[FIV/ChestLowerTrace] after-layout: reason={reason}, ok={DescribeComponent(okBtn)}, trash={DescribeComponent(trashBtn)}, up={DescribeComponent(playerGrid.UpArrow)}, down={DescribeComponent(playerGrid.DownArrow)}");
            state.SideButtonTargets = playerGrid.ExportSideButtonTargets();

            MenuWithInventoryDropNavigation.Preserve(menu, orderedMenus);
        }

        private static bool ShouldApplyChestCachedTarget(
            ClickableComponent button,
            ClickableComponent? okButton,
            ClickableComponent? trashButton,
            GridViewport playerGrid,
            List<ClickableComponent> playerSlots
        )
        {
            if (button == null || GridViewportLayoutHelpers.IsProtectedComponent(button))
                return false;
            if (ReferenceEquals(button, okButton) || ReferenceEquals(button, trashButton))
            {
                Log.Debug($"[FIV/SideLayoutCache] skip lower vanilla target: {DescribeComponent(button)}");
                return false;
            }
            if (ReferenceEquals(button, playerGrid.UpArrow) || ReferenceEquals(button, playerGrid.DownArrow))
                return false;
            if (string.Equals(button.name, "sort-inventory", StringComparison.OrdinalIgnoreCase))
            {
                Log.Debug($"[FIV/SideLayoutCache] skip external lower target: {DescribeComponent(button)}");
                return false;
            }

            int playerTop = playerSlots.Min(slot => slot.bounds.Top);
            // The cache is only for the upper chest action cluster. Lower controls are
            // recalculated from the live menu every pass, because OK/trash and overlay
            // buttons change when Chests Anywhere/color picker rebuilds the menu.
            if (button.bounds.Center.Y >= playerTop - 32)
            {
                Log.Debug($"[FIV/SideLayoutCache] skip lower-zone target: playerTop={playerTop}, button={DescribeComponent(button)}");
                return false;
            }

            return true;
        }

        private static void RestoreLowerVanillaButtonPosition(
            ClickableComponent? button,
            Rectangle? originalBounds,
            bool showScrollButtons
        )
        {
            if (button == null || !originalBounds.HasValue)
                return;

            // Cache replay is allowed to restore our custom top-button layout, but OK and
            // trash must keep the horizontal column chosen by the vanilla/current menu.
            // Do not restore Y here: the lower layout intentionally aligns OK with the last
            // visible player-inventory row, and stacks trash/custom lower buttons above it.
            GridViewportLayoutHelpers.SetBoundsX(button, originalBounds.Value.X);
        }

        private static MenuCachedFields BuildMenuCachedFields(IClickableMenu menu)
        {
            var cBtn = MenuComponentFinder.FindFieldContaining(
                menu,
                "colorPickerToggleButton"
            );
            var fBtn = MenuComponentFinder.FindFieldContaining(
                menu,
                "fillStacksButton"
            );
            var oBtn =
                MenuComponentFinder.FindFieldContaining(menu, "organizeButton")
                ?? MenuComponentFinder.FindFieldContaining(
                    menu,
                    "organizeStashButton"
                );
            var oK = MenuComponentFinder.FindFieldContaining(menu, "okButton");
            var sBtn = MenuComponentFinder.FindFieldContaining(menu, "specialButton");
            var jBtn = MenuComponentFinder.FindFieldContaining(menu, "junimoNoteIcon");
            var tBtn = MenuComponentFinder.FindFieldContaining(menu, "trashCan");

            if (
                cBtn != null
                && (cBtn == menu.upperRightCloseButton || cBtn.name == "upperRightCloseButton")
            )
                cBtn = null;
            if (
                fBtn != null
                && (fBtn == menu.upperRightCloseButton || fBtn.name == "upperRightCloseButton")
            )
                fBtn = null;
            if (
                oBtn != null
                && (oBtn == menu.upperRightCloseButton || oBtn.name == "upperRightCloseButton")
            )
                oBtn = null;
            if (
                oK != null
                && (oK == menu.upperRightCloseButton || oK.name == "upperRightCloseButton")
            )
                oK = null;
            if (
                sBtn != null
                && (sBtn == menu.upperRightCloseButton || sBtn.name == "upperRightCloseButton")
            )
                sBtn = null;
            if (
                tBtn != null
                && (tBtn == menu.upperRightCloseButton || tBtn.name == "upperRightCloseButton")
            )
                tBtn = null;
            if (
                jBtn != null
                && (jBtn == menu.upperRightCloseButton || jBtn.name == "upperRightCloseButton")
            )
                jBtn = null;
            if (sBtn == null)
                sBtn = jBtn;

            var cPicker = MenuComponentFinder.FindColorPicker(menu);
            var pToggle =
                cPicker != null
                    ? MenuComponentFinder.FindColorPickerToggleButton(cPicker)
                    : null;
            if (cBtn == null && pToggle != null)
                cBtn = pToggle;

            return new MenuCachedFields
            {
                ColorBtn = cBtn,
                FillBtn = fBtn,
                OrganizeBtn = oBtn,
                SpecialBtn = sBtn,
                OkBtn = oK,
                TrashBtn = tBtn,
                ColorPicker = cPicker,
                PickerToggle = pToggle,
            };
        }

        private static List<ClickableComponent> FindExternalSideLayoutComponents(
            IClickableMenu menu,
            List<ClickableComponent> chestSlots,
            List<ClickableComponent> playerSlots,
            MenuCachedFields fields
        )
        {
            var result = new List<ClickableComponent>();
            if (playerSlots.Count == 0)
                return result;

            var known = new HashSet<ClickableComponent>();
            if (menu.allClickableComponents != null)
            {
                foreach (var component in menu.allClickableComponents)
                {
                    if (component != null)
                        known.Add(component);
                }
            }

            AddKnown(known, fields.ColorBtn);
            AddKnown(known, fields.FillBtn);
            AddKnown(known, fields.OrganizeBtn);
            AddKnown(known, fields.SpecialBtn);
            AddKnown(known, fields.OkBtn);
            AddKnown(known, fields.TrashBtn);
            AddKnown(known, fields.PickerToggle);
            if (menu.upperRightCloseButton != null)
                known.Add(menu.upperRightCloseButton);
            foreach (var slot in chestSlots)
                known.Add(slot);
            foreach (var slot in playerSlots)
                known.Add(slot);

            foreach (var externalButton in ExternalSideButtonRegistry.Get(menu))
            {
                var button = externalButton.Button;
                if (button == null)
                    continue;

                known.Add(button);

                if (!externalButton.AllowLayout)
                {
                    Log.Debug($"[FIV/ChestSideLayout] external fixed-position button: source={externalButton.SourceId}, button={DescribeComponent(button)}, menu={menu.GetType().Name}#{menu.GetHashCode()}");
                    continue;
                }

                // Registered/API buttons are external layout inputs even when they already exist in
                // allClickableComponents. The previous known.Contains guard made the live
                // Chests Anywhere SortInventoryButton disappear from ExtraClickableComponents, then
                // the stale-prune step removed it as an orphan and the next pass recreated it again.
                if (!result.Contains(button))
                {
                    result.Add(button);
                    Log.Debug($"[FIV/ChestSideLayout] external registered button: source={externalButton.SourceId}, button={DescribeComponent(button)}, menu={menu.GetType().Name}#{menu.GetHashCode()}");
                }
            }

            var scanned = new List<ClickableComponent>();
            var visitedObjects = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var visitedComponents = new HashSet<ClickableComponent>();

            // First scan the active menu itself for fields that are not mirrored into
            // allClickableComponents yet. Some mods patch ItemGrabMenu and keep their own
            // component fields.
            CollectClickableComponents(menu, scanned, visitedObjects, visitedComponents, maxDepth: 3);

            // Then scan SMAPI/on-screen overlay menus. Chests Anywhere-style buttons can be
            // drawn and owned outside activeClickableMenu, so active allClickableComponents
            // never sees them even though they are visually in the chest menu column.
            foreach (var overlay in Game1.onScreenMenus)
            {
                if (overlay == null || ReferenceEquals(overlay, menu))
                    continue;
                CollectClickableComponents(overlay, scanned, visitedObjects, visitedComponents, maxDepth: 3);
            }

            Rectangle playerBounds = GetBoundsForComponents(playerSlots);
            Rectangle chestBounds = GetBoundsForComponents(chestSlots);
            int anchorCenterX =
                (fields.TrashBtn ?? fields.OkBtn ?? fields.OrganizeBtn ?? fields.FillBtn ?? fields.ColorBtn ?? fields.SpecialBtn)
                    ?.bounds.Center.X
                ?? playerBounds.Right + 60;

            foreach (var component in scanned)
            {
                if (component == null || known.Contains(component) || result.Contains(component))
                    continue;
                if (!IsExternalBottomCandidate(component, playerBounds, chestBounds, anchorCenterX))
                    continue;
                result.Add(component);
            }

            if (result.Count > 0 || scanned.Count > 0)
            {
                Log.Debug($"[FIV/ChestSideLayout] external scan: menu={menu.GetType().Name}#{menu.GetHashCode()}, scanned={scanned.Count}, candidates={DescribeComponentsForLog(result, 12)}");
            }

            return result;
        }

        private static bool IsExternalBottomCandidate(
            ClickableComponent component,
            Rectangle playerBounds,
            Rectangle chestBounds,
            int anchorCenterX
        )
        {
            if (component.bounds.Width <= 0 || component.bounds.Height <= 0)
                return false;
            if (component.bounds.Width < 16 || component.bounds.Height < 16)
                return false;
            if (component.bounds.Width > 220 || component.bounds.Height > 220)
                return false;
            if (component.name == "upperRightCloseButton")
                return false;

            int centerX = component.bounds.Center.X;
            int centerY = component.bounds.Center.Y;

            // It must belong to the player-inventory lower side area, not the chest top area.
            if (centerY < playerBounds.Top - 96)
                return false;
            if (centerY > playerBounds.Bottom + 160)
                return false;

            bool inRightBand = centerX >= playerBounds.Right - 96 && centerX <= playerBounds.Right + 460;
            bool inAnchorColumn = Math.Abs(centerX - anchorCenterX) <= 160;
            if (!inRightBand && !inAnchorColumn)
                return false;

            // Avoid stealing components that are clearly over the chest grid/top button area.
            if (centerY >= chestBounds.Top - 16 && centerY <= chestBounds.Bottom + 16)
                return false;

            return true;
        }

        private static Rectangle GetBoundsForComponents(List<ClickableComponent> components)
        {
            int left = components.Min(c => c.bounds.Left);
            int top = components.Min(c => c.bounds.Top);
            int right = components.Max(c => c.bounds.Right);
            int bottom = components.Max(c => c.bounds.Bottom);
            return new Rectangle(left, top, right - left, bottom - top);
        }

        private static void AddKnown(HashSet<ClickableComponent> known, ClickableComponent? component)
        {
            if (component != null)
                known.Add(component);
        }

        private static void CollectClickableComponents(
            object? value,
            List<ClickableComponent> output,
            HashSet<object> visitedObjects,
            HashSet<ClickableComponent> visitedComponents,
            int maxDepth
        )
        {
            if (value == null || maxDepth < 0)
                return;

            if (value is ClickableComponent component)
            {
                if (visitedComponents.Add(component))
                    output.Add(component);
                return;
            }

            if (value is string || value.GetType().IsPrimitive || value.GetType().IsEnum)
                return;

            if (!visitedObjects.Add(value))
                return;

            if (value is IEnumerable enumerable && value is not IDictionary)
            {
                foreach (var item in enumerable)
                    CollectClickableComponents(item, output, visitedObjects, visitedComponents, maxDepth - 1);
            }

            Type? type = value.GetType();
            if (type != null && type.FullName?.Contains("SortInventoryButton", StringComparison.OrdinalIgnoreCase) == true)
            {
                LayoutDiagnostics.DebugChanged(
                    $"ExternalButtonContainer:SortInventoryButton:{RuntimeHelpers.GetHashCode(value)}",
                    $"[FIV/ChestSideLayout] inspecting external button container: type={type.FullName}, hash={RuntimeHelpers.GetHashCode(value)}, depth={maxDepth}"
                );
            }

            while (type != null && type != typeof(object))
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (ShouldSkipReflectionField(field))
                        continue;
                    try
                    {
                        var fieldValue = field.GetValue(value);
                        if (fieldValue is ClickableComponent || fieldValue is IEnumerable || ShouldDescendIntoLikelyButtonContainer(fieldValue))
                            CollectClickableComponents(fieldValue, output, visitedObjects, visitedComponents, maxDepth - 1);
                    }
                    catch { }
                }

                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (property.GetIndexParameters().Length != 0 || !property.CanRead)
                        continue;
                    bool propertyMayContainClickable = typeof(ClickableComponent).IsAssignableFrom(property.PropertyType)
                        || typeof(IEnumerable).IsAssignableFrom(property.PropertyType)
                        || LooksLikeButtonContainerType(property.PropertyType);
                    if (!propertyMayContainClickable)
                        continue;

                    try
                    {
                        var propertyValue = property.GetValue(value);
                        if (propertyValue is ClickableComponent || propertyValue is IEnumerable || ShouldDescendIntoLikelyButtonContainer(propertyValue))
                            CollectClickableComponents(propertyValue, output, visitedObjects, visitedComponents, maxDepth - 1);
                    }
                    catch { }
                }

                type = type.BaseType;
            }
        }


        private static bool ShouldDescendIntoLikelyButtonContainer(object? value)
        {
            if (value == null)
                return false;
            if (value is string || value.GetType().IsPrimitive || value.GetType().IsEnum)
                return false;
            return LooksLikeButtonContainerType(value.GetType());
        }

        private static bool LooksLikeButtonContainerType(Type type)
        {
            string name = type.FullName ?? type.Name;
            return name.Contains("SortInventoryButton", StringComparison.OrdinalIgnoreCase)
                || name.Contains("InventoryButton", StringComparison.OrdinalIgnoreCase)
                || name.Contains("ClickableButton", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("Button", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Button", StringComparison.OrdinalIgnoreCase) && name.Contains("Inventory", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipReflectionField(FieldInfo field)
        {
            if (field.IsStatic)
                return true;
            if (field.FieldType == typeof(string))
                return true;
            return false;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        private static bool UpdateCachedLayoutHash(ChestMenuLayoutState state, IClickableMenu menu, int hash)
        {
            if (state.LayoutHash == hash)
            {
                LayoutDiagnostics.DebugChanged($"SideLayoutHash:unchanged:{menu.GetHashCode()}", $"[FIV/SideLayoutHash] unchanged: menu={menu.GetType().Name}, hash={hash}");
                return false;
            }

            Log.Debug($"[FIV/SideLayoutHash] changed: menu={menu.GetType().Name}, old={state.LayoutHash}, new={hash}");
            state.LayoutHash = hash;
            return true;
        }

        private static void RestoreChestPlayerScroll(
            ChestMenuLayoutState state,
            GridViewport playerGrid,
            InventoryMenu playerMenu,
            string reason
        )
        {
            if (state.LastPlayerScrollRow <= 0 || playerGrid.ScrollRow != 0)
                return;

            playerGrid.ScrollRow = state.LastPlayerScrollRow;
            LayoutDiagnostics.DebugChanged(
                $"ChestScroll:restore:{playerMenu.GetHashCode()}",
                $"[FIV/ChestScroll] restored player scroll: reason={reason}, menuHash={playerMenu.GetHashCode()}, scrollRow={state.LastPlayerScrollRow}, previousPlayerMenuHash={state.LastPlayerMenuHash}"
            );
        }

        private static void RememberChestPlayerScroll(
            ChestMenuLayoutState state,
            GridViewport playerGrid,
            InventoryMenu playerMenu,
            IList<Item>? playerSource
        )
        {
            int maxScroll = playerSource != null ? playerGrid.GetMaxScrollRow(playerSource) : 0;
            state.LastPlayerScrollRow = Math.Clamp(playerGrid.ScrollRow, 0, maxScroll);
            state.LastPlayerMaxScrollRow = maxScroll;
            state.LastPlayerMenuHash = playerMenu.GetHashCode();
        }

        private static bool ApplyCachedChestLayout(IClickableMenu menu, string reason)
        {
            if (!IsSideLayoutMenu(menu))
                return false;

            var state = GetChestLayoutState(menu);
            if (state.SideButtonTargets.Count == 0)
                return false;

            var orderedMenus = MenuComponentFinder.FindInventoryMenus(menu)
                .Where(m => m?.inventory != null && m.inventory.Count > 0)
                .OrderBy(m => m.yPositionOnScreen)
                .ToList();
            if (orderedMenus.Count == 0)
                return false;

            var playerMenu = orderedMenus.Last();
            var playerGrid = ViewportRegistry.GetOrCreate(playerMenu);
            playerGrid.CustomArrowLayout = true;
            playerGrid.UpArrow.myID = ArrowIdUp;
            playerGrid.DownArrow.myID = ArrowIdDown;

            RestoreChestPlayerScroll(state, playerGrid, playerMenu, reason);
            IList<Item>? playerSource = RefreshInventorySource(playerMenu, playerGrid);
            bool showScrollButtons = playerSource != null
                && InventoryGridMetrics.GetEffectiveSlotCount(
                    playerSource,
                    ReferenceEquals(playerSource, Game1.player?.Items)
                ) > playerMenu.capacity;

            playerGrid.SetScrollButtonsClickable(menu, showScrollButtons);
            playerGrid.ImportSideButtonTargets(state.SideButtonTargets);

            var fields = BuildMenuCachedFields(menu);
            EnsureFieldSideButtonsInClickableComponents(menu, fields);

            Func<ClickableComponent, bool> cachePredicate = button => ShouldApplyChestCachedTarget(button, fields.OkBtn, fields.TrashBtn, playerGrid, playerMenu.inventory);
            bool applied = SideButtonLayoutEngine.ApplyCachedTargets(playerGrid, menu, reason, cachePredicate);
            if (fields.ColorPicker != null && fields.PickerToggle != null && fields.ColorBtn != null)
                GridViewportLayoutHelpers.SetBounds(fields.PickerToggle, fields.ColorBtn.bounds);

            MenuWithInventoryDropNavigation.Preserve(menu, orderedMenus);
            if (playerSource != null && ReferenceEquals(playerSource, Game1.player?.Items))
                RememberChestPlayerScroll(state, playerGrid, playerMenu, playerSource);

            return applied;
        }

        private static void EnsureFieldSideButtonsInClickableComponents(
            IClickableMenu menu,
            MenuCachedFields fields
        )
        {
            menu.allClickableComponents ??= new List<ClickableComponent>();
            foreach (var button in EnumerateFieldSideButtons(fields).Distinct())
            {
                if (button == null)
                    continue;
                if (button == menu.upperRightCloseButton || button.name == "upperRightCloseButton")
                    continue;
                if (!menu.allClickableComponents.Contains(button))
                    menu.allClickableComponents.Add(button);
            }
        }

        private static IEnumerable<ClickableComponent?> EnumerateFieldSideButtons(MenuCachedFields fields)
        {
            yield return fields.ColorBtn;
            yield return fields.FillBtn;
            yield return fields.OrganizeBtn;
            yield return fields.SpecialBtn;
            yield return fields.OkBtn;
            yield return fields.TrashBtn;
            yield return fields.PickerToggle;
        }

        private static int ComputeChestLayoutHash(
            int anchorCenterX,
            List<ClickableComponent> chestTopColumn,
            ClickableComponent? colorBtn,
            ClickableComponent? fillBtn,
            ClickableComponent? organizeBtn,
            ClickableComponent? specialBtn,
            ClickableComponent? okBtn,
            ClickableComponent? trashBtn,
            List<ClickableComponent> chestSlots,
            List<ClickableComponent> playerSlots,
            bool showScrollButtons
        )
        {
            var hash = new HashCode();
            hash.Add(anchorCenterX);
            hash.Add(showScrollButtons);
            hash.Add(chestSlots[0].bounds.X);
            hash.Add(chestSlots[0].bounds.Y);
            hash.Add(chestSlots.Count);
            hash.Add(playerSlots[0].bounds.X);
            hash.Add(playerSlots[0].bounds.Y);
            hash.Add(playerSlots[playerSlots.Count - 1].bounds.Y);
            hash.Add(playerSlots.Count);
            AddStableButtonShape(ref hash, colorBtn);
            AddStableButtonShape(ref hash, fillBtn);
            AddStableButtonShape(ref hash, organizeBtn);
            AddStableButtonShape(ref hash, specialBtn);
            AddStableButtonShape(ref hash, okBtn);
            AddStableButtonShape(ref hash, trashBtn);

            foreach (var button in chestTopColumn)
            {
                AddStableButtonShape(ref hash, button);
            }

            return hash.ToHashCode();
        }

        private static void AddStableButtonShape(ref HashCode hash, ClickableComponent? button)
        {
            if (button == null)
            {
                hash.Add(0);
                return;
            }

            hash.Add(1);
            // Intentionally exclude myID: the Stardew 1.6 ItemGrabMenu rebuilds itself from
            // scratch on every item transfer (new ItemGrabMenu(...) via chest callbacks), and
            // some buttons — notably the colorPickerToggleButton found via FindColorPicker —
            // get assigned different myID values between the original construction and the
            // reconstructed menu, even though their visual position/size is identical.
            // Including myID here causes a layout hash miss on every X-button press, forcing
            // a full relayout and button reposition when nothing visible actually changed.
            // The name, width, and height are sufficient to distinguish button "presence" for
            // layout-change detection purposes.
            hash.Add(button.name ?? string.Empty);
            hash.Add(button.bounds.Width);
            hash.Add(button.bounds.Height);
        }

        private static void ItemGrabMenuDrawPrefix(ItemGrabMenu __instance, SpriteBatch b)
        {
            // No per-frame side-button scan here. Chest layout is handled by MenuChanged,
            // populateClickableComponentList, input triggers, and short pending update passes.
        }

        private static void ShopMenuDrawPrefix(ShopMenu __instance, SpriteBatch b)
        {
            InventoryMenuLayoutHelpers.AdjustShopMenuScrollLayout(
                __instance,
                StardewMenuFields.ShopMenuScrollBarRunner
            );
        }

        private static void MuseumMenuDrawPrefix(MuseumMenu __instance, SpriteBatch b)
        {
            // No per-frame side-button scan here.
        }

    

        // ---- Vanilla menu lifecycle hooks ----
        private static void updatePositionPostfix(ShopMenu __instance)
        {
            if (InventoryGridMetrics.PlayerHasExpandedInventory())
            {
                int extraHeight = GetExtraHeight();
                __instance.yPositionOnScreen -= extraHeight / 2;
            }

            InventoryMenuLayoutHelpers.AdjustShopMenuScrollLayout(
                __instance,
                StardewMenuFields.ShopMenuScrollBarRunner
            );
        }

        static bool isWithinBoundsPrefix(
            IClickableMenu __instance,
            ref bool __result,
            int x,
            ref int y
        )
        {
            if (InventoryGridMetrics.PlayerHasExpandedInventory())
            {
                if (__instance is InventoryPage)
                {
                    int extraSpace = GetExtraHeight();
                    y += extraSpace;
                }
                else if (__instance is ShopMenu)
                {
                    int extraHeight = GetExtraHeight();
                    __result =
                        x >= __instance.xPositionOnScreen
                        && x <= __instance.xPositionOnScreen + __instance.width
                        && y >= __instance.yPositionOnScreen
                        && y <= __instance.yPositionOnScreen + __instance.height + extraHeight;
                    return false; // bypass original
                }
            }
            return true;
        }

        private static void iClickableMenuPrefix(
            IClickableMenu __instance,
            ref int y,
            ref int height
        )
        {
            if (__instance is MuseumMenu)
            {
                return;
            }

            if (__instance is not (GameMenu or MenuWithInventory or ShopMenu or TailoringMenu))
                return;

            if (InventoryGridMetrics.PlayerHasExpandedInventory())
            {
                if (__instance is GameMenu)
                {
                    int extraSpace = GetExtraHeight() / 2;
                    y -= extraSpace;
                }
                else if (__instance is ItemGrabMenu)
                {
                    int extraSpace = GetExtraHeight();
                    height += extraSpace;
                    y -= extraSpace / 2 - InventoryGridMetrics.DefaultRowHeight;
                }
                else
                {
                    int extraSpace = GetExtraHeight();
                    height += extraSpace;
                    y -= extraSpace / 2;
                }
            }
        }

        private static bool IsCalledFromMuseum()
        {
            try
            {
                foreach (var frame in new System.Diagnostics.StackTrace().GetFrames())
                {
                    var method = frame.GetMethod();
                    if (method != null && method.DeclaringType != null)
                    {
                        if (typeof(MuseumMenu).IsAssignableFrom(method.DeclaringType))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static void InventoryMenuPrefix(
            ref int yPosition,
            ref IList<Item> actualInventory,
            ref bool playerInventory,
            ref int capacity,
            ref int rows
        )
        {
            InventoryMenuConstructorLayout.Apply(
                ref yPosition,
                ref actualInventory,
                ref playerInventory,
                ref capacity,
                ref rows,
                CurrentParentMenu,
                IsCalledFromMuseum()
            );
        }

        private static void InventoryPagePrefix(ref int y, ref int height)
        {
            if (InventoryGridMetrics.PlayerHasExpandedInventory())
            {
                int extraSpace = GetExtraHeight();
                height += extraSpace;
                y += extraSpace;
            }
        }

        private static void InventoryPagePostfix(InventoryPage __instance)
        {
            // Build the vanilla/component list first, then apply our arrow layout and side-column
            // gamepad links. Doing this in the opposite order lets populateClickableComponentList
            // overwrite the first-frame remap, so the second mod column only starts working after
            // a later mouse click/rebuild.
            __instance.populateClickableComponentList();
            EnsureScrollButtons(__instance);
        }

        private static void CraftingPagePrefix(ref int y, ref int height)
        {
            if (InventoryGridMetrics.PlayerHasExpandedInventory())
            {
                int extraSpace = GetExtraHeight();
                height += extraSpace;
            }
        }

        private static void InventoryMenuMethodPrefix(InventoryMenu __instance)
        {
            if (Game1.player == null)
                return;

            if (StardewMenuFields.ActualInventory.GetValue(__instance) is not IList<Item> currentInventory)
                return;
            if (currentInventory is ScrollableInventoryList scrollList)
            {
                var grid = ViewportRegistry.GetOrCreate(__instance);
                grid.Depth++;

                // Only player inventory methods depend on Game1.player.maxItems.
                // Never shrink/max the player while a non-player inventory (e.g. chest grid)
                // is being drawn or clicked through a viewport window.
                if (ReferenceEquals(scrollList.Underlying, Game1.player.Items))
                {
                    if (grid.OriginalMaxItems == null)
                    {
                        grid.OriginalMaxItems = Game1.player.maxItems.Value;
                    }
                    Game1.player.maxItems.Value = scrollList.Count;
                }
                return;
            }

            var gridViewport = ViewportRegistry.GetOrCreate(__instance);
            gridViewport.Depth++;
            gridViewport.FullInventory = currentInventory;

            int columns = __instance.capacity / __instance.rows;
            if (columns <= 0)
                columns = InventoryGridMetrics.DefaultColumnCount;

            bool isPlayerInventory = ReferenceEquals(currentInventory, Game1.player.Items);
            int effectiveSlotCount = InventoryGridMetrics.GetEffectiveSlotCount(
                currentInventory,
                isPlayerInventory
            );
            int totalRows = InventoryGridMetrics.GetRequiredRows(effectiveSlotCount, columns);
            int maxScrollRow = Math.Max(0, totalRows - __instance.rows);
            gridViewport.ScrollRow = Math.Clamp(gridViewport.ScrollRow, 0, maxScrollRow);

            if (effectiveSlotCount <= __instance.capacity)
                return;

            gridViewport.OriginalInventory = currentInventory;
            var scrollableList = new ScrollableInventoryList(
                currentInventory,
                gridViewport.ScrollRow * columns,
                __instance.capacity,
                effectiveSlotCount
            );
            StardewMenuFields.ActualInventory.SetValue(__instance, scrollableList);

            if (isPlayerInventory)
            {
                if (gridViewport.OriginalMaxItems == null)
                {
                    gridViewport.OriginalMaxItems = Game1.player.maxItems.Value;
                }
                Game1.player.maxItems.Value = scrollableList.Count;
            }
        }

        private static void InventoryMenuMethodPostfix(InventoryMenu __instance)
        {
            if (!ViewportRegistry.TryGet(__instance, out GridViewport? grid) || grid == null)
                return;

            grid.Depth--;
            if (grid.Depth > 0)
                return;

            if (grid.OriginalMaxItems != null)
            {
                if (Game1.player != null)
                {
                    Game1.player.maxItems.Value = grid.OriginalMaxItems.Value;
                }
                grid.OriginalMaxItems = null;
            }

            if (grid.OriginalInventory is not null)
            {
                StardewMenuFields.ActualInventory.SetValue(__instance, grid.OriginalInventory);
                grid.OriginalInventory = null;
            }
        }

        private static void EnsureScrollButtons(InventoryPage page)
        {
            if (StardewMenuFields.InventoryPageInventory.GetValue(page) is not InventoryMenu inventoryMenu)
                return;
            var grid = ViewportRegistry.GetOrCreate(inventoryMenu);
            RefreshInventorySource(inventoryMenu, grid);

            grid.CustomArrowLayout = true;
            grid.UpArrow.myID = ArrowIdUp;
            grid.DownArrow.myID = ArrowIdDown;

            // Layout can still use the arrow bounds as virtual anchors, but the arrows only
            // become clickable/navigable when the inventory actually needs scroll.
            LayoutScrollButtons(page, grid);

            bool showScrollButtons = InventoryPageShouldShowScrollButtons(page, inventoryMenu, grid);
            SetInventoryPageScrollButtonsClickable(page, grid, showScrollButtons);

            if (Game1.options?.SnappyMenus ?? false)
            {
                var activeComponents = page.allClickableComponents;
                if (
                    Game1.activeClickableMenu is GameMenu gameMenu
                    && gameMenu.allClickableComponents != null
                )
                {
                    activeComponents = gameMenu.allClickableComponents;
                }
                if (activeComponents != null)
                {
                    WireGamepadNavigation(page, activeComponents);
                }
            }
        }

        private static void LayoutScrollButtons(InventoryPage page, GridViewport grid)
        {
            if (StardewMenuFields.InventoryPageInventory.GetValue(page) is not InventoryMenu inventoryMenu)
                return;
            if (
                StardewMenuFields.Inventory.GetValue(inventoryMenu) is not List<ClickableComponent> slots
                || slots.Count == 0
            )
                return;
            var organizeButton =
                StardewMenuFields.InventoryPageOrganizeButton.GetValue(page) as ClickableTextureComponent;
            bool showScrollButtons = InventoryPageShouldShowScrollButtons(page, inventoryMenu, grid);
            bool shouldLayoutSideButtons = InventoryPageShouldLayoutSideButtons(page, inventoryMenu, grid, showScrollButtons);
            int effectiveSlots = GetInventoryPageEffectiveSlotCount(inventoryMenu, grid);
            int requiredRows = GetInventoryPageEffectiveRequiredRows(inventoryMenu, grid);
            LayoutDiagnostics.DebugChanged("InventoryPage:layout-decision", $"[FIV/InventoryPage] layout decision: rows={inventoryMenu.rows}, capacity={inventoryMenu.capacity}, slotComponents={slots.Count}, effectiveSlots={effectiveSlots}, requiredRows={requiredRows}, scrollRow={grid.ScrollRow}, showScroll={showScrollButtons}, shouldLayoutSideButtons={shouldLayoutSideButtons}, organize={DescribeComponent(organizeButton)}");
            if (shouldLayoutSideButtons)
            {
                grid.LayoutInventoryPageButtons(page, slots, organizeButton, showScrollButtons);
            }
        }

        private static bool InventoryPageShouldLayoutSideButtons(
            InventoryPage page,
            InventoryMenu inventoryMenu,
            GridViewport? grid,
            bool showScrollButtons
        )
        {
            if (showScrollButtons)
                return true;

            int visibleRows = Math.Max(inventoryMenu.rows, InventoryGridMetrics.DefaultRowCount);
            if (visibleRows >= 4)
                return true;

            int requiredRows = GetInventoryPageEffectiveRequiredRows(inventoryMenu, grid);
            return requiredRows >= 4 && visibleRows >= 4;
        }

        private static int GetInventoryPageEffectiveRequiredRows(
            InventoryMenu inventoryMenu,
            GridViewport? grid
        )
        {
            int columns = InventoryGridMetrics.GetColumns(inventoryMenu.capacity, inventoryMenu.rows);
            int effectiveSlots = GetInventoryPageEffectiveSlotCount(inventoryMenu, grid);
            return InventoryGridMetrics.GetRequiredRows(effectiveSlots, columns);
        }

        private static bool InventoryPageShouldShowScrollButtons(
            InventoryPage page,
            InventoryMenu inventoryMenu,
            GridViewport? grid
        )
        {
            return GetInventoryPageEffectiveSlotCount(inventoryMenu, grid) > inventoryMenu.capacity;
        }

        private static int GetInventoryPageEffectiveSlotCount(
            InventoryMenu inventoryMenu,
            GridViewport? grid
        )
        {
            IList<Item>? source = RefreshInventorySource(inventoryMenu, grid);
            bool isPlayerInventory = source != null && ReferenceEquals(source, Game1.player?.Items);
            return InventoryGridMetrics.GetEffectiveSlotCount(source, isPlayerInventory);
        }

        private static IList<Item>? RefreshInventorySource(InventoryMenu inventoryMenu, GridViewport? grid)
        {
            IList<Item>? source = null;
            if (StardewMenuFields.ActualInventory.GetValue(inventoryMenu) is IList<Item> currentInventory)
            {
                source = currentInventory is ScrollableInventoryList scrollable
                    ? scrollable.Underlying
                    : currentInventory;
            }

            source ??= grid?.OriginalInventory ?? grid?.FullInventory;

            if (grid != null && source != null)
            {
                int oldScrollRow = grid.ScrollRow;
                grid.FullInventory = source;
                int columns = InventoryGridMetrics.GetColumns(inventoryMenu.capacity, inventoryMenu.rows);
                bool isPlayerInventory = ReferenceEquals(source, Game1.player?.Items);
                int effectiveSlots = InventoryGridMetrics.GetEffectiveSlotCount(source, isPlayerInventory);
                int totalRows = InventoryGridMetrics.GetRequiredRows(effectiveSlots, columns);
                int maxScrollRow = Math.Max(0, totalRows - inventoryMenu.rows);
                grid.ScrollRow = Math.Clamp(grid.ScrollRow, 0, maxScrollRow);
                if (maxScrollRow == 0)
                    grid.ScrollRow = 0;

                grid.ApplyVisibleSlotWindow(source, "refresh-source");

                var sourceHash = new HashCode();
                sourceHash.Add(inventoryMenu.rows);
                sourceHash.Add(inventoryMenu.capacity);
                sourceHash.Add(columns);
                sourceHash.Add(source.Count);
                sourceHash.Add(isPlayerInventory);
                sourceHash.Add(effectiveSlots);
                sourceHash.Add(totalRows);
                sourceHash.Add(maxScrollRow);
                sourceHash.Add(grid.ScrollRow);
                sourceHash.Add(source.GetType().FullName);
                int sourceSignature = sourceHash.ToHashCode();
                if (grid.LastInventorySourceSignature != sourceSignature || oldScrollRow != grid.ScrollRow)
                {
                    grid.LastInventorySourceSignature = sourceSignature;
                    LayoutDiagnostics.DebugChanged($"InventorySource:refresh:{inventoryMenu.GetHashCode()}:{isPlayerInventory}", $"[FIV/InventorySource] refresh: rows={inventoryMenu.rows}, capacity={inventoryMenu.capacity}, columns={columns}, sourceCount={source.Count}, isPlayer={isPlayerInventory}, effectiveSlots={effectiveSlots}, totalRows={totalRows}, maxScroll={maxScrollRow}, scrollRow={oldScrollRow}->{grid.ScrollRow}, sourceType={source.GetType().FullName}");
                }
            }

            return source;
        }

        private static void SetInventoryPageScrollButtonsClickable(
            InventoryPage page,
            GridViewport grid,
            bool enabled
        )
        {
            page.allClickableComponents ??= new List<ClickableComponent>();
            var upArrow = grid.UpArrow;
            var downArrow = grid.DownArrow;

            LayoutDiagnostics.DebugChanged("InventoryPageScrollButtons:set-clickable", $"[FIV/InventoryPageScrollButtons] set clickable: enabled={enabled}, pageComponents={page.allClickableComponents.Count}, up={DescribeComponent(upArrow)}, down={DescribeComponent(downArrow)}");

            if (enabled)
            {
                AddClickableComponent(page, upArrow);
                AddClickableComponent(page, downArrow);
            }
            else
            {
                page.allClickableComponents.Remove(upArrow);
                page.allClickableComponents.Remove(downArrow);
            }

            if (
                Game1.activeClickableMenu is GameMenu gameMenu
                && gameMenu.GetCurrentPage() == page
                && gameMenu.allClickableComponents != null
            )
            {
                if (enabled)
                {
                    if (!gameMenu.allClickableComponents.Contains(upArrow))
                        gameMenu.allClickableComponents.Add(upArrow);
                    if (!gameMenu.allClickableComponents.Contains(downArrow))
                        gameMenu.allClickableComponents.Add(downArrow);
                }
                else
                {
                    gameMenu.allClickableComponents.Remove(upArrow);
                    gameMenu.allClickableComponents.Remove(downArrow);
                }
            }
        }

        private static void AddClickableComponent(InventoryPage page, ClickableComponent component)
        {
            page.allClickableComponents ??= new List<ClickableComponent>();
            if (!page.allClickableComponents.Contains(component))
            {
                page.allClickableComponents.Add(component);
            }
        }

        private static bool GameMenuReceiveScrollWheelActionPrefix(
            GameMenu __instance,
            int direction
        )
        {
            if (__instance.GetCurrentPage() is not InventoryPage inventoryPage)
                return true;
            if (
                StardewMenuFields.InventoryPageInventory.GetValue(inventoryPage)
                is not InventoryMenu inventoryMenu
            )
                return true;

            if (ViewportRegistry.TryGet(inventoryMenu, out var grid) && grid != null)
            {
                IList<Item>? items = RefreshInventorySource(inventoryMenu, grid);
                if (
                    items != null
                    && MenuComponentFinder.IsMouseTargetingInventoryArea(
                        inventoryMenu,
                        grid,
                        Game1.getOldMouseX(),
                        Game1.getOldMouseY()
                    )
                    && grid.ReceiveScrollWheelAction(direction, items)
                )
                {
                    return false;
                }
            }
            return true;
        }

        private static void GameMenuUpdatePostfix(GameMenu __instance, GameTime time)
        {
            if (__instance.GetCurrentPage() is not InventoryPage inventoryPage)
                return;
            if (
                StardewMenuFields.InventoryPageInventory.GetValue(inventoryPage)
                is not InventoryMenu inventoryMenu
            )
                return;
            GridViewport? grid = null;
            if (InventoryGridMetrics.PlayerHasExpandedInventory())
            {
                grid = ViewportRegistry.GetOrCreate(inventoryMenu);

                // Keep the InventoryPage side-column links current from the first visible frame.
                // Some mods rebuild/adjust side buttons after construction without changing the
                // component count, so a count-only hash can leave stale first-frame navigation.
                LayoutScrollButtons(inventoryPage, grid);
                if (__instance.allClickableComponents != null)
                {
                    WireGamepadNavigation(inventoryPage, __instance.allClickableComponents);
                }
            }

            if (ViewportRegistry.TryGet(inventoryMenu, out var viewport) && viewport != null)
            {
                IList<Item>? items = RefreshInventorySource(inventoryMenu, viewport);
                if (items != null)
                {
                    viewport.UpdatePointerInteraction(items);
                }
                if (
                    items != null
                    && MenuComponentFinder.IsGamepadTargetingInventoryArea(
                        __instance,
                        inventoryMenu,
                        viewport
                    )
                )
                {
                    viewport.UpdateGamepad(items);
                }
            }
        }
    

        // ---- Manual chest transfer service ----
        private static bool MenuReceiveScrollWheelActionPrefix(
            IClickableMenu __instance,
            int direction
        )
        {
            var menus = GetKnownInventoryMenus(__instance);
            foreach (var menu in menus)
            {
                if (
                    MenuComponentFinder.IsMouseTargetingInventoryArea(
                        menu,
                        ViewportRegistry.TryGet(menu, out GridViewport? g) ? g : null,
                        Game1.getOldMouseX(),
                        Game1.getOldMouseY()
                    )
                )
                {
                    if (ViewportRegistry.TryGet(menu, out GridViewport? grid) && grid != null)
                    {
                        IList<Item>? items = RefreshInventorySource(menu, grid);
                        if (items != null && grid.ReceiveScrollWheelAction(direction, items))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private static bool IsPlainChestItemGrabMenu(ItemGrabMenu menu, out Chest? chest)
        {
            chest = null;

            // Do not intercept custom ItemGrabMenu subclasses. HugeChestMenu already does the
            // correct thing by overriding item interactions manually; this guard keeps us from
            // fighting custom menus from other mods.
            if (menu.GetType() != typeof(ItemGrabMenu))
                return false;

            if (menu.shippingBin || menu.ItemsToGrabMenu == null || menu.inventory == null)
                return false;

            chest = menu.sourceItem as Chest ?? menu.context as Chest;
            return chest != null && menu.source == ItemGrabMenu.source_chest;
        }

        private static bool TryHandleChestManualLeftClick(
            ItemGrabMenu menu,
            int x,
            int y,
            bool playSound
        )
        {
            if (!IsPlainChestItemGrabMenu(menu, out Chest? chest) || chest == null)
                return false;

            if (TryHandleChestStructuralLeftClick(menu, chest, x, y, playSound))
                return true;

            if (IsPointInInventoryMenu(menu.ItemsToGrabMenu, x, y))
                return TryHandleChestInventoryLeftClick(menu, chest, x, y, playSound);

            if (IsPointInInventoryMenu(menu.inventory, x, y))
                return TryHandlePlayerInventoryLeftClick(menu, chest, x, y, playSound);

            return false;
        }

        private static bool TryHandleChestManualRightClick(
            ItemGrabMenu menu,
            int x,
            int y,
            bool playSound
        )
        {
            if (!IsPlainChestItemGrabMenu(menu, out Chest? chest) || chest == null)
                return false;

            // The vanilla ItemGrabMenu.receiveRightClick path can also call setSourceItem.
            // Intercept both grids so right click / controller X can move one item without
            // rebuilding the ItemGrabMenu.
            if (IsPointInInventoryMenu(menu.ItemsToGrabMenu, x, y))
                return TryHandleChestInventoryRightClick(menu, chest, x, y, playSound);

            if (IsPointInInventoryMenu(menu.inventory, x, y))
                return TryHandlePlayerInventoryRightClick(menu, chest, x, y, playSound);

            return false;
        }

        private static bool IsBaseReceiveGamePadDispatchForItemGrabMenu(
            IClickableMenu menu,
            MethodBase originalMethod
        )
        {
            // We patch both ItemGrabMenu.receiveGamePadButton and IClickableMenu.receiveGamePadButton.
            // Vanilla ItemGrabMenu.receiveGamePadButton calls base.receiveGamePadButton(button), so
            // the same physical A/X press can reach this prefix twice: first on ItemGrabMenu, then
            // again on the base IClickableMenu method. Manual chest transfers must only run on the
            // ItemGrabMenu method itself; the base dispatch is allowed to continue vanilla behavior.
            return menu is ItemGrabMenu && originalMethod.DeclaringType == typeof(IClickableMenu);
        }

        private static bool TryHandleChestManualGamePadButton(
            ItemGrabMenu menu,
            Buttons button
        )
        {
            if (button is not (Buttons.A or Buttons.X))
                return false;

            if (!IsPlainChestItemGrabMenu(menu, out Chest? chest) || chest == null)
                return false;

            ClickableComponent? snapped = menu.currentlySnappedComponent;
            if (snapped == null)
                return false;

            if (menu.organizeButton != null && ReferenceEquals(snapped, menu.organizeButton))
            {
                OrganizeChestWithoutRebuilding(menu);
                return true;
            }

            if (menu.fillStacksButton != null && ReferenceEquals(snapped, menu.fillStacksButton))
            {
                menu.FillOutStacks();
                Game1.playSound("Ship");
                return true;
            }

            if (menu.ItemsToGrabMenu?.inventory != null && menu.ItemsToGrabMenu.inventory.Contains(snapped))
            {
                return button == Buttons.X
                    ? TryHandleChestInventoryRightClick(menu, chest, snapped.bounds.Center.X, snapped.bounds.Center.Y, playSound: true)
                    : TryHandleChestInventoryLeftClick(menu, chest, snapped.bounds.Center.X, snapped.bounds.Center.Y, playSound: true);
            }

            if (menu.inventory?.inventory != null && menu.inventory.inventory.Contains(snapped))
            {
                return button == Buttons.X
                    ? TryHandlePlayerInventoryRightClick(menu, chest, snapped.bounds.Center.X, snapped.bounds.Center.Y, playSound: true)
                    : TryHandlePlayerInventoryLeftClick(menu, chest, snapped.bounds.Center.X, snapped.bounds.Center.Y, playSound: true);
            }

            return false;
        }

        private static bool TryHandleChestStructuralLeftClick(
            ItemGrabMenu menu,
            Chest chest,
            int x,
            int y,
            bool playSound
        )
        {
            if (menu.chestColorPicker != null && menu.chestColorPicker.visible)
            {
                Rectangle pickerBounds = new Rectangle(
                    menu.chestColorPicker.xPositionOnScreen,
                    menu.chestColorPicker.yPositionOnScreen,
                    menu.chestColorPicker.width,
                    menu.chestColorPicker.height
                );
                if (pickerBounds.Contains(x, y))
                {
                    menu.chestColorPicker.receiveLeftClick(x, y);
                    chest.playerChoiceColor.Value = DiscreteColorPicker.getColorFromSelection(menu.chestColorPicker.colorSelection);
                    return true;
                }
            }

            if (menu.colorPickerToggleButton != null && menu.colorPickerToggleButton.containsPoint(x, y))
            {
                Game1.player.showChestColorPicker = !Game1.player.showChestColorPicker;
                if (menu.chestColorPicker != null)
                    menu.chestColorPicker.visible = Game1.player.showChestColorPicker;
                Game1.playSound("drumkit6");
                return true;
            }

            if (menu.organizeButton != null && menu.organizeButton.containsPoint(x, y))
            {
                OrganizeChestWithoutRebuilding(menu);
                return true;
            }

            if (menu.fillStacksButton != null && menu.fillStacksButton.containsPoint(x, y))
            {
                menu.FillOutStacks();
                Game1.playSound("Ship");
                return true;
            }

            return false;
        }

        private static void OrganizeChestWithoutRebuilding(ItemGrabMenu menu)
        {
            if (menu.ItemsToGrabMenu?.actualInventory == null)
                return;

            ItemGrabMenu.organizeItemsInList(menu.ItemsToGrabMenu.actualInventory);
            Game1.playSound("Ship");

            // Keep the current snapped component stable. Vanilla rebuilds with new ItemGrabMenu(this)
            // and then copies the snapped ID; we avoid the rebuild and leave the same component alive.
            if (Game1.options.SnappyMenus && menu.currentlySnappedComponent != null)
                menu.snapCursorToCurrentSnappedComponent();
        }

        private static bool TryHandleChestInventoryLeftClick(
            ItemGrabMenu menu,
            Chest chest,
            int x,
            int y,
            bool playSound
        )
        {
            if (menu.ItemsToGrabMenu == null)
                return false;

            int slotIndex = GetInventorySlotIndex(menu.ItemsToGrabMenu, x, y);
            if (slotIndex < 0)
                return true;

            IList<Item?> chestItems = GetChestItems(menu, chest);

            // Same intent as HugeChestMenu.receiveLeftClick: a left click / A button moves the
            // whole held stack into the chest, or moves the whole clicked chest stack to the player.
            // Do not call ItemGrabMenu.receiveLeftClick here; that vanilla path can rebuild
            // ItemGrabMenu and break the side-button layout.
            if (menu.heldItem != null)
            {
                Item held = menu.heldItem;
                Item? target = slotIndex < chestItems.Count ? chestItems[slotIndex] : null;

                if (target != null && target.canStackWith(held))
                {
                    int remaining = target.addToStack(held);
                    menu.heldItem = remaining <= 0 ? null : held;
                    if (menu.heldItem != null)
                        menu.heldItem.Stack = remaining;
                }
                else
                {
                    menu.heldItem = chest.addItem(held);
                }

                if (playSound)
                    Game1.playSound("coin");
                return true;
            }

            if (!menu.showReceivingMenu)
                return true;

            if (slotIndex >= chestItems.Count || chestItems[slotIndex] == null)
                return true;

            Item item = chestItems[slotIndex]!;
            chestItems.RemoveAt(slotIndex);

            Item? leftover = Game1.player.addItemToInventory(item);
            if (leftover != null)
                leftover = chest.addItem(leftover);
            if (leftover != null)
                menu.heldItem = leftover;

            if (playSound)
                Game1.playSound("coin");
            return true;
        }

        private static bool TryHandleChestInventoryRightClick(
            ItemGrabMenu menu,
            Chest chest,
            int x,
            int y,
            bool playSound
        )
        {
            if (menu.ItemsToGrabMenu == null)
                return false;

            int slotIndex = GetInventorySlotIndex(menu.ItemsToGrabMenu, x, y);
            if (slotIndex < 0)
                return true;

            IList<Item?> chestItems = GetChestItems(menu, chest);

            // Right click / X button moves exactly one item. This is the difference that was
            // missing before: A = whole stack, X = one item.
            if (menu.heldItem != null)
            {
                if (menu.heldItem.Stack <= 1)
                {
                    menu.heldItem = chest.addItem(menu.heldItem);
                }
                else
                {
                    Item one = menu.heldItem.getOne();
                    int movedFromHeldStack = Math.Max(1, Math.Min(one.Stack, menu.heldItem.Stack));
                    Item? leftover = chest.addItem(one);
                    if (leftover == null)
                    {
                        menu.heldItem.Stack -= movedFromHeldStack;
                        if (menu.heldItem.Stack <= 0)
                            menu.heldItem = null;
                    }
                }

                if (playSound)
                    Game1.playSound("coin");
                return true;
            }

            if (!menu.showReceivingMenu)
                return true;

            if (slotIndex >= chestItems.Count || chestItems[slotIndex] == null)
                return true;

            Item item = chestItems[slotIndex]!;
            if (item.Stack <= 1)
            {
                if (Game1.player.addItemToInventoryBool(item, false))
                {
                    chestItems.RemoveAt(slotIndex);
                    if (playSound)
                        Game1.playSound("coin");
                }
                return true;
            }

            Item oneItem = item.getOne();
            int movedStack = Math.Max(1, Math.Min(oneItem.Stack, item.Stack));
            if (Game1.player.addItemToInventoryBool(oneItem, false))
            {
                item.Stack -= movedStack;
                if (item.Stack <= 0)
                    chestItems.RemoveAt(slotIndex);
                if (playSound)
                    Game1.playSound("coin");
            }

            return true;
        }

        private static bool TryHandlePlayerInventoryLeftClick(
            ItemGrabMenu menu,
            Chest chest,
            int x,
            int y,
            bool playSound
        )
        {
            if (menu.inventory == null)
                return false;

            if (menu.heldItem != null)
            {
                // Player-grid held-item placement does not need ItemGrabMenu's chest transfer
                // logic, so this is safe and keeps normal slot swapping behavior.
                Item? before = menu.heldItem;
                menu.heldItem = menu.inventory.leftClick(x, y, menu.heldItem, playSound: false);
                if (playSound && !ReferenceEquals(before, menu.heldItem))
                    Game1.playSound("coin");
                return true;
            }

            Item? item = menu.inventory.getItemAt(x, y);
            if (item == null)
                return true;

            Game1.player.removeItemFromInventory(item);
            Item? leftover = chest.addItem(item);
            if (leftover != null)
                Game1.player.addItemToInventory(leftover);

            if (playSound)
                Game1.playSound("coin");
            return true;
        }

        private static bool TryHandlePlayerInventoryRightClick(
            ItemGrabMenu menu,
            Chest chest,
            int x,
            int y,
            bool playSound
        )
        {
            if (menu.inventory == null)
                return false;

            if (menu.heldItem != null)
            {
                if (menu.heldItem.Stack <= 1)
                {
                    menu.heldItem = chest.addItem(menu.heldItem);
                }
                else
                {
                    Item one = menu.heldItem.getOne();
                    int movedFromHeldStack = Math.Max(1, Math.Min(one.Stack, menu.heldItem.Stack));
                    Item? leftover = chest.addItem(one);
                    if (leftover == null)
                    {
                        menu.heldItem.Stack -= movedFromHeldStack;
                        if (menu.heldItem.Stack <= 0)
                            menu.heldItem = null;
                    }
                }

                if (playSound)
                    Game1.playSound("coin");
                return true;
            }

            Item? item = menu.inventory.getItemAt(x, y);
            if (item == null)
                return true;

            if (item.Stack <= 1)
            {
                Game1.player.removeItemFromInventory(item);
                Item? leftover = chest.addItem(item);
                if (leftover != null)
                    Game1.player.addItemToInventory(leftover);
                if (playSound)
                    Game1.playSound("coin");
                return true;
            }

            Item oneItem = item.getOne();
            int movedStack = Math.Max(1, Math.Min(oneItem.Stack, item.Stack));
            Item? returned = chest.addItem(oneItem);
            if (returned == null)
            {
                item.Stack -= movedStack;
                if (item.Stack <= 0)
                    Game1.player.removeItemFromInventory(item);
                if (playSound)
                    Game1.playSound("coin");
            }

            return true;
        }

        private static IList<Item?> GetChestItems(ItemGrabMenu menu, Chest chest)
        {
            if (menu.ItemsToGrabMenu?.actualInventory is IList<Item?> nullableItems)
                return nullableItems;

            if (menu.ItemsToGrabMenu?.actualInventory != null)
                return new ItemListAdapter(menu.ItemsToGrabMenu.actualInventory);

            return new ItemListAdapter(chest.Items);
        }

        private sealed class ItemListAdapter : IList<Item?>
        {
            private readonly IList<Item> items;

            public ItemListAdapter(IList<Item> items)
            {
                this.items = items;
            }

            public Item? this[int index]
            {
                get => this.items[index];
                set => this.items[index] = value!;
            }

            public int Count => this.items.Count;
            public bool IsReadOnly => this.items.IsReadOnly;
            public void Add(Item? item) => this.items.Add(item!);
            public void Clear() => this.items.Clear();
            public bool Contains(Item? item) => item != null && this.items.Contains(item);
            public void CopyTo(Item?[] array, int arrayIndex)
            {
                for (int i = 0; i < this.items.Count; i++)
                    array[arrayIndex + i] = this.items[i];
            }
            public IEnumerator<Item?> GetEnumerator() => this.items.Cast<Item?>().GetEnumerator();
            public int IndexOf(Item? item) => item == null ? -1 : this.items.IndexOf(item);
            public void Insert(int index, Item? item) => this.items.Insert(index, item!);
            public bool Remove(Item? item) => item != null && this.items.Remove(item);
            public void RemoveAt(int index) => this.items.RemoveAt(index);
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private static int GetInventorySlotIndex(InventoryMenu inventoryMenu, int x, int y)
        {
            if (inventoryMenu.inventory == null)
                return -1;

            for (int i = 0; i < inventoryMenu.inventory.Count; i++)
            {
                ClickableComponent? component = inventoryMenu.inventory[i];
                if (component != null && component.containsPoint(x, y))
                    return i;
            }

            return -1;
        }

        private static bool IsPointInInventoryMenu(InventoryMenu? inventoryMenu, int x, int y)
        {
            if (inventoryMenu == null)
                return false;

            if (inventoryMenu.inventory != null)
            {
                foreach (var component in inventoryMenu.inventory)
                {
                    if (component != null && component.containsPoint(x, y))
                        return true;
                }
            }

            return ((IClickableMenu)inventoryMenu).isWithinBounds(x, y);
        }
    

        // ---- Menu input hooks ----
        private static bool MenuReceiveLeftClickPrefix(
            IClickableMenu __instance,
            int x,
            int y,
            bool playSound
        )
        {
            if (TryHandleScrollArrowMouseClick(__instance, x, y))
                return false;

            if (__instance is ItemGrabMenu itemGrabMenu && TryHandleChestManualLeftClick(itemGrabMenu, x, y, playSound))
                return false;

            if (IsSideLayoutMenu(__instance) && __instance is not ItemGrabMenu)
                ScheduleChestLayout(__instance, "receiveLeftClick", 1);

            var menus = GetKnownInventoryMenus(__instance);
            foreach (var menu in menus)
            {
                if (ViewportRegistry.TryGet(menu, out GridViewport? grid) && grid != null)
                {
                    IList<Item>? items = RefreshInventorySource(menu, grid);
                    if (items != null && grid.ReceiveLeftClick(x, y, items))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool MenuReceiveRightClickPrefix(
            IClickableMenu __instance,
            int x,
            int y,
            bool playSound
        )
        {
            if (__instance is ItemGrabMenu itemGrabMenu && TryHandleChestManualRightClick(itemGrabMenu, x, y, playSound))
                return false;

            return true;
        }

        private static void MenuUpdatePostfix(IClickableMenu __instance, GameTime time)
        {
            ChestMenuLayoutState? state = null;
            if (IsSideLayoutMenu(__instance))
            {
                state = GetChestLayoutState(__instance);
                if (__instance is ItemGrabMenu)
                {
                    // Item transfers in chest menus must not keep a general pending relayout alive.
                    // We still allow a tiny delayed bottom-button probe because some mods add
                    // extra buttons after ItemGrabMenu.MenuChanged/populateClickableComponentList.
                    state.PendingLayoutPasses = 0;
                    if (state.PendingBottomProbePasses > 0)
                    {
                        string reason = $"bottom-probe:{state.LastTrigger}";
                        state.PendingBottomProbePasses--;
                        Log.Debug($"[FIV/ChestLifecycle] run bottom probe: menu={__instance.GetType().Name}#{__instance.GetHashCode()}, reason={reason}, remaining={state.PendingBottomProbePasses}, components={__instance.allClickableComponents?.Count ?? 0}");
                        RepositionAndWireSideButtons(__instance, reason, force: false);
                    }
                }
                else if (state.PendingLayoutPasses > 0)
                {
                    string reason = $"pending:{state.LastTrigger}";
                    state.PendingLayoutPasses--;
                    RepositionAndWireSideButtons(__instance, reason, force: state.PendingLayoutPasses > 0);
                }
            }

            var menus = GetKnownInventoryMenus(__instance);
            foreach (var menu in menus)
            {
                if (ViewportRegistry.TryGet(menu, out GridViewport? grid) && grid != null)
                {
                    IList<Item>? items = RefreshInventorySource(menu, grid);
                    if (items != null)
                    {
                        grid.UpdatePointerInteraction(items);
                    }
                    if (
                        items != null
                        && MenuComponentFinder.IsGamepadTargetingInventoryArea(
                            __instance,
                            menu,
                            grid
                        )
                    )
                    {
                        grid.UpdateGamepad(items);
                    }

                    if (state != null && items != null && ReferenceEquals(items, Game1.player?.Items))
                    {
                        RememberChestPlayerScroll(state, grid, menu, items);
                    }
                }
            }
        }

        private static bool TryHandleScrollArrowMouseClick(IClickableMenu parentMenu, int x, int y)
        {
            foreach (var menu in GetKnownInventoryMenus(parentMenu))
            {
                if (!ViewportRegistry.TryGet(menu, out GridViewport? grid) || grid == null)
                    continue;

                IList<Item>? items = RefreshInventorySource(menu, grid);
                if (items == null)
                    continue;

                if (grid.UpArrow.containsPoint(x, y))
                {
                    if (grid.CanScroll(items, -1))
                        grid.Scroll(items, -1);
                    return true;
                }

                if (grid.DownArrow.containsPoint(x, y))
                {
                    if (grid.CanScroll(items, 1))
                        grid.Scroll(items, 1);
                    return true;
                }
            }

            return false;
        }

        private static bool TryActivateSnappedScrollArrow(IClickableMenu parentMenu)
        {
            ClickableComponent? snapped = parentMenu.currentlySnappedComponent;
            if (snapped == null)
                return false;

            foreach (var menu in GetKnownInventoryMenus(parentMenu))
            {
                if (!ViewportRegistry.TryGet(menu, out GridViewport? grid) || grid == null)
                    continue;

                IList<Item>? items = RefreshInventorySource(menu, grid);
                if (items == null)
                    continue;

                if (ReferenceEquals(snapped, grid.UpArrow) || IsScrollArrowByShape(snapped, ArrowIdUp, "Scroll Up"))
                {
                    if (grid.CanScroll(items, -1))
                        grid.Scroll(items, -1);
                    return true;
                }

                if (ReferenceEquals(snapped, grid.DownArrow) || IsScrollArrowByShape(snapped, ArrowIdDown, "Scroll Down"))
                {
                    if (grid.CanScroll(items, 1))
                        grid.Scroll(items, 1);
                    return true;
                }
            }

            return false;
        }

        private static bool IClickableMenuApplyMovementKeyPrefix(IClickableMenu __instance, int direction)
        {
            return !TryScrollInventoryViewportFromNavigation(
                __instance,
                direction,
                oldID: null,
                reason: "applyMovementKey"
            );
        }

        private static bool ItemGrabMenuCustomSnapBehaviorPrefix(
            ItemGrabMenu __instance,
            int direction,
            int oldRegion,
            int oldID
        )
        {
            return !TryScrollInventoryViewportFromNavigation(
                __instance,
                direction,
                oldID,
                reason: $"customSnapBehavior:region={oldRegion}"
            );
        }

        private static bool TryScrollInventoryViewportFromNavigation(
            IClickableMenu parentMenu,
            int direction,
            int? oldID,
            string reason
        )
        {
            // The automatic edge-scroll toggle is intentionally only for the main InventoryPage.
            // In that menu, disabling it lets vanilla snap move from the last visible backpack row
            // to the equipment/hat/ring area instead of scrolling. Chest/shop style menus keep
            // edge-scroll enabled because there is no equivalent lower equipment area to navigate to.
            if (parentMenu is InventoryPage && !ModEntry.Config.EnableInventoryPageGamepadEdgeScroll)
                return false;

            // Stardew directions: 0 = up, 2 = down. Other directions are normal snap behavior.
            int delta = direction switch
            {
                0 => -1,
                2 => 1,
                _ => 0,
            };
            if (delta == 0)
                return false;

            ClickableComponent? snapped = parentMenu.currentlySnappedComponent;

            foreach (var inventoryMenu in GetKnownInventoryMenus(parentMenu))
            {
                if (inventoryMenu?.inventory == null || inventoryMenu.inventory.Count == 0)
                    continue;

                ClickableComponent? slot = null;
                int slotIndex = -1;

                if (snapped != null)
                {
                    slotIndex = inventoryMenu.inventory.IndexOf(snapped);
                    if (slotIndex >= 0)
                        slot = snapped;
                }

                if (slot == null && oldID.HasValue)
                {
                    for (int i = 0; i < inventoryMenu.inventory.Count; i++)
                    {
                        var candidate = inventoryMenu.inventory[i];
                        if (candidate != null && candidate.myID == oldID.Value)
                        {
                            slot = candidate;
                            slotIndex = i;
                            break;
                        }
                    }
                }

                if (slot == null || slotIndex < 0)
                    continue;

                int columns = InventoryGridMetrics.GetColumns(inventoryMenu.capacity, inventoryMenu.rows);
                int row = slotIndex / columns;
                bool atTopEdge = delta < 0 && row == 0;
                bool atBottomEdge = delta > 0 && row == inventoryMenu.rows - 1;
                if (!atTopEdge && !atBottomEdge)
                    continue;

                if (!ViewportRegistry.TryGet(inventoryMenu, out GridViewport? grid) || grid == null)
                    continue;

                IList<Item>? items = RefreshInventorySource(inventoryMenu, grid);
                if (items == null || !grid.CanScroll(items, delta))
                    continue;

                int previousScroll = grid.ScrollRow;
                if (!grid.Scroll(items, delta, reason))
                    continue;

                parentMenu.currentlySnappedComponent = slot;
                if (Game1.options?.SnappyMenus ?? false)
                    parentMenu.snapCursorToCurrentSnappedComponent();

                if (IsSideLayoutMenu(parentMenu) && ReferenceEquals(items, Game1.player?.Items))
                {
                    RememberChestPlayerScroll(
                        GetChestLayoutState(parentMenu),
                        grid,
                        inventoryMenu,
                        items
                    );
                }

                LayoutDiagnostics.DebugChanged(
                    $"GridViewport:edge-nav:{RuntimeHelpers.GetHashCode(parentMenu)}:{RuntimeHelpers.GetHashCode(inventoryMenu)}",
                    $"[FIV/GridViewport] edge navigation scroll: reason={reason}, parent={parentMenu.GetType().Name}#{RuntimeHelpers.GetHashCode(parentMenu)}, menuHash={RuntimeHelpers.GetHashCode(inventoryMenu)}, direction={direction}, delta={delta}, slotIndex={slotIndex}, row={row}, scrollRow={previousScroll}->{grid.ScrollRow}, snapped={DescribeComponent(slot)}"
                );
                return true;
            }

            return false;
        }

        private static bool IsScrollArrowByShape(ClickableComponent component, int id, string name)
        {
            return component.myID == id || string.Equals(component.name, name, StringComparison.Ordinal);
        }

        private static bool MenuReceiveGamePadButtonPrefix(
            IClickableMenu __instance,
            Buttons button,
            MethodBase __originalMethod
        )
        {
            if (__instance is ItemGrabMenu && button is (Buttons.A or Buttons.X or Buttons.Y or Buttons.B))
            {
                LayoutDiagnostics.DebugChanged(
                    $"ChestInput:gamepad:{button}:{RuntimeHelpers.GetHashCode(__instance)}:{DescribeComponent(__instance.currentlySnappedComponent)}:{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}",
                    $"[FIV/ChestInput] gamepad:{button}: source={__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}, menuHash={RuntimeHelpers.GetHashCode(__instance)}, snapped={DescribeComponent(__instance.currentlySnappedComponent)}, components={__instance.allClickableComponents?.Count ?? 0}");
            }

            if (IsBaseReceiveGamePadDispatchForItemGrabMenu(__instance, __originalMethod))
                return true;

            if (button is Buttons.A or Buttons.X)
            {
                if (TryActivateSnappedScrollArrow(__instance))
                    return false;

                if (ExternalSideButtonRegistry.TryActivateSnapped(__instance, button))
                    return false;

                if (__instance is ItemGrabMenu itemGrabMenu && TryHandleChestManualGamePadButton(itemGrabMenu, button))
                    return false;
            }

            bool isDirectionalButton =
                button == Buttons.DPadDown
                || button == Buttons.DPadUp
                || button == Buttons.DPadLeft
                || button == Buttons.DPadRight
                || button == Buttons.LeftThumbstickDown
                || button == Buttons.LeftThumbstickUp
                || button == Buttons.LeftThumbstickLeft
                || button == Buttons.LeftThumbstickRight;

            if (isDirectionalButton)
            {
                if (__instance is InventoryPage inventoryPageInstance)
                {
                    RefreshInventoryPageGamepadNavigation(inventoryPageInstance);
                }
                else if (__instance is GameMenu gameMenu && gameMenu.GetCurrentPage() is InventoryPage page)
                {
                    RefreshInventoryPageGamepadNavigation(page);
                }
            }

            // Edge scrolling is handled in applyMovementKey/customSnapBehavior now.
            // receiveGamePadButton should activate buttons/items, not rebuild or scroll the grid.
            return true;
        }
    

        // ---- InventoryMenu hooks ----
        private static void InventoryMenuDrawPostfix(InventoryMenu __instance, SpriteBatch b)
        {
            if (!ViewportRegistry.TryGet(__instance, out GridViewport? grid) || grid == null)
                return;
            IList<Item>? items = RefreshInventorySource(__instance, grid);
            if (items != null)
            {
                ApplyChestDrawSafetyLayout(__instance, grid, items);
                grid.Draw(b, items);
            }
        }

        private static void ApplyChestDrawSafetyLayout(
            InventoryMenu inventoryMenu,
            GridViewport grid,
            IList<Item> items
        )
        {
            if (Game1.activeClickableMenu == null || !IsSideLayoutMenu(Game1.activeClickableMenu))
                return;
            if (!ReferenceEquals(items, Game1.player?.Items))
                return;
            if (!ChestLayoutStates.TryGetValue(Game1.activeClickableMenu, out var state))
                return;
            if (state.SideButtonTargets.Count == 0 || grid.HasResolvedCustomLayout)
                return;

            grid.CustomArrowLayout = true;
            grid.UpArrow.myID = ArrowIdUp;
            grid.DownArrow.myID = ArrowIdDown;
            RestoreChestPlayerScroll(state, grid, inventoryMenu, "draw-safety");
            bool showScrollButtons = InventoryGridMetrics.GetEffectiveSlotCount(items, isPlayerInventory: true) > inventoryMenu.capacity;
            grid.SetScrollButtonsClickable(Game1.activeClickableMenu, showScrollButtons);
            grid.ImportSideButtonTargets(state.SideButtonTargets);
            var fields = BuildMenuCachedFields(Game1.activeClickableMenu);
            Func<ClickableComponent, bool> cachePredicate = button => ShouldApplyChestCachedTarget(button, fields.OkBtn, fields.TrashBtn, grid, inventoryMenu.inventory);
            bool applied = SideButtonLayoutEngine.ApplyCachedTargets(grid, Game1.activeClickableMenu, "draw-safety", cachePredicate);
            grid.HasResolvedCustomLayout = applied;
            RememberChestPlayerScroll(state, grid, inventoryMenu, items);
        }

        private static void InventoryMenuPerformHoverActionPostfix(
            InventoryMenu __instance,
            int x,
            int y
        )
        {
            if (!ViewportRegistry.TryGet(__instance, out GridViewport? grid) || grid == null)
                return;
            IList<Item>? items = RefreshInventorySource(__instance, grid);
            if (items != null)
            {
                grid.PerformHoverAction(x, y, items);
            }
        }

        private static bool InventoryMenuLeftClickPrefix(
            InventoryMenu __instance,
            ref Item __result,
            int x,
            int y,
            Item toPlace,
            bool playSound
        )
        {
            if (!ViewportRegistry.TryGet(__instance, out GridViewport? grid) || grid == null)
                return true;
            IList<Item>? items = RefreshInventorySource(__instance, grid);
            if (items != null)
            {
                if (grid.ReceiveLeftClick(x, y, items))
                {
                    __result = toPlace;
                    return false;
                }
            }
            return true;
        }

        private static bool InventoryMenuReceiveLeftClickPrefix(
            InventoryMenu __instance,
            int x,
            int y,
            bool playSound
        )
        {
            if (!ViewportRegistry.TryGet(__instance, out GridViewport? grid) || grid == null)
                return true;
            IList<Item>? items = RefreshInventorySource(__instance, grid);
            if (items != null)
            {
                if (grid.ReceiveLeftClick(x, y, items))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryGetPageState(
            InventoryPage page,
            out GridViewport? state,
            bool create = false
        )
        {
            state = null;
            if (StardewMenuFields.InventoryPageInventory.GetValue(page) is not InventoryMenu inventoryMenu)
                return false;
            if (StardewMenuFields.ActualInventory.GetValue(inventoryMenu) is not IList<Item> actualInventory)
                return false;

            IList<Item> fullInventory = actualInventory is ScrollableInventoryList scrollable
                ? scrollable.Underlying
                : actualInventory;
            if (fullInventory != (Game1.player?.Items))
                return false;
            if (!InventoryGridMetrics.PlayerHasExpandedInventory(fullInventory))
                return false;

            int requiredRows = GetInventoryPageEffectiveRequiredRows(inventoryMenu, null);
            if (requiredRows <= InventoryGridMetrics.DefaultRowCount)
                return false;

            state =
                create ? ViewportRegistry.GetOrCreate(inventoryMenu)
                : ViewportRegistry.TryGet(inventoryMenu, out var existing) ? existing
                : null;
            return state is not null;
        }
    

    }
}
