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
        private const int DEFAULT_ROW_HEIGHT = 64;
        private const int DEFAULT_COLUMN_COUNT = 12;
        private const int DEFAULT_ROW_COUNT = 3;
        private const int MAX_ROW_COUNT = 7;
        private const int DEFAULT_MAX_ITEMS = 36;

        private const int ArrowIdUp = 11001;
        private const int ArrowIdDown = 11002;

        private static readonly ConditionalWeakTable<InventoryMenu, GridViewport> GridViewports =
            new();
        private static readonly ConditionalWeakTable<IClickableMenu, BoxedInt> MenuOriginalXs =
            new();
        private static readonly ConditionalWeakTable<IClickableMenu, BoxedInt> ChestLayoutHashes =
            new();
        private static readonly ConditionalWeakTable<
            IClickableMenu,
            BoxedInt
        > ChestLayoutLoggedHashes = new();
        private static readonly ConditionalWeakTable<
            IClickableMenu,
            MenuCachedFields
        > MenuFieldsCache = new();

        private sealed class MenuCachedFields
        {
            public ClickableComponent? ColorBtn;
            public ClickableComponent? FillBtn;
            public ClickableComponent? OrganizeBtn;
            public ClickableComponent? OkBtn;
            public ClickableComponent? TrashBtn;
            public DiscreteColorPicker? ColorPicker;
            public ClickableTextureComponent? PickerToggle;
        }

        private sealed class BoxedInt
        {
            public int Value;

            public BoxedInt(int value) => Value = value;
        }

        private static readonly FieldInfo InventoryField = AccessTools.Field(
            typeof(InventoryMenu),
            "inventory"
        );
        private static readonly FieldInfo ActualInventoryField = AccessTools.Field(
            typeof(InventoryMenu),
            "actualInventory"
        );
        private static readonly FieldInfo InventoryPageInventoryField = AccessTools.Field(
            typeof(InventoryPage),
            "inventory"
        );
        private static readonly FieldInfo InventoryPageOrganizeButtonField = AccessTools.Field(
            typeof(InventoryPage),
            "organizeButton"
        );
        private static readonly FieldInfo ItemGrabMenuColorPickerField = AccessTools.Field(
            typeof(ItemGrabMenu),
            "colorPicker"
        );
        private static readonly FieldInfo DiscreteColorPickerToggleButtonField = AccessTools.Field(
            typeof(DiscreteColorPicker),
            "colorPickerToggleButton"
        );
        private static readonly FieldInfo ShopMenuScrollBarRunnerField = AccessTools.Field(
            typeof(ShopMenu),
            "scrollBarRunner"
        );
        private static readonly MethodInfo? ShopMenuSetScrollBarToCurrentIndexMethod =
            AccessTools.Method(typeof(ShopMenu), "setScrollBarToCurrentIndex");

        private static readonly Rectangle UpArrowSourceRect = new(421, 459, 11, 12);
        private static readonly Rectangle DownArrowSourceRect = new(421, 472, 11, 12);
        public static IClickableMenu? CurrentParentMenu = null;

        private static int GetDynamicMaxRows()
        {
            // Reserva ~500px super seguros de altura para baú acima, título, botões e margens da tela.
            // O espaço restante nós dividimos por 64 (altura de uma linha) para saber QUANTAS cabem dentro da UI.
            int reservedHeight = 500;
            int availableHeight = Game1.uiViewport.Height - reservedHeight;
            int maxRows = availableHeight / DEFAULT_ROW_HEIGHT;

            // Retorna no mínimo 3 (padrão do jogo) e no máximo 7 (limite do seu mod)
            return Math.Clamp(maxRows, DEFAULT_ROW_COUNT, MAX_ROW_COUNT);
        }

        private static int GetRows()
        {
            if (Game1.player.maxItems.Value <= DEFAULT_MAX_ITEMS)
            {
                return DEFAULT_ROW_COUNT;
            }
            int maxAllowed = GetDynamicMaxRows();
            int rows = Game1.player.maxItems.Value / DEFAULT_COLUMN_COUNT;
            return Math.Min(rows, maxAllowed);
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
            int extraRows = GetExtraRow();
            if (extraRows <= 0)
                return 0; // Proteção para não encolher o menu se o monitor for pequeno
            return (extraRows * DEFAULT_ROW_HEIGHT)
                + ((extraRows - 1) * IClickableMenu.spaceBetweenTabs);
        }

        public static int GetDoubleExtraHeight()
        {
            return GetExtraHeight() * 2;
        }

        public static int GetBillboardOffset()
        {
            int extra = GetExtraHeight();
            if (extra <= 0)
                return 0;

            return extra - DEFAULT_ROW_HEIGHT;
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
            // Patch no construtor do MenuWithInventory para capturar a instância
            // Patch no construtor do MenuWithInventory para capturar a instância
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

            // Injeção de navegação na página do inventário com prioridade Last (roda por último)
            harmony.Patch(
                original: this.RequireMethod<InventoryPage>(
                    nameof(InventoryPage.setUpForGamePadMode)
                ),
                postfix: this.GetHarmonyMethod(
                    nameof(InventoryPageSetUpForGamePadModePostfix),
                    (int)Priority.Last
                )
            );

            // Injeção MASTER no GameMenu com prioridade Last
            harmony.Patch(
                original: this.RequireMethod<GameMenu>(nameof(GameMenu.setUpForGamePadMode)),
                postfix: this.GetHarmonyMethod(
                    nameof(GameMenuSetUpForGamePadModePostfix),
                    (int)Priority.Last
                )
            );

            // População com prioridade Last para rodar depois de todos os outros mods
            harmony.Patch(
                original: this.RequireMethod<IClickableMenu>(
                    nameof(IClickableMenu.populateClickableComponentList)
                ),
                postfix: this.GetHarmonyMethod(
                    nameof(PopulateClickableComponentListPostfix),
                    (int)Priority.Last
                )
            );

            // Universal Gamepad Snap Scroll Navigation Patches
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

            if (InventoryPageInventoryField.GetValue(page) is InventoryMenu inventoryMenu)
            {
                if (GridViewports.TryGetValue(inventoryMenu, out var grid) && grid != null)
                {
                    if (
                        grid.UpArrow != null
                        && !__instance.allClickableComponents.Contains(grid.UpArrow)
                    )
                        __instance.allClickableComponents.Add(grid.UpArrow);
                    if (
                        grid.DownArrow != null
                        && !__instance.allClickableComponents.Contains(grid.DownArrow)
                    )
                        __instance.allClickableComponents.Add(grid.DownArrow);
                }
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
            if (__instance.allClickableComponents == null)
                return;

            EnsureScrollButtons(__instance);
            WireGamepadNavigation(__instance, __instance.allClickableComponents);
        }

        internal static void WireGamepadNavigation(
            InventoryPage page,
            List<ClickableComponent> activeComponents
        )
        {
            if (activeComponents == null || activeComponents.Count == 0)
                return;
            if (InventoryPageInventoryField.GetValue(page) is not InventoryMenu inventoryMenu)
                return;
            if (
                InventoryField.GetValue(inventoryMenu) is not List<ClickableComponent> slots
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

            // 1. Coleta as setas de rolagem do estado se existirem
            GridViewports.TryGetValue(inventoryMenu, out var grid);
            if (grid != null)
            {
                if (grid.UpArrow != null && activeSet.Add(grid.UpArrow))
                    activeComponents.Add(grid.UpArrow);
                if (grid.DownArrow != null && activeSet.Add(grid.DownArrow))
                    activeComponents.Add(grid.DownArrow);
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
            if (grid != null)
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

            // Ordena todos por Y
            rightColumn = rightColumn.OrderBy(c => c.bounds.Center.Y).ToList();

            // 3. O SEGREDO: Atribuir IDs válidos aos botões de mods (que normalmente usam -1).
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

                comp.upNeighborID = i > 0 ? rightColumn[i - 1].myID : -1;
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

        public static IEnumerable<CodeInstruction> drawTranspiler(
            IEnumerable<CodeInstruction> instructions
        )
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
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(InventoryMenuPatcher), nameof(GetExtraHeight))
                        )
                    );
                    codes.Insert(i + 7, new CodeInstruction(OpCodes.Add));
                    Log.Debug("Patching ShopMenu to add extra height to the shop menu");
                    break;
                }
            }
            return codes;
        }

        public static IEnumerable<CodeInstruction> receiveLeftClickTranspiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            var codes = new List<CodeInstruction>(instructions);
            var result = new List<CodeInstruction>();
            bool found = false;

            FieldInfo yPosField = AccessTools.Field(
                typeof(IClickableMenu),
                nameof(IClickableMenu.yPositionOnScreen)
            );
            FieldInfo heightField = AccessTools.Field(
                typeof(IClickableMenu),
                nameof(IClickableMenu.height)
            );

            for (int i = 0; i < codes.Count; i++)
            {
                result.Add(codes[i]);

                // yPositionOnScreen + height + 64
                if (
                    !found
                    && i >= 6
                    && codes[i - 6].opcode == OpCodes.Ldarg_0
                    && codes[i - 5].LoadsField(yPosField)
                    && codes[i - 4].opcode == OpCodes.Ldarg_0
                    && codes[i - 3].LoadsField(heightField)
                    && codes[i - 2].opcode == OpCodes.Add
                    && codes[i - 1].OperandIs(64)
                    && codes[i].opcode == OpCodes.Add
                )
                {
                    found = true;

                    result.Add(
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(InventoryMenuPatcher), nameof(GetExtraHeight))
                        )
                    );
                    result.Add(new CodeInstruction(OpCodes.Add));
                }
            }

            return result;
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
                AdjustShopMenuScrollLayout(shopMenu);
            }

            var menus = FindInventoryMenus(menu);
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
                chestColumns = DEFAULT_COLUMN_COUNT;
            int playerColumns = playerMenu.capacity / playerMenu.rows;
            if (playerColumns <= 0)
                playerColumns = DEFAULT_COLUMN_COUNT;

            var playerGrid = GridViewports.GetValue(playerMenu, m => new GridViewport(m));
            playerGrid.CustomArrowLayout = true;
            playerGrid.UpArrow.myID = ArrowIdUp;
            playerGrid.DownArrow.myID = ArrowIdDown;
            playerGrid.EnsureClickableComponents(menu);

            var fields = MenuFieldsCache.GetValue(
                menu,
                m =>
                {
                    var cBtn = FindFieldContaining(m, "colorPickerToggleButton");
                    var fBtn = FindFieldContaining(m, "fillStacksButton");
                    var oBtn =
                        FindFieldContaining(m, "organizeButton")
                        ?? FindFieldContaining(m, "organizeStashButton");
                    var oK =
                        FindFieldContaining(m, "okButton")
                        ?? FindFieldContaining(m, "specialButton");
                    var tBtn = FindFieldContaining(m, "trashCan");

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
                        tBtn != null
                        && (tBtn == m.upperRightCloseButton || tBtn.name == "upperRightCloseButton")
                    )
                        tBtn = null;

                    var cPicker = FindColorPicker(m);
                    var pToggle = cPicker != null ? FindColorPickerToggleButton(cPicker) : null;
                    if (cBtn == null && pToggle != null)
                        cBtn = pToggle;

                    return new MenuCachedFields
                    {
                        ColorBtn = cBtn,
                        FillBtn = fBtn,
                        OrganizeBtn = oBtn,
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
            var okBtn = fields.OkBtn;
            var trashBtn = fields.TrashBtn;
            var pickerToggle = fields.PickerToggle;
            var colorPicker = fields.ColorPicker;
            var preferredSide = GetPreferredSide(menu);
            int preferredSideOffsetPixels = GetPreferredSideOffsetPixels(menu);
            int? arrowAnchorCenterXOverride = GetArrowAnchorCenterXOverride(menu);
            var arrowAnchorComponentOverride = GetArrowAnchorComponentOverride(menu);
            int anchorCenterX =
                (okBtn ?? trashBtn ?? organizeBtn ?? fillBtn ?? colorBtn)?.bounds.Center.X ?? 0;
            int layoutHash = ComputeChestLayoutHash(
                anchorCenterX,
                new List<ClickableComponent>(),
                colorBtn,
                fillBtn,
                organizeBtn,
                okBtn,
                trashBtn,
                chestSlots,
                playerSlots,
                menu.allClickableComponents?.Count ?? 0
            );
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
                    OkButton = okBtn,
                    TrashButton = trashBtn,
                    PickerToggle = pickerToggle,
                    ColorPicker = colorPicker,
                }
            );
        }

        private static GridViewport.SideLayoutPreference GetPreferredSide(IClickableMenu menu)
        {
            string typeName = menu.GetType().Name;
            if (typeName == "HugeChestMenu")
            {
                return GridViewport.SideLayoutPreference.Left;
            }

            return GridViewport.SideLayoutPreference.Right;
        }

        private static int GetPreferredSideOffsetPixels(IClickableMenu menu)
        {
            string typeName = menu.GetType().Name;
            if (typeName == "HugeChestMenu")
            {
                return 64;
            }

            return 28;
        }

        private static int? GetArrowAnchorCenterXOverride(IClickableMenu menu)
        {
            if (menu is ShopMenu shopMenu)
            {
                return null;
            }

            return null;
        }

        private static ClickableComponent? GetArrowAnchorComponentOverride(IClickableMenu menu)
        {
            if (menu is ShopMenu shopMenu)
            {
                return shopMenu.upArrow ?? shopMenu.downArrow;
            }

            return null;
        }

        private static void AdjustShopMenuScrollLayout(ShopMenu menu)
        {
            if (menu?.upArrow == null || menu.downArrow == null || menu.scrollBar == null)
                return;
            if (menu.forSaleButtons == null || menu.forSaleButtons.Count == 0)
                return;
            if (ShopMenuScrollBarRunnerField == null || ShopMenuSetScrollBarToCurrentIndexMethod == null)
                return;

            var firstSaleButton = menu.forSaleButtons[0];
            var lastSaleButton = menu.forSaleButtons[menu.forSaleButtons.Count - 1];
            int arrowX = menu.upArrow.bounds.X;
            int upY = firstSaleButton.bounds.Top;
            int downY = lastSaleButton.bounds.Bottom - menu.downArrow.bounds.Height;

            menu.upArrow.bounds = new Rectangle(
                arrowX,
                upY,
                menu.upArrow.bounds.Width,
                menu.upArrow.bounds.Height
            );
            menu.downArrow.bounds = new Rectangle(
                arrowX,
                downY,
                menu.downArrow.bounds.Width,
                menu.downArrow.bounds.Height
            );

            int runnerX = menu.upArrow.bounds.X + 12;
            int runnerY = menu.upArrow.bounds.Bottom + 4;
            int runnerHeight = Math.Max(8, menu.downArrow.bounds.Y - runnerY - 8);

            menu.scrollBar.bounds = new Rectangle(
                runnerX,
                menu.scrollBar.bounds.Y,
                menu.scrollBar.bounds.Width,
                menu.scrollBar.bounds.Height
            );

            ShopMenuScrollBarRunnerField.SetValue(
                menu,
                new Rectangle(runnerX, runnerY, menu.scrollBar.bounds.Width, runnerHeight)
            );
            SetShopScrollBarToCurrentIndex(menu, runnerY, downY);
        }

        private static void SetShopScrollBarToCurrentIndex(ShopMenu menu, int runnerY, int downArrowY)
        {
            int maxIndex = Math.Max(0, menu.forSale.Count - 4);
            int minY = runnerY;
            int maxY = Math.Max(minY, downArrowY - menu.scrollBar.bounds.Height - 4);

            int targetY;
            if (maxIndex <= 0)
            {
                targetY = minY;
            }
            else
            {
                float progress = menu.currentItemIndex / (float)maxIndex;
                targetY = minY + (int)Math.Round((maxY - minY) * progress);
            }

            menu.scrollBar.bounds = new Rectangle(
                menu.scrollBar.bounds.X,
                targetY,
                menu.scrollBar.bounds.Width,
                menu.scrollBar.bounds.Height
            );
        }

        private static ClickableComponent? FindFieldContaining(object obj, string substring)
        {
            if (obj == null)
                return null;
            Type? type = obj.GetType();
            while (type != null)
            {
                foreach (
                    var field in type.GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    )
                )
                {
                    if (typeof(ClickableComponent).IsAssignableFrom(field.FieldType))
                    {
                        if (field.Name.Contains(substring, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                return field.GetValue(obj) as ClickableComponent;
                            }
                            catch { }
                        }
                    }
                }
                type = type.BaseType;
            }
            return null;
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
            ClickableComponent? okBtn,
            ClickableComponent? trashBtn,
            List<ClickableComponent> chestSlots,
            List<ClickableComponent> playerSlots,
            int clickableCount
        )
        {
            var hash = new HashCode();
            hash.Add(anchorCenterX);
            hash.Add(clickableCount);
            hash.Add(chestSlots[0].bounds.Y);
            hash.Add(playerSlots[0].bounds.Y);
            hash.Add(playerSlots[playerSlots.Count - 1].bounds.Y);
            hash.Add(colorBtn != null ? RuntimeHelpers.GetHashCode(colorBtn) : 0);
            hash.Add(fillBtn != null ? RuntimeHelpers.GetHashCode(fillBtn) : 0);
            hash.Add(organizeBtn != null ? RuntimeHelpers.GetHashCode(organizeBtn) : 0);
            hash.Add(okBtn != null ? RuntimeHelpers.GetHashCode(okBtn) : 0);
            hash.Add(trashBtn != null ? RuntimeHelpers.GetHashCode(trashBtn) : 0);

            foreach (var button in chestTopColumn)
            {
                hash.Add(RuntimeHelpers.GetHashCode(button));
            }

            return hash.ToHashCode();
        }

        private static DiscreteColorPicker? FindColorPicker(IClickableMenu menu)
        {
            if (menu == null)
                return null;
            Type? type = menu.GetType();
            while (type != null)
            {
                foreach (
                    var field in type.GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    )
                )
                {
                    if (typeof(DiscreteColorPicker).IsAssignableFrom(field.FieldType))
                    {
                        try
                        {
                            return field.GetValue(menu) as DiscreteColorPicker;
                        }
                        catch { }
                    }
                }
                type = type.BaseType;
            }
            return null;
        }

        private static ClickableTextureComponent? FindColorPickerToggleButton(
            DiscreteColorPicker picker
        )
        {
            if (picker == null)
                return null;
            foreach (
                var field in typeof(DiscreteColorPicker).GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                )
            )
            {
                if (typeof(ClickableTextureComponent).IsAssignableFrom(field.FieldType))
                {
                    if (
                        field.Name.Contains("toggle", StringComparison.OrdinalIgnoreCase)
                        || field.Name.Contains("button", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        try
                        {
                            var val = field.GetValue(picker) as ClickableTextureComponent;
                            if (val != null)
                                return val;
                        }
                        catch { }
                    }
                }
            }
            return null;
        }

        private static void ItemGrabMenuDrawPrefix(ItemGrabMenu __instance, SpriteBatch b)
        {
            RepositionAndWireSideButtons(__instance);
        }

        private static void ShopMenuDrawPrefix(ShopMenu __instance, SpriteBatch b)
        {
            AdjustShopMenuScrollLayout(__instance);
            RepositionAndWireSideButtons(__instance);
        }

        private static void MuseumMenuDrawPrefix(MuseumMenu __instance, SpriteBatch b)
        {
            RepositionAndWireSideButtons(__instance);
        }

        private static void updatePositionPostfix(ShopMenu __instance)
        {
            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
            {
                int extraHeight = GetExtraHeight();
                __instance.yPositionOnScreen -= extraHeight / 2;
            }

            AdjustShopMenuScrollLayout(__instance);
        }

        static bool isWithinBoundsPrefix(
            IClickableMenu __instance,
            ref bool __result,
            int x,
            ref int y
        )
        {
            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
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

            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
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
            if (actualInventory is not null && actualInventory != Game1.player.Items)
                return;

            if (IsCalledFromMuseum())
            {
                rows = 3;
                capacity = rows * DEFAULT_COLUMN_COUNT;
                return;
            }

            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
            {
                if (CurrentParentMenu is ItemGrabMenu grabMenu)

                {
                    string nomeDaClasse = grabMenu.GetType().Name;

                    if (nomeDaClasse == "HugeChestMenu")
                    {
                        rows = 4;
                        capacity = rows * DEFAULT_COLUMN_COUNT;
                        return;
                    }

                    var max = CurrentParentMenu.yPositionOnScreen + CurrentParentMenu.height - 192 - IClickableMenu.borderWidth - yPosition + 64;
                    int space = IClickableMenu.spaceBetweenTabs;
                    int rawRows = DEFAULT_ROW_HEIGHT + space;
                    int adjustedMax = max + 2 * space;
                    int maxLinhasQueCabem = adjustedMax / rawRows;
                    rows = maxLinhasQueCabem;
                    capacity = rows * DEFAULT_COLUMN_COUNT;
                    return;
                }

                int dynamicMax = GetDynamicMaxRows();
                if (rows > dynamicMax)
                {
                    rows = dynamicMax;
                    capacity = rows * DEFAULT_COLUMN_COUNT;
                }
                else if (rows == DEFAULT_ROW_COUNT)
                {
                    if (playerInventory)
                    {
                        int extraSpace = GetExtraHeight();
                        yPosition -= extraSpace;
                    }
                    rows = GetRows();
                    capacity = rows * DEFAULT_COLUMN_COUNT;
                }
            }
        }

        private static int GetExtraHeightForRows(int rows)
        {
            int extraRows = Math.Max(0, rows - DEFAULT_ROW_COUNT);
            if (extraRows <= 0)
                return 0;

            return (extraRows * DEFAULT_ROW_HEIGHT)
                + ((extraRows - 1) * IClickableMenu.spaceBetweenTabs);
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
            if (Game1.player == null)
                return;

            if (ActualInventoryField.GetValue(__instance) is not IList<Item> currentInventory)
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
                columns = DEFAULT_COLUMN_COUNT;

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
            ActualInventoryField.SetValue(__instance, scrollableList);

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
                ActualInventoryField.SetValue(__instance, grid.OriginalInventory);
                grid.OriginalInventory = null;
            }
        }

        private static void EnsureScrollButtons(InventoryPage page)
        {
            if (InventoryPageInventoryField.GetValue(page) is not InventoryMenu inventoryMenu)
                return;
            var grid = GridViewports.GetValue(inventoryMenu, m => new GridViewport(m));

            if (!TryGetPageState(page, out GridViewport? stateGrid, create: true) || stateGrid == null)
                return;

            grid.CustomArrowLayout = true;
            grid.UpArrow.myID = ArrowIdUp;
            grid.DownArrow.myID = ArrowIdDown;

            LayoutScrollButtons(page, grid);

            AddClickableComponent(page, grid.UpArrow);
            AddClickableComponent(page, grid.DownArrow);

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
            if (InventoryPageInventoryField.GetValue(page) is not InventoryMenu inventoryMenu)
                return;
            if (
                InventoryField.GetValue(inventoryMenu) is not List<ClickableComponent> slots
                || slots.Count == 0
            )
                return;
            var organizeButton =
                InventoryPageOrganizeButtonField.GetValue(page) as ClickableTextureComponent;
            grid.LayoutInventoryPageButtons(page, slots, organizeButton);
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
                InventoryPageInventoryField.GetValue(inventoryPage)
                is not InventoryMenu inventoryMenu
            )
                return true;

            if (GridViewports.TryGetValue(inventoryMenu, out var grid) && grid != null)
            {
                IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
                if (
                    items != null
                    && IsMouseTargetingInventoryArea(
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
                InventoryPageInventoryField.GetValue(inventoryPage)
                is not InventoryMenu inventoryMenu
            )
                return;
            if (!TryGetPageState(inventoryPage, out GridViewport? grid) || grid == null)
                return;

            int pageCount = inventoryPage.allClickableComponents?.Count ?? 0;
            int menuCount = __instance.allClickableComponents?.Count ?? 0;
            int combinedHash = pageCount + (menuCount << 16);

            if (combinedHash != grid.LastPageComponentsHash)
            {
                grid.LastPageComponentsHash = combinedHash;
                LayoutScrollButtons(inventoryPage, grid);
                if (__instance.allClickableComponents != null)
                {
                    WireGamepadNavigation(inventoryPage, __instance.allClickableComponents);
                }
            }

            if (GridViewports.TryGetValue(inventoryMenu, out var viewport) && viewport != null)
            {
                IList<Item>? items = viewport.OriginalInventory ?? viewport.FullInventory;
                if (items != null && IsGamepadTargetingInventoryArea(__instance, inventoryMenu, viewport))
                {
                    viewport.UpdateGamepad(items);
                }
            }
        }

        private static InventoryMenu? GetPlayerInventoryMenu(IClickableMenu menu)
        {
            if (menu == null)
                return null;
            FieldInfo? field = menu.GetType()
                .GetField(
                    "inventory",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                );
            if (field != null && typeof(InventoryMenu).IsAssignableFrom(field.FieldType))
            {
                return field.GetValue(menu) as InventoryMenu;
            }
            return null;
        }

        private static bool MenuReceiveScrollWheelActionPrefix(
            IClickableMenu __instance,
            int direction
        )
        {
            var menus = FindInventoryMenus(__instance);
            foreach (var menu in menus)
            {
                if (
                    IsMouseTargetingInventoryArea(
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
            var menus = FindInventoryMenus(__instance);
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

            var menus = FindInventoryMenus(__instance);
            foreach (var menu in menus)
            {
                if (GridViewports.TryGetValue(menu, out GridViewport? grid) && grid != null)
                {
                    IList<Item>? items = grid.OriginalInventory ?? grid.FullInventory;
                    if (items != null && IsGamepadTargetingInventoryArea(__instance, menu, grid))
                    {
                        grid.UpdateGamepad(items);
                    }
                }
            }
        }

        private static bool IsMouseTargetingInventoryArea(
            InventoryMenu menu,
            GridViewport? grid,
            int mouseX,
            int mouseY
        )
        {
            if (menu.inventory != null)
            {
                foreach (var slot in menu.inventory)
                {
                    if (slot?.containsPoint(mouseX, mouseY) == true)
                        return true;
                }
            }

            if (grid != null)
            {
                if (grid.UpArrow.containsPoint(mouseX, mouseY))
                    return true;
                if (grid.DownArrow.containsPoint(mouseX, mouseY))
                    return true;
                if (grid.ShowAuxScrollBar)
                {
                    if (grid.ScrollBarThumb.containsPoint(mouseX, mouseY))
                        return true;
                    if (grid.ScrollBarRunner.Contains(mouseX, mouseY))
                        return true;
                }
            }

            return false;
        }

        private static bool IsGamepadTargetingInventoryArea(
            IClickableMenu parentMenu,
            InventoryMenu inventoryMenu,
            GridViewport grid
        )
        {
            var snapped = parentMenu.currentlySnappedComponent;
            if (snapped == null)
                return false;

            if (inventoryMenu.inventory != null && inventoryMenu.inventory.Contains(snapped))
                return true;

            if (ReferenceEquals(snapped, grid.UpArrow) || ReferenceEquals(snapped, grid.DownArrow))
                return true;

            if (grid.ShowAuxScrollBar && ReferenceEquals(snapped, grid.ScrollBarThumb))
                return true;

            return false;
        }

        private static List<InventoryMenu> FindInventoryMenus(IClickableMenu menu)
        {
            var list = new List<InventoryMenu>();
            if (menu == null)
                return list;

            foreach (
                var field in menu.GetType()
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            )
            {
                if (typeof(InventoryMenu).IsAssignableFrom(field.FieldType))
                {
                    var val = field.GetValue(menu) as InventoryMenu;
                    if (val != null)
                        list.Add(val);
                }
            }

            foreach (
                var prop in menu.GetType()
                    .GetProperties(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    )
            )
            {
                if (typeof(InventoryMenu).IsAssignableFrom(prop.PropertyType))
                {
                    try
                    {
                        var val = prop.GetValue(menu) as InventoryMenu;
                        if (val != null)
                            list.Add(val);
                    }
                    catch { }
                }
            }

            return list;
        }

        private static bool MenuReceiveGamePadButtonPrefix(
            IClickableMenu __instance,
            Buttons button
        )
        {
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

            var menus = FindInventoryMenus(__instance);
            foreach (var menu in menus)
            {
                if (menu.inventory != null && menu.inventory.Contains(snapped))
                {
                    int index = menu.inventory.IndexOf(snapped);
                    int columns = menu.capacity / menu.rows;
                    if (columns <= 0)
                        columns = DEFAULT_COLUMN_COUNT;

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
            if (Game1.player?.maxItems.Value <= DEFAULT_MAX_ITEMS)
                return false;
            if (InventoryPageInventoryField.GetValue(page) is not InventoryMenu inventoryMenu)
                return false;
            if (ActualInventoryField.GetValue(inventoryMenu) is not IList<Item> actualInventory)
                return false;

            IList<Item> fullInventory = actualInventory is ScrollableInventoryList scrollable
                ? scrollable.Underlying
                : actualInventory;
            if (
                fullInventory != (Game1.player?.Items)
                || GetTotalRows(fullInventory) <= inventoryMenu.rows
            )
                return false;

            state =
                create ? GridViewports.GetValue(inventoryMenu, m => new GridViewport(m))
                : GridViewports.TryGetValue(inventoryMenu, out var existing) ? existing
                : null;
            return state is not null;
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
                for (int i = 0; i < Count; i++)
                    array[arrayIndex + i] = this[i];
            }

            public IEnumerator<Item> GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                    yield return this[i];
            }

            public int IndexOf(Item item)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (Equals(this[i], item))
                        return i;
                }
                return -1;
            }

            public void Insert(int index, Item item) => throw new NotSupportedException();

            public bool Remove(Item item) => throw new NotSupportedException();

            public void RemoveAt(int index) => throw new NotSupportedException();

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
                GetEnumerator();
        }
    }
}
