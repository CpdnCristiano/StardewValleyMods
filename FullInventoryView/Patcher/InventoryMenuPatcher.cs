using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.Common.Patching;
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
        private const int VisibleRows = MAX_ROW_COUNT;
        private const int DEFAULT_ROW_HEIGHT = 64;
        private const int DEFAULT_COLUMN_COUNT = 12;
        private const int DEFAULT_ROW_COUNT = 3;
        private const int MAX_ROW_COUNT = 7;
        private const int DEFAULT_MAX_ITEMS = 36;

        private const int ArrowIdUp = 11001;
        private const int ArrowIdDown = 11002;

        private static readonly ConditionalWeakTable<InventoryMenu, InventoryScrollState> InventoryStates = new();
        private static readonly ConditionalWeakTable<InventoryPage, PageScrollState> PageStates = new();

        private static readonly FieldInfo InventoryField = AccessTools.Field(typeof(InventoryMenu), "inventory");
        private static readonly FieldInfo ActualInventoryField = AccessTools.Field(typeof(InventoryMenu), "actualInventory");
        private static readonly FieldInfo InventoryPageInventoryField = AccessTools.Field(typeof(InventoryPage), "inventory");
        private static readonly FieldInfo InventoryPageOrganizeButtonField = AccessTools.Field(typeof(InventoryPage), "organizeButton");

        private static readonly Rectangle UpArrowSourceRect = new(421, 459, 11, 12);
        private static readonly Rectangle DownArrowSourceRect = new(421, 472, 11, 12);

        private static int GetRows()
        {
            if (Game1.player.maxItems.Value <= DEFAULT_MAX_ITEMS)
            {
                return DEFAULT_ROW_COUNT;
            }
            int rows = Game1.player.maxItems.Value / DEFAULT_COLUMN_COUNT;
            return Math.Min(rows, MAX_ROW_COUNT);
        }

        private static int GetTotalRows(IList<Item> inventory)
        {
            return Math.Max(0, (inventory.Count + DEFAULT_COLUMN_COUNT - 1) / DEFAULT_COLUMN_COUNT);
        }

        private static int GetExtraRow()
        {
            return Math.Max(0, GetRows() - DEFAULT_ROW_COUNT);
        }

        public static int GetExtraHeight()
        {
            return (GetExtraRow() * DEFAULT_ROW_HEIGHT)
                + ((GetExtraRow() - 1) * IClickableMenu.spaceBetweenTabs);
        }

        public static int GetDoubleExtraHeight()
        {
            return GetExtraHeight() * 2;
        }



        private static int GetCapacity()
        {
            return VisibleRows * DEFAULT_COLUMN_COUNT;
        }

        public static int GetBillboardOffset()
        {
            int extra = GetExtraHeight();
            if (extra <= 0) return 0;

            return extra - DEFAULT_ROW_HEIGHT;
        }

        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            harmony.Patch(
                original: this.RequireConstructor<InventoryMenu>(
                    new Type[]
                    {
                        typeof(int), typeof(int), typeof(bool), typeof(IList<Item>),
                        typeof(highlightThisItem), typeof(int), typeof(int),
                        typeof(int), typeof(int), typeof(bool),
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
                        typeof(int), typeof(int), typeof(int), typeof(int),
                        typeof(bool), typeof(bool), typeof(List<IInventory>),
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

            PatchInventoryMenuMethods(harmony);
            PatchInventoryPageMethods(harmony);
        }

        private void PatchInventoryMenuMethods(Harmony harmony)
        {
            string[] methodNames =
            {
                "draw", "leftClick", "rightClick", "hover", "receiveLeftClick",
                "performHoverAction", "getItemAt", "getItemFromClickableComponent", "tryToAddItem"
            };

            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(InventoryMenu)))
            {
                if (!methodNames.Contains(method.Name)) continue;

                harmony.Patch(
                    original: method,
                    prefix: this.GetHarmonyMethod(nameof(InventoryMenuMethodPrefix)),
                    postfix: this.GetHarmonyMethod(nameof(InventoryMenuMethodPostfix))
                );
            }
        }

        private void PatchInventoryPageMethods(Harmony harmony)
        {
            harmony.Patch(
                original: this.RequireMethod<InventoryPage>(nameof(InventoryPage.receiveLeftClick)),
                prefix: this.GetHarmonyMethod(nameof(InventoryPageReceiveLeftClickPrefix))
            );
            harmony.Patch(
                original: this.RequireMethod<InventoryPage>(nameof(InventoryPage.performHoverAction)),
                postfix: this.GetHarmonyMethod(nameof(InventoryPagePerformHoverActionPostfix))
            );
            harmony.Patch(
                original: this.RequireMethod<InventoryPage>(nameof(InventoryPage.draw), new Type[] { typeof(SpriteBatch) }),
                postfix: this.GetHarmonyMethod(nameof(InventoryPageDrawPostfix))
            );
            harmony.Patch(
                original: this.RequireMethod<GameMenu>(nameof(GameMenu.receiveScrollWheelAction), new Type[] { typeof(int) }),
                prefix: this.GetHarmonyMethod(nameof(GameMenuReceiveScrollWheelActionPrefix))
            );
            harmony.Patch(
                original: this.RequireMethod<GameMenu>(nameof(GameMenu.update), new Type[] { typeof(GameTime) }),
                postfix: this.GetHarmonyMethod(nameof(GameMenuUpdatePostfix), (int)Priority.Last)
            );

            // Injeção de navegação na página do inventário com prioridade Last (roda por último)
            harmony.Patch(
                original: this.RequireMethod<InventoryPage>(nameof(InventoryPage.setUpForGamePadMode)),
                postfix: this.GetHarmonyMethod(nameof(InventoryPageSetUpForGamePadModePostfix), (int)Priority.Last)
            );

            // Injeção MASTER no GameMenu com prioridade Last
            harmony.Patch(
                original: this.RequireMethod<GameMenu>(nameof(GameMenu.setUpForGamePadMode)),
                postfix: this.GetHarmonyMethod(nameof(GameMenuSetUpForGamePadModePostfix), (int)Priority.Last)
            );

            // População com prioridade Last para rodar depois de todos os outros mods
            harmony.Patch(
                original: this.RequireMethod<IClickableMenu>(nameof(IClickableMenu.populateClickableComponentList)),
                postfix: this.GetHarmonyMethod(nameof(PopulateClickableComponentListPostfix), (int)Priority.Last)
            );

            harmony.Patch(
                original: this.RequireMethod<InventoryPage>(nameof(InventoryPage.receiveGamePadButton)),
                prefix: this.GetHarmonyMethod(nameof(InventoryPageReceiveGamePadButtonPrefix))
            );
        }

        private static void GameMenuSetUpForGamePadModePostfix(GameMenu __instance)
        {
            if (__instance.allClickableComponents == null) return;
            if (__instance.GetCurrentPage() is not InventoryPage page) return;

            EnsureScrollButtons(page);

            if (TryGetPageState(page, out PageScrollState? state) && state != null)
            {
                if (state.UpArrow != null && !__instance.allClickableComponents.Contains(state.UpArrow)) __instance.allClickableComponents.Add(state.UpArrow);
                if (state.DownArrow != null && !__instance.allClickableComponents.Contains(state.DownArrow)) __instance.allClickableComponents.Add(state.DownArrow);
            }

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
            if (__instance.allClickableComponents == null) return;

            EnsureScrollButtons(__instance);
            WireGamepadNavigation(__instance, __instance.allClickableComponents);
        }

        internal static void WireGamepadNavigation(InventoryPage page, List<ClickableComponent> activeComponents)
        {
            if (activeComponents == null || activeComponents.Count == 0) return;
            if (InventoryPageInventoryField.GetValue(page) is not InventoryMenu inventoryMenu) return;
            if (InventoryField.GetValue(inventoryMenu) is not List<ClickableComponent> slots || slots.Count == 0) return;

            // Use a HashSet for O(1) lookups of components currently in activeComponents
            var activeSet = new HashSet<ClickableComponent>(activeComponents);

            // Sincroniza qualquer componente tardio adicionado por outros mods na página para a lista mestre
            if (Game1.activeClickableMenu is GameMenu gameMenu && gameMenu.allClickableComponents == activeComponents)
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

            // 1. Coleta as setas de rolagem do estado se existirem
            PageStates.TryGetValue(page, out PageScrollState? state);
            if (state != null)
            {
                if (state.UpArrow != null && activeSet.Add(state.UpArrow)) activeComponents.Add(state.UpArrow);
                if (state.DownArrow != null && activeSet.Add(state.DownArrow)) activeComponents.Add(state.DownArrow);
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

            // Always keep arrows and organize button in rightColumn
            if (state != null)
            {
                if (state.UpArrow != null) rightColumn.Add(state.UpArrow);
                if (state.DownArrow != null) rightColumn.Add(state.DownArrow);
            }
            if (page.organizeButton != null) rightColumn.Add(page.organizeButton);

            var rightColumnSet = new HashSet<ClickableComponent>(rightColumn);

            // Now categorize the rest of activeComponents
            foreach (var c in activeComponents)
            {
                if (c == null || rightColumnSet.Contains(c)) continue;
                if (c.name == "charPortrait" || c == page.portrait) continue; // Portrait is not focusable, bypass it

                if (c.bounds.Top >= inventoryBottom - 16)
                {
                    if (c == page.trashCan)
                    {
                        if (c.bounds.Center.Y >= inventoryBottom + 16)
                        {
                            bottomComponents.Add(c);
                        }
                        else
                        {
                            rightColumn.Add(c);
                            rightColumnSet.Add(c);
                        }
                    }
                    else
                    {
                        bottomComponents.Add(c);
                    }
                }
                else if (c.bounds.Center.X > rightEdge && c.bounds.Center.X < rightEdge + 300)
                {
                    rightColumn.Add(c);
                    rightColumnSet.Add(c);
                }
            }

            // Ordena todos por Y
            rightColumn = rightColumn.OrderBy(c => c.bounds.Center.Y).ToList();

            // 3. O SEGREDO: Atribuir IDs válidos aos botões de mods (que normalmente usam -1).
            int dynamicId = 150000;
            foreach (var comp in rightColumn)
            {
                if (comp.myID == -1) comp.myID = dynamicId++;
            }
            foreach (var comp in bottomComponents)
            {
                if (comp.myID == -1) comp.myID = dynamicId++;
            }

            // Encontra o upNeighborID original para guiar até as abas do menu
            int originalUpNeighbor = -1;
            foreach (var c in rightColumn)
            {
                if (c.upNeighborID >= 12340 && c.upNeighborID <= 12350)
                {
                    originalUpNeighbor = c.upNeighborID;
                    break;
                }
            }
            if (originalUpNeighbor == -1 && page.organizeButton != null)
            {
                originalUpNeighbor = page.organizeButton.upNeighborID;
            }
            int upNeighbor = originalUpNeighbor != -1 ? originalUpNeighbor : 12340;

            // Pre-calcula os slots mais à direita da grade do inventário
            var rightmostSlots = new List<ClickableComponent>();
            for (int idx = DEFAULT_COLUMN_COUNT - 1; idx < slots.Count; idx += DEFAULT_COLUMN_COUNT)
            {
                rightmostSlots.Add(slots[idx]);
            }

            // Pre-calcula os slots da última linha do inventário
            int visibleRows = slots.Count / DEFAULT_COLUMN_COUNT;
            int lastRowStart = (visibleRows - 1) * DEFAULT_COLUMN_COUNT;
            var lastRowSlots = new List<ClickableComponent>();
            for (int col = 0; col < DEFAULT_COLUMN_COUNT; col++)
            {
                int idx = lastRowStart + col;
                if (idx < slots.Count)
                {
                    lastRowSlots.Add(slots[idx]);
                }
            }

            // 4. Mapeamento Vertical e Horizontal (Esquerda) Cirúrgico para a coluna direita
            for (int i = 0; i < rightColumn.Count; i++)
            {
                var comp = rightColumn[i];

                comp.upNeighborID = i > 0 ? rightColumn[i - 1].myID : upNeighbor;
                comp.downNeighborID = i < rightColumn.Count - 1 ? rightColumn[i + 1].myID : -1;

                ClickableComponent? closestSlot = null;
                int minDistance = int.MaxValue;
                int compY = comp.bounds.Center.Y;
                foreach (var s in rightmostSlots)
                {
                    int dist = Math.Abs(s.bounds.Center.Y - compY);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestSlot = s;
                    }
                }

                if (closestSlot != null)
                {
                    comp.leftNeighborID = closestSlot.myID;
                }
            }

            // 5. Forçar o inventário a apontar para o botão MAIS PRÓXIMO (Anular o Hardcode do Jogo)
            foreach (var slot in rightmostSlots)
            {
                ClickableComponent? closestRightComp = null;
                int minDistance = int.MaxValue;
                int slotY = slot.bounds.Center.Y;
                foreach (var c in rightColumn)
                {
                    int dist = Math.Abs(c.bounds.Center.Y - slotY);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestRightComp = c;
                    }
                }

                if (closestRightComp != null)
                {
                    slot.rightNeighborID = closestRightComp.myID;
                }
            }

            // 6. Corrigir navegação vertical entre as linhas do inventário e os componentes de baixo
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

            // 7. Mapeamento inteligente de vizinhos entre os componentes de baixo
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
                    if (c2 == c) continue;

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

        private static bool drawCurrencyPrefix(ShopMenu __instance, SpriteBatch b)
        {
            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
            {
                FieldInfo? _isStorageShopField = typeof(ShopMenu).GetField(
                    "_isStorageShop",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (_isStorageShopField == null)
                {
                    Log.Error("Field '_isStorageShop' not found in ShopMenu.");
                    return true;
                }
                var isStorageShop = _isStorageShopField.GetValue(__instance) as bool? ?? false;
                if (!isStorageShop && __instance.currency == 0)
                {
                    var extraHeight =
                        GetExtraHeight() - ((GetExtraRow() - 1) * IClickableMenu.spaceBetweenTabs);
                    if (extraHeight < 0)
                    {
                        extraHeight = 0;
                    }
                    Game1.dayTimeMoneyBox.drawMoneyBox(
                        b,
                        __instance.xPositionOnScreen - 36,
                        __instance.yPositionOnScreen
                            + __instance.height
                            - __instance.inventory.height
                            - 12
                            + extraHeight
                    );
                }
                return false;
            }
            return true;
        }

        public static IEnumerable<CodeInstruction> drawTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count - 5; i++)
            {
                if (
                    codes[i].opcode == OpCodes.Ldarg_0
                    && codes[i + 1].opcode == OpCodes.Ldfld
                    && ((FieldInfo)codes[i + 1].operand).Name == "height"
                    && codes[i + 2].opcode == OpCodes.Ldc_I4
                    && (int)codes[i + 2].operand == 448
                    && codes[i + 3].opcode == OpCodes.Sub
                    && codes[i + 4].opcode == OpCodes.Ldc_I4_S
                    && (sbyte)codes[i + 4].operand == 20
                    && codes[i + 5].opcode == OpCodes.Add
                )
                {
                    codes.Insert(
                        i + 6,
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InventoryMenuPatcher), nameof(GetExtraHeight)))
                    );
                    codes.Insert(i + 7, new CodeInstruction(OpCodes.Add));
                    Log.Debug("Patching ShopMenu to add extra height to the shop menu");
                    break;
                }
            }
            return codes;
        }

        private static void initializeShippingBinPostfix(ItemGrabMenu __instance)
        {
            if (__instance.lastShippedHolder is not null)
            {
                __instance.lastShippedHolder.bounds.Y -= GetExtraHeight() / 2;
            }
        }

        private static void updatePositionPostfix(ShopMenu __instance)
        {
            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
            {
                __instance.yPositionOnScreen -= GetExtraHeight() / 2;
            }
        }

        static bool isWithinBoundsPrefix(IClickableMenu __instance, ref bool __result, int x, ref int y)
        {
            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS && __instance is InventoryPage)
            {
                int extraSpace = GetExtraHeight();
                y += extraSpace;
            }
            return true;
        }

        private static void iClickableMenuPrefix(IClickableMenu __instance, ref int y, ref int height)
        {
            if (__instance is not (GameMenu or MenuWithInventory or ShopMenu or TailoringMenu))
                return;

            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
            {
                if (__instance is GameMenu)
                {
                    int extraSpace = GetExtraHeight() / 2;
                    y -= extraSpace;
                }
                else if (__instance is MuseumMenu)
                {
                    int extraSpace = GetExtraHeight();
                    height += extraSpace;
                }
                else if (__instance is ItemGrabMenu)
                {
                    int extraSpace = GetExtraHeight();
                    height += extraSpace;
                    y -= extraSpace / 2 - DEFAULT_ROW_HEIGHT;
                }
                else
                {
                    int extraSpace = GetExtraHeight();
                    height += extraSpace;
                    y -= extraSpace / 2;
                }
            }
        }

        private static void InventoryMenuPrefix(ref int yPosition, ref IList<Item> actualInventory, ref bool playerInventory, ref int capacity, ref int rows)
        {
            if (actualInventory is not null && actualInventory != Game1.player.Items)
                return;

            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
            {
                if (rows > MAX_ROW_COUNT)
                {
                    rows = MAX_ROW_COUNT;
                    capacity = GetCapacity();
                }
                else if (rows == DEFAULT_ROW_COUNT)
                {
                    if (playerInventory)
                    {
                        int extraSpace = GetExtraHeight();
                        yPosition -= extraSpace;
                    }
                    rows = GetRows();
                    capacity = GetCapacity();
                }
            }
        }

        private static void InventoryPagePrefix(ref int y, ref int height)
        {
            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
            {
                int extraSpace = GetExtraHeight();
                height += extraSpace;
                y += extraSpace;
            }
        }

        private static void InventoryPagePostfix(InventoryPage __instance)
        {
            EnsureScrollButtons(__instance);
            __instance.populateClickableComponentList();
            __instance.snapToDefaultClickableComponent();
        }

        private static void CraftingPagePrefix(ref int y, ref int height)
        {
            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
            {
                int extraSpace = GetExtraHeight();
                height += extraSpace;
            }
        }

        private static void InventoryMenuMethodPrefix(InventoryMenu __instance)
        {
            if (Game1.player == null || Game1.player.maxItems.Value <= DEFAULT_MAX_ITEMS) return;

            if (ActualInventoryField.GetValue(__instance) is not IList<Item> currentInventory) return;
            if (currentInventory is ScrollableInventoryList)
            {
                InventoryStates.GetOrCreateValue(__instance).Depth++;
                return;
            }
            if (currentInventory != Game1.player.Items) return;

            var state = InventoryStates.GetOrCreateValue(__instance);
            state.Depth++;
            state.FullInventory = currentInventory;

            int totalRows = GetTotalRows(currentInventory);
            int maxScrollRow = Math.Max(0, totalRows - MAX_ROW_COUNT);
            state.ScrollRow = Math.Clamp(state.ScrollRow, 0, maxScrollRow);

            if (totalRows <= MAX_ROW_COUNT) return;

            state.OriginalInventory = currentInventory;
            ActualInventoryField.SetValue(__instance, new ScrollableInventoryList(currentInventory, state.ScrollRow * DEFAULT_COLUMN_COUNT, GetCapacity()));
        }

        private static void InventoryMenuMethodPostfix(InventoryMenu __instance)
        {
            if (!InventoryStates.TryGetValue(__instance, out InventoryScrollState? state)) return;

            state.Depth--;
            if (state.Depth > 0) return;

            if (state.OriginalInventory is not null)
            {
                ActualInventoryField.SetValue(__instance, state.OriginalInventory);
                state.OriginalInventory = null;
            }
        }

        private static void EnsureScrollButtons(InventoryPage page)
        {
            if (!TryGetPageState(page, out PageScrollState? state, create: true) || state == null) return;

            state.UpArrow ??= CreateArrow("Scroll Up", ArrowIdUp, UpArrowSourceRect);
            state.DownArrow ??= CreateArrow("Scroll Down", ArrowIdDown, DownArrowSourceRect);

            LayoutScrollButtons(page, state);

            AddClickableComponent(page, state.UpArrow);
            AddClickableComponent(page, state.DownArrow);

            if (Game1.options?.SnappyMenus ?? false)
            {
                var activeComponents = page.allClickableComponents;
                if (Game1.activeClickableMenu is GameMenu gameMenu && gameMenu.allClickableComponents != null)
                {
                    activeComponents = gameMenu.allClickableComponents;
                }
                if (activeComponents != null)
                {
                    WireGamepadNavigation(page, activeComponents);
                }
            }
        }

        private static ClickableTextureComponent CreateArrow(string name, int id, Rectangle sourceRect)
        {
            return new ClickableTextureComponent(name, new Rectangle(0, 0, 44, 48), null, name, Game1.mouseCursors, sourceRect, 4f)
            {
                myID = id,
            };
        }

        private static void LayoutScrollButtons(InventoryPage page, PageScrollState state)
        {
            if (InventoryPageInventoryField.GetValue(page) is not InventoryMenu inventoryMenu) return;
            if (InventoryField.GetValue(inventoryMenu) is not List<ClickableComponent> slots || slots.Count == 0) return;
            if (state.UpArrow == null || state.DownArrow == null) return;

            ClickableComponent firstSlot = slots[0];
            int bottomSlotIndex = Math.Min(slots.Count - 1, (MAX_ROW_COUNT - 1) * DEFAULT_COLUMN_COUNT);
            ClickableComponent lastRowAnchor = slots[bottomSlotIndex];

            var organizeButton = InventoryPageOrganizeButtonField.GetValue(page) as ClickableTextureComponent;
            int targetX = organizeButton != null ? organizeButton.bounds.Center.X - (state.UpArrow.bounds.Width / 2) : lastRowAnchor.bounds.Right + 32;

            int upY = firstSlot.bounds.Y;
            int downY = lastRowAnchor.bounds.Y + (lastRowAnchor.bounds.Height - state.DownArrow.bounds.Height);

            state.UpArrow.bounds.X = targetX;
            state.UpArrow.bounds.Y = upY;
            state.DownArrow.bounds.X = targetX;
            state.DownArrow.bounds.Y = downY;

            var rightEdge = lastRowAnchor.bounds.Right - 16;
            var middleButtons = new List<ClickableComponent>();

            if (organizeButton != null)
                middleButtons.Add(organizeButton);

            if (page.allClickableComponents != null)
            {
                foreach (var c in page.allClickableComponents)
                {
                    if (c == null || c == state.UpArrow || c == state.DownArrow || c == organizeButton || c == page.trashCan)
                        continue;
                    if (c.name == "charPortrait" || c == page.portrait)
                        continue;

                    if (c.bounds.Center.X > rightEdge && c.bounds.Center.X < rightEdge + 300)
                    {
                        if (c.bounds.Y >= upY - 32 && c.bounds.Y <= downY + 64)
                        {
                            middleButtons.Add(c);
                        }
                    }
                }
            }

            middleButtons = middleButtons.Distinct().ToList();
            if (middleButtons.Count == 0) return;

            // Cache the original Y position relative to the menu top (upY) on first sight
            foreach (var b in middleButtons)
            {
                if (!state.OriginalY.ContainsKey(b))
                {
                    state.OriginalY[b] = b.bounds.Y - upY;
                }
            }

            // Sort middleButtons based on their cached original Y offset
            middleButtons = middleButtons.OrderBy(b => state.OriginalY[b]).ToList();

            // Layout the middle buttons vertically between UpArrow.bounds.Bottom and DownArrow.bounds.Top
            int startY = state.UpArrow.bounds.Bottom + 16;
            int endY = state.DownArrow.bounds.Top - 16;
            int availableHeight = endY - startY;
            int totalButtonsHeight = middleButtons.Sum(b => b.bounds.Height);

            if (middleButtons.Count == 1)
            {
                var b = middleButtons[0];
                b.bounds.X = targetX + (state.UpArrow.bounds.Width - b.bounds.Width) / 2;
                b.bounds.Y = startY + (availableHeight - b.bounds.Height) / 2;
            }
            else
            {
                int spacing = (availableHeight - totalButtonsHeight) / (middleButtons.Count - 1);
                if (spacing < 8)
                {
                    spacing = 8;
                    int stackHeight = totalButtonsHeight + (spacing * (middleButtons.Count - 1));
                    int currentY = startY + (availableHeight - stackHeight) / 2;
                    foreach (var b in middleButtons)
                    {
                        b.bounds.X = targetX + (state.UpArrow.bounds.Width - b.bounds.Width) / 2;
                        b.bounds.Y = currentY;
                        currentY += b.bounds.Height + spacing;
                    }
                }
                else
                {
                    int currentY = startY;
                    foreach (var b in middleButtons)
                    {
                        b.bounds.X = targetX + (state.UpArrow.bounds.Width - b.bounds.Width) / 2;
                        b.bounds.Y = currentY;
                        currentY += b.bounds.Height + spacing;
                    }
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

        private static void InventoryPageDrawPostfix(InventoryPage __instance, SpriteBatch b)
        {
            if (!TryGetPageState(__instance, out PageScrollState? state) || state == null) return;
            if (state.UpArrow == null || state.DownArrow == null) return;

            float upAlpha = CanScroll(__instance, -1) ? 1f : 0.35f;
            float downAlpha = CanScroll(__instance, 1) ? 1f : 0.35f;

            state.UpArrow.draw(b, Color.White * upAlpha, 0.9f);
            state.DownArrow.draw(b, Color.White * downAlpha, 0.9f);
        }

        private static bool InventoryPageReceiveLeftClickPrefix(InventoryPage __instance, int x, int y, bool playSound)
        {
            if (!TryGetPageState(__instance, out PageScrollState? state) || state == null) return true;
            if (state.UpArrow == null || state.DownArrow == null) return true;

            if (state.UpArrow.containsPoint(x, y))
            {
                ScrollInventory(__instance, -1);
                return false;
            }
            if (state.DownArrow.containsPoint(x, y))
            {
                ScrollInventory(__instance, 1);
                return false;
            }
            return true;
        }

        private static bool InventoryPageReceiveGamePadButtonPrefix(InventoryPage __instance, Buttons button)
        {
            if (button != Buttons.A) return true;

            ClickableComponent snapped = __instance.currentlySnappedComponent;
            if (snapped == null) return true;

            if (snapped.myID == 77770) // Calendar snap
            {
                Game1.activeClickableMenu = new Billboard(false);
                Game1.playSound("bigSelect");
                return false;
            }
            if (snapped.myID == 77771) // Quest/Billboard snap
            {
                Game1.activeClickableMenu = new Billboard(true);
                Game1.playSound("bigSelect");
                return false;
            }

            if (!TryGetPageState(__instance, out PageScrollState? state) || state == null) return true;
            if (state.UpArrow == null || state.DownArrow == null) return true;

            if (snapped.myID == ArrowIdUp)
            {
                ScrollInventory(__instance, -1);
                return false;
            }
            if (snapped.myID == ArrowIdDown)
            {
                ScrollInventory(__instance, 1);
                return false;
            }
            return true;
        }

        private static void InventoryPagePerformHoverActionPostfix(InventoryPage __instance, int x, int y)
        {
            if (!TryGetPageState(__instance, out PageScrollState? state) || state == null) return;
            if (state.UpArrow == null || state.DownArrow == null) return;

            bool canScrollUp = CanScroll(__instance, -1);
            bool canScrollDown = CanScroll(__instance, 1);

            state.UpArrow.scale = state.UpArrow.containsPoint(x, y) && canScrollUp ? 4.1f : 4f;
            state.DownArrow.scale = state.DownArrow.containsPoint(x, y) && canScrollDown ? 4.1f : 4f;
        }

        private static bool GameMenuReceiveScrollWheelActionPrefix(GameMenu __instance, int direction)
        {
            if (__instance.GetCurrentPage() is not InventoryPage inventoryPage) return true;
            if (!TryGetPageState(inventoryPage, out _)) return true;

            int delta = direction > 0 ? -1 : direction < 0 ? 1 : 0;
            if (delta == 0 || !CanScroll(inventoryPage, delta)) return false;

            ScrollInventory(inventoryPage, delta);
            return false;
        }

        private static void GameMenuUpdatePostfix(GameMenu __instance, GameTime time)
        {
            if (__instance.GetCurrentPage() is not InventoryPage inventoryPage) return;
            if (!TryGetPageState(inventoryPage, out PageScrollState? state) || state == null) return;

            // Monitorar se novos botões foram adicionados por outros mods
            int pageCount = inventoryPage.allClickableComponents?.Count ?? 0;
            int menuCount = __instance.allClickableComponents?.Count ?? 0;
            int combinedHash = pageCount + (menuCount << 16);

            if (combinedHash != state.LastComponentsHash)
            {
                state.LastComponentsHash = combinedHash;
                if (__instance.allClickableComponents != null)
                {
                    WireGamepadNavigation(inventoryPage, __instance.allClickableComponents);
                }
            }

            GamePadState gamePadState = GamePad.GetState(PlayerIndex.One);
            float thumbY = gamePadState.ThumbSticks.Right.Y;
            int currentDirection = thumbY >= 0.5f ? -1 : thumbY <= -0.5f ? 1 : 0;

            if (currentDirection != 0 && currentDirection != state.LastRightStickDirection)
            {
                ScrollInventory(inventoryPage, currentDirection);
            }
            state.LastRightStickDirection = currentDirection;
        }

        private static bool TryGetPageState(InventoryPage page, out PageScrollState? state, bool create = false)
        {
            state = null;
            if (Game1.player?.maxItems.Value <= DEFAULT_MAX_ITEMS) return false;
            if (InventoryPageInventoryField.GetValue(page) is not InventoryMenu inventoryMenu) return false;
            if (ActualInventoryField.GetValue(inventoryMenu) is not IList<Item> actualInventory) return false;

            IList<Item> fullInventory = actualInventory is ScrollableInventoryList scrollable ? scrollable.Underlying : actualInventory;
            if (fullInventory != (Game1.player?.Items) || GetTotalRows(fullInventory) <= MAX_ROW_COUNT) return false;

            state = create ? PageStates.GetOrCreateValue(page) : PageStates.TryGetValue(page, out var existing) ? existing : null;
            return state is not null;
        }

        private static bool CanScroll(InventoryPage page, int direction)
        {
            if (InventoryPageInventoryField.GetValue(page) is not InventoryMenu inventoryMenu) return false;

            var state = InventoryStates.GetOrCreateValue(inventoryMenu);
            IList<Item> fullInventory = state.FullInventory ?? (ActualInventoryField.GetValue(inventoryMenu) as IList<Item>) ?? ((IList<Item>?)Game1.player?.Items) ?? Array.Empty<Item>();

            int totalRows = GetTotalRows(fullInventory);
            int maxScrollRow = Math.Max(0, totalRows - MAX_ROW_COUNT);
            int next = state.ScrollRow + direction;
            return next >= 0 && next <= maxScrollRow;
        }

        private static void ScrollInventory(InventoryPage page, int delta)
        {
            if (InventoryPageInventoryField.GetValue(page) is not InventoryMenu inventoryMenu) return;

            var state = InventoryStates.GetOrCreateValue(inventoryMenu);
            IList<Item> fullInventory = state.FullInventory ?? (ActualInventoryField.GetValue(inventoryMenu) as IList<Item>) ?? ((IList<Item>?)Game1.player?.Items) ?? Array.Empty<Item>();

            int totalRows = GetTotalRows(fullInventory);
            int maxScrollRow = Math.Max(0, totalRows - MAX_ROW_COUNT);
            int newScrollRow = Math.Clamp(state.ScrollRow + delta, 0, maxScrollRow);

            if (newScrollRow == state.ScrollRow) return;

            state.ScrollRow = newScrollRow;
            Game1.playSound("shwip");
        }

        private sealed class InventoryScrollState
        {
            public int ScrollRow;
            public int Depth;
            public IList<Item>? FullInventory;
            public IList<Item>? OriginalInventory;
        }

        private sealed class PageScrollState
        {
            public ClickableTextureComponent? UpArrow;
            public ClickableTextureComponent? DownArrow;
            public int LastRightStickDirection;
            public int LastComponentsHash;
            public readonly Dictionary<ClickableComponent, int> OriginalY = new();
        }

        private sealed class ScrollableInventoryList : IList<Item>
        {
            private readonly int _offset;
            private readonly int _capacity;

            public ScrollableInventoryList(IList<Item> underlying, int offset, int capacity)
            {
                Underlying = underlying;
                _offset = offset;
                _capacity = capacity;
            }

            public IList<Item> Underlying { get; }

            public Item this[int index]
            {
                get => Underlying[_offset + index];
                set => Underlying[_offset + index] = value;
            }

            public int Count => Math.Max(0, Math.Min(_capacity, Underlying.Count - _offset));
            public bool IsReadOnly => Underlying.IsReadOnly;
            public void Add(Item item) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public bool Contains(Item item) => IndexOf(item) >= 0;

            public void CopyTo(Item[] array, int arrayIndex)
            {
                for (int i = 0; i < Count; i++) array[arrayIndex + i] = this[i];
            }

            public IEnumerator<Item> GetEnumerator()
            {
                for (int i = 0; i < Count; i++) yield return this[i];
            }

            public int IndexOf(Item item)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (Equals(this[i], item)) return i;
                }
                return -1;
            }

            public void Insert(int index, Item item) => throw new NotSupportedException();
            public bool Remove(Item item) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}