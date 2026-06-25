using System.Reflection;
using System.Runtime.CompilerServices;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Collections;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout;
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
using static StardewValley.Menus.InventoryMenu;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher
{
    internal class InventoryMenuPatcher : BasePatcher
    {
        private const int ArrowIdUp = 11001;
        private const int ArrowIdDown = 11002;

        private static readonly ConditionalWeakTable<InventoryMenu, GridViewport> GridViewports =
            new();
        private static readonly ConditionalWeakTable<IClickableMenu, BoxedInt> ChestLayoutHashes =
            new();
        private static readonly ConditionalWeakTable<
            IClickableMenu,
            MenuCachedFields
        > MenuFieldsCache = new();

        public static IClickableMenu? CurrentParentMenu = null;

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
        }

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
            GridViewports.TryGetValue(inventoryMenu, out var grid);
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
                if (c == null || rightColumnSet.Contains(c))
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
                if (comp.myID == -1)
                    comp.myID = dynamicId++;
            }
            foreach (var comp in bottomComponents)
            {
                if (comp.myID == -1)
                    comp.myID = dynamicId++;
            }

            // Encontra o upNeighborID original que aponta para um componente acima do inventário (abas do menu)
            int originalUpNeighbor = -1;
            foreach (var c in rightColumn)
            {
                if (c.upNeighborID != -1)
                {
                    var target = activeComponents.FirstOrDefault(tc =>
                        tc != null && tc.myID == c.upNeighborID
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
                && activeComponents.Any(c => c != null && c.myID == originalUpNeighbor)
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
                            // InventoryPage: the up-arrow has no component above it. Pressing up
                            // here must stop instead of wrapping/descending into another button.
                            comp.upNeighborID = -1;
                            var firstLogical = logicalColumn.FirstOrDefault();
                            comp.downNeighborID = firstLogical != null ? firstLogical.myID : -1;
                            comp.leftNeighborID = -1;
                            comp.rightNeighborID = -1;
                            continue;
                        }

                        if (downArrow != null && ReferenceEquals(comp, downArrow))
                        {
                            var lastLogical = logicalColumn.LastOrDefault();
                            comp.upNeighborID = lastLogical != null ? lastLogical.myID : -1;
                            // InventoryPage: pressing DOWN on the down-arrow must reach the trash can.
                            // The arrow's own action still scrolls when pressing A; this is only directional navigation.
                            comp.downNeighborID = page.trashCan != null ? page.trashCan.myID : -1;
                            comp.leftNeighborID = -1;
                            comp.rightNeighborID = -1;
                            if (page.trashCan != null)
                                page.trashCan.upNeighborID = comp.myID;
                            continue;
                        }

                        int logicalIndex = logicalColumn.IndexOf(comp);

                        if (logicalIndex >= 0)
                        {
                            if (logicalIndex > 0)
                                comp.upNeighborID = logicalColumn[logicalIndex - 1].myID;
                            else if (upArrow != null && column.Contains(upArrow))
                                comp.upNeighborID = upArrow.myID;
                            else
                                comp.upNeighborID = -1;

                            if (logicalIndex < logicalColumn.Count - 1)
                                comp.downNeighborID = logicalColumn[logicalIndex + 1].myID;
                            else if (downArrow != null && showScrollButtons && column.Contains(downArrow))
                                comp.downNeighborID = downArrow.myID;
                            else if (page.trashCan != null)
                                comp.downNeighborID = page.trashCan.myID;
                            else
                                comp.downNeighborID = -1;
                        }
                        else
                        {
                            comp.upNeighborID = i > 0 ? column[i - 1].myID : -1;
                            comp.downNeighborID = i < column.Count - 1 ? column[i + 1].myID : -1;
                        }

                        if (colIndex > 0)
                        {
                            var leftButton = PickSameLogicalRow(nonArrowColumns[colIndex - 1], logicalIndex, comp.bounds.Center.Y);
                            if (leftButton != null)
                                comp.leftNeighborID = leftButton.myID;
                        }
                        else
                        {
                            var closestSlot = FindClosestByY(rightmostSlots, comp.bounds.Center.Y);
                            if (closestSlot != null)
                                comp.leftNeighborID = closestSlot.myID;
                        }

                        if (colIndex < rightColumns.Count - 1)
                        {
                            var rightButton = PickSameLogicalRow(nonArrowColumns[colIndex + 1], logicalIndex, comp.bounds.Center.Y);
                            if (rightButton != null)
                                comp.rightNeighborID = rightButton.myID;
                        }
                    }
                }

                var primaryRightColumn = nonArrowColumns[0].Count > 0
                    ? nonArrowColumns[0]
                    : rightColumns[0];
                foreach (var slot in rightmostSlots)
                {
                    var closestRightComp = FindClosestByY(primaryRightColumn, slot.bounds.Center.Y);
                    if (closestRightComp != null)
                        slot.rightNeighborID = closestRightComp.myID;
                }
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
                    slot.downNeighborID = bestDown.myID;
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
                    c.upNeighborID = bestUpComp.myID;
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
                        c.upNeighborID = closestSlot.myID;
                    }
                }

                if (bestDownComp != null)
                {
                    c.downNeighborID = bestDownComp.myID;
                }

                if (bestLeftComp != null)
                {
                    c.leftNeighborID = bestLeftComp.myID;
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
                        c.leftNeighborID = closestSlot.myID;
                    }
                }

                if (bestRightComp != null)
                {
                    c.rightNeighborID = bestRightComp.myID;
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

        private static void IClickableMenuUpdatePositionPostfix(IClickableMenu __instance)
        {
            if (__instance is ItemGrabMenu or ShopMenu or MenuWithInventory or MuseumMenu)
            {
                RepositionAndWireSideButtons(__instance);
            }
        }

        private static void RepositionAndWireSideButtons(IClickableMenu menu)
        {
            if (menu == null)
                return;
            if (menu is ShopMenu shopMenu)
            {
                InventoryMenuLayoutHelpers.AdjustShopMenuScrollLayout(
                    shopMenu,
                    StardewMenuFields.ShopMenuScrollBarRunner
                );
            }

            var menus = MenuComponentFinder.FindInventoryMenus(menu);
            if (menus.Count == 0)
                return;
            var orderedMenus = menus
                .Where(m => m?.inventory != null && m.inventory.Count > 0)
                .OrderBy(m => m.yPositionOnScreen)
                .ToList();
            if (orderedMenus.Count == 0)
                return;

            var chestMenu = orderedMenus.First();
            var playerMenu = orderedMenus.Last();
            var chestSlots = chestMenu.inventory;
            var playerSlots = playerMenu.inventory;
            if (
                chestSlots == null
                || chestSlots.Count == 0
                || playerSlots == null
                || playerSlots.Count == 0
            )
                return;

            int chestColumns = chestMenu.capacity / chestMenu.rows;
            if (chestColumns <= 0)
                chestColumns = InventoryGridMetrics.DefaultColumnCount;
            int playerColumns = playerMenu.capacity / playerMenu.rows;
            if (playerColumns <= 0)
                playerColumns = InventoryGridMetrics.DefaultColumnCount;

            var playerGrid = GridViewports.GetValue(playerMenu, m => new GridViewport(m));
            playerGrid.CustomArrowLayout = true;
            playerGrid.UpArrow.myID = ArrowIdUp;
            playerGrid.DownArrow.myID = ArrowIdDown;
            bool showScrollButtons = playerGrid.FullInventory != null
                && playerGrid.FullInventory.Count > playerMenu.capacity;
            playerGrid.SetScrollButtonsClickable(menu, showScrollButtons);

            var fields = MenuFieldsCache.GetValue(
                menu,
                m =>
                {
                    var cBtn = MenuComponentFinder.FindFieldContaining(
                        m,
                        "colorPickerToggleButton"
                    );
                    var fBtn = MenuComponentFinder.FindFieldContaining(
                        m,
                        "fillStacksButton"
                    );
                    var oBtn =
                        MenuComponentFinder.FindFieldContaining(m, "organizeButton")
                        ?? MenuComponentFinder.FindFieldContaining(
                            m,
                            "organizeStashButton"
                        );
                    var oK = MenuComponentFinder.FindFieldContaining(m, "okButton");
                    var sBtn = MenuComponentFinder.FindFieldContaining(m, "specialButton");
                    var tBtn = MenuComponentFinder.FindFieldContaining(m, "trashCan");

                    if (
                        cBtn != null
                        && (cBtn == m.upperRightCloseButton || cBtn.name == "upperRightCloseButton")
                    )
                        cBtn = null;
                    if (
                        fBtn != null
                        && (fBtn == m.upperRightCloseButton || fBtn.name == "upperRightCloseButton")
                    )
                        fBtn = null;
                    if (
                        oBtn != null
                        && (oBtn == m.upperRightCloseButton || oBtn.name == "upperRightCloseButton")
                    )
                        oBtn = null;
                    if (
                        oK != null
                        && (oK == m.upperRightCloseButton || oK.name == "upperRightCloseButton")
                    )
                        oK = null;
                    if (
                        sBtn != null
                        && (sBtn == m.upperRightCloseButton || sBtn.name == "upperRightCloseButton")
                    )
                        sBtn = null;
                    if (
                        tBtn != null
                        && (tBtn == m.upperRightCloseButton || tBtn.name == "upperRightCloseButton")
                    )
                        tBtn = null;

                    var cPicker = MenuComponentFinder.FindColorPicker(m);
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
            );

            var colorBtn = fields.ColorBtn;
            var fillBtn = fields.FillBtn;
            var organizeBtn = fields.OrganizeBtn;
            var specialBtn = fields.SpecialBtn;
            var okBtn = fields.OkBtn;
            var trashBtn = fields.TrashBtn;
            var pickerToggle = fields.PickerToggle;
            var colorPicker = fields.ColorPicker;
            var preferredSide = InventoryMenuLayoutHelpers.GetPreferredSide(menu);
            int preferredSideOffsetPixels =
                InventoryMenuLayoutHelpers.GetPreferredSideOffsetPixels(menu);
            int? arrowAnchorCenterXOverride = null;
            var arrowAnchorComponentOverride =
                InventoryMenuLayoutHelpers.GetArrowAnchorComponentOverride(menu);
            int anchorCenterX =
                (okBtn ?? trashBtn ?? organizeBtn ?? fillBtn ?? colorBtn ?? specialBtn)?.bounds.Center.X ?? 0;
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
                showScrollButtons,
                menu.allClickableComponents?.Count ?? 0
            );

            // Keep the vanilla drop-item target reachable even when the side-column layout
            // did not change this frame. Other mods and populateClickableComponentList can
            // rebuild neighbors after our last layout pass.
            MenuWithInventoryDropNavigation.Preserve(menu, orderedMenus);

            if (!UpdateCachedLayoutHash(menu, layoutHash))
                return;

            playerGrid.LayoutSideButtons(
                new GridViewport.SideButtonLayoutContext
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
                    PickerToggle = pickerToggle,
                    ColorPicker = colorPicker,
                    ShowScrollButtons = showScrollButtons,
                }
            );

            MenuWithInventoryDropNavigation.Preserve(menu, orderedMenus);
        }

        private static bool UpdateCachedLayoutHash(IClickableMenu menu, int hash)
        {
            var cached = ChestLayoutHashes.GetValue(menu, _ => new BoxedInt(int.MinValue));
            if (cached.Value == hash)
            {
                return false;
            }

            cached.Value = hash;
            return true;
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
            bool showScrollButtons,
            int clickableCount
        )
        {
            var hash = new HashCode();
            hash.Add(anchorCenterX);
            hash.Add(clickableCount);
            hash.Add(showScrollButtons);
            hash.Add(chestSlots[0].bounds.Y);
            hash.Add(playerSlots[0].bounds.Y);
            hash.Add(playerSlots[playerSlots.Count - 1].bounds.Y);
            hash.Add(colorBtn != null ? RuntimeHelpers.GetHashCode(colorBtn) : 0);
            hash.Add(fillBtn != null ? RuntimeHelpers.GetHashCode(fillBtn) : 0);
            hash.Add(organizeBtn != null ? RuntimeHelpers.GetHashCode(organizeBtn) : 0);
            hash.Add(specialBtn != null ? RuntimeHelpers.GetHashCode(specialBtn) : 0);
            hash.Add(okBtn != null ? RuntimeHelpers.GetHashCode(okBtn) : 0);
            hash.Add(trashBtn != null ? RuntimeHelpers.GetHashCode(trashBtn) : 0);

            foreach (var button in chestTopColumn)
            {
                hash.Add(RuntimeHelpers.GetHashCode(button));
            }

            return hash.ToHashCode();
        }

        private static void ItemGrabMenuDrawPrefix(ItemGrabMenu __instance, SpriteBatch b)
        {
            RepositionAndWireSideButtons(__instance);
        }

        private static void ShopMenuDrawPrefix(ShopMenu __instance, SpriteBatch b)
        {
            InventoryMenuLayoutHelpers.AdjustShopMenuScrollLayout(
                __instance,
                StardewMenuFields.ShopMenuScrollBarRunner
            );
            RepositionAndWireSideButtons(__instance);
        }

        private static void MuseumMenuDrawPrefix(MuseumMenu __instance, SpriteBatch b)
        {
            RepositionAndWireSideButtons(__instance);
        }

        private static void updatePositionPostfix(ShopMenu __instance)
        {
            if (Game1.player.maxItems.Value > InventoryGridMetrics.DefaultMaxItems)
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
            if (Game1.player.maxItems.Value > InventoryGridMetrics.DefaultMaxItems)
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

            if (Game1.player.maxItems.Value > InventoryGridMetrics.DefaultMaxItems)
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
            if (Game1.player.maxItems.Value > InventoryGridMetrics.DefaultMaxItems)
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
            if (Game1.player.maxItems.Value > InventoryGridMetrics.DefaultMaxItems)
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
                var grid = GridViewports.GetValue(__instance, m => new GridViewport(m));
                grid.Depth++;
                if (grid.OriginalMaxItems == null)
                {
                    grid.OriginalMaxItems = Game1.player.maxItems.Value;
                }
                Game1.player.maxItems.Value = scrollList.Count;
                return;
            }

            var gridViewport = GridViewports.GetValue(__instance, m => new GridViewport(m));
            gridViewport.Depth++;
            gridViewport.FullInventory = currentInventory;

            int columns = __instance.capacity / __instance.rows;
            if (columns <= 0)
                columns = InventoryGridMetrics.DefaultColumnCount;

            int totalRows = Math.Max(0, (currentInventory.Count + columns - 1) / columns);
            int maxScrollRow = Math.Max(0, totalRows - __instance.rows);
            gridViewport.ScrollRow = Math.Clamp(gridViewport.ScrollRow, 0, maxScrollRow);

            if (currentInventory.Count <= __instance.capacity)
                return;

            gridViewport.OriginalInventory = currentInventory;
            var scrollableList = new ScrollableInventoryList(
                currentInventory,
                gridViewport.ScrollRow * columns,
                __instance.capacity
            );
            StardewMenuFields.ActualInventory.SetValue(__instance, scrollableList);

            if (gridViewport.OriginalMaxItems == null)
            {
                gridViewport.OriginalMaxItems = Game1.player.maxItems.Value;
            }
            Game1.player.maxItems.Value = scrollableList.Count;
        }

        private static void InventoryMenuMethodPostfix(InventoryMenu __instance)
        {
            if (!GridViewports.TryGetValue(__instance, out GridViewport? grid))
                return;

            grid.Depth--;
            if (grid.Depth > 0)
                return;

            if (grid.OriginalMaxItems != null)
            {
                Game1.player.maxItems.Value = grid.OriginalMaxItems.Value;
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
            var grid = GridViewports.GetValue(inventoryMenu, m => new GridViewport(m));

            if (!TryGetPageState(page, out GridViewport? stateGrid, create: true) || stateGrid == null)
                return;

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
            grid.LayoutInventoryPageButtons(page, slots, organizeButton, showScrollButtons);
        }

        private static bool InventoryPageShouldShowScrollButtons(
            InventoryPage page,
            InventoryMenu inventoryMenu,
            GridViewport? grid
        )
        {
            IList<Item>? items = grid?.OriginalInventory ?? grid?.FullInventory;
            if (items != null)
                return items.Count > inventoryMenu.capacity;

            if (StardewMenuFields.ActualInventory.GetValue(inventoryMenu) is IList<Item> currentInventory)
                return currentInventory.Count > inventoryMenu.capacity;

            return Game1.player != null && Game1.player.maxItems.Value > inventoryMenu.capacity;
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

            if (GridViewports.TryGetValue(inventoryMenu, out var grid) && grid != null)
            {
                IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
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
            if (!TryGetPageState(inventoryPage, out GridViewport? grid) || grid == null)
                return;

            // Keep the InventoryPage side-column links current from the first visible frame.
            // Some mods rebuild/adjust side buttons after construction without changing the
            // component count, so a count-only hash can leave stale first-frame navigation.
            LayoutScrollButtons(inventoryPage, grid);
            if (__instance.allClickableComponents != null)
            {
                WireGamepadNavigation(inventoryPage, __instance.allClickableComponents);
            }

            if (GridViewports.TryGetValue(inventoryMenu, out var viewport) && viewport != null)
            {
                IList<Item>? items = viewport.OriginalInventory ?? viewport.FullInventory;
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

        private static bool MenuReceiveScrollWheelActionPrefix(
            IClickableMenu __instance,
            int direction
        )
        {
            var menus = MenuComponentFinder.FindInventoryMenus(__instance);
            foreach (var menu in menus)
            {
                if (
                    MenuComponentFinder.IsMouseTargetingInventoryArea(
                        menu,
                        GridViewports.TryGetValue(menu, out GridViewport? g) ? g : null,
                        Game1.getOldMouseX(),
                        Game1.getOldMouseY()
                    )
                )
                {
                    if (GridViewports.TryGetValue(menu, out GridViewport? grid) && grid != null)
                    {
                        IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
                        if (items != null && grid.ReceiveScrollWheelAction(direction, items))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private static bool MenuReceiveLeftClickPrefix(
            IClickableMenu __instance,
            int x,
            int y,
            bool playSound
        )
        {
            var menus = MenuComponentFinder.FindInventoryMenus(__instance);
            foreach (var menu in menus)
            {
                if (GridViewports.TryGetValue(menu, out GridViewport? grid) && grid != null)
                {
                    IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
                    if (items != null && grid.ReceiveLeftClick(x, y, items))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static void MenuUpdatePostfix(IClickableMenu __instance, GameTime time)
        {
            if (__instance is ItemGrabMenu or ShopMenu or MenuWithInventory or MuseumMenu)
            {
                RepositionAndWireSideButtons(__instance);
            }

            var menus = MenuComponentFinder.FindInventoryMenus(__instance);
            foreach (var menu in menus)
            {
                if (GridViewports.TryGetValue(menu, out GridViewport? grid) && grid != null)
                {
                    IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
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
                }
            }
        }

        private static bool MenuReceiveGamePadButtonPrefix(
            IClickableMenu __instance,
            Buttons button
        )
        {
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

            if (
                button != Buttons.DPadDown
                && button != Buttons.DPadUp
                && button != Buttons.LeftThumbstickDown
                && button != Buttons.LeftThumbstickUp
            )
                return true;

            ClickableComponent snapped = __instance.currentlySnappedComponent;
            if (snapped == null)
                return true;

            var menus = MenuComponentFinder.FindInventoryMenus(__instance);
            foreach (var menu in menus)
            {
                if (menu.inventory != null && menu.inventory.Contains(snapped))
                {
                    int index = menu.inventory.IndexOf(snapped);
                    int columns = menu.capacity / menu.rows;
                    if (columns <= 0)
                        columns = InventoryGridMetrics.DefaultColumnCount;

                    int row = index / columns;

                    if (
                        (button == Buttons.DPadDown || button == Buttons.LeftThumbstickDown)
                        && row == menu.rows - 1
                    )
                    {
                        if (GridViewports.TryGetValue(menu, out GridViewport? grid) && grid != null)
                        {
                            IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
                            if (items != null && grid.CanScroll(items, 1))
                            {
                                grid.Scroll(items, 1);
                                return false; // Handled, block default snap
                            }
                        }
                    }
                    else if (
                        (button == Buttons.DPadUp || button == Buttons.LeftThumbstickUp)
                        && row == 0
                    )
                    {
                        if (GridViewports.TryGetValue(menu, out GridViewport? grid) && grid != null)
                        {
                            IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
                            if (items != null && grid.CanScroll(items, -1))
                            {
                                grid.Scroll(items, -1);
                                return false; // Handled, block default snap
                            }
                        }
                    }
                }
            }
            return true;
        }

        private static void InventoryMenuDrawPostfix(InventoryMenu __instance, SpriteBatch b)
        {
            if (!GridViewports.TryGetValue(__instance, out GridViewport? grid) || grid == null)
                return;
            IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
            if (items != null)
            {
                grid.Draw(b, items);
            }
        }

        private static void InventoryMenuPerformHoverActionPostfix(
            InventoryMenu __instance,
            int x,
            int y
        )
        {
            if (!GridViewports.TryGetValue(__instance, out GridViewport? grid) || grid == null)
                return;
            IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
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
            if (!GridViewports.TryGetValue(__instance, out GridViewport? grid) || grid == null)
                return true;
            IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
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
            if (!GridViewports.TryGetValue(__instance, out GridViewport? grid) || grid == null)
                return true;
            IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
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
            if (Game1.player?.maxItems.Value <= InventoryGridMetrics.DefaultMaxItems)
                return false;
            if (StardewMenuFields.InventoryPageInventory.GetValue(page) is not InventoryMenu inventoryMenu)
                return false;
            if (StardewMenuFields.ActualInventory.GetValue(inventoryMenu) is not IList<Item> actualInventory)
                return false;

            IList<Item> fullInventory = actualInventory is ScrollableInventoryList scrollable
                ? scrollable.Underlying
                : actualInventory;
            if (
                fullInventory != (Game1.player?.Items)
                || InventoryGridMetrics.GetTotalRows(fullInventory) <= inventoryMenu.rows
            )
                return false;

            state =
                create ? GridViewports.GetValue(inventoryMenu, m => new GridViewport(m))
                : GridViewports.TryGetValue(inventoryMenu, out var existing) ? existing
                : null;
            return state is not null;
        }

    }
}
