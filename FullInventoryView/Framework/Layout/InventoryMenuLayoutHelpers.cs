using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout
{
    internal static class InventoryMenuLayoutHelpers
    {
        public static bool DrawCurrencyPrefix(
            ShopMenu menu,
            SpriteBatch b,
            Func<int> getExtraHeight,
            Func<int> getExtraRow
        )
        {
            if (!InventoryGridMetrics.PlayerHasExpandedInventory())
                return true;

            FieldInfo? isStorageShopField = typeof(ShopMenu).GetField(
                "_isStorageShop",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (isStorageShopField == null)
                return true;

            var isStorageShop = isStorageShopField.GetValue(menu) as bool? ?? false;
            if (!isStorageShop && menu.currency == 0)
            {
                var extraHeight =
                    getExtraHeight() - ((getExtraRow() - 1) * IClickableMenu.spaceBetweenTabs);
                if (extraHeight < 0)
                    extraHeight = 0;

                Game1.dayTimeMoneyBox.drawMoneyBox(
                    b,
                    menu.xPositionOnScreen - 36,
                    menu.yPositionOnScreen + menu.height - menu.inventory.height - 12 + extraHeight
                );
            }

            return false;
        }

        public static IEnumerable<CodeInstruction> InjectExtraHeightIntoShopDraw(
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
                            AccessTools.Method(typeof(CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher.InventoryMenuPatcher), "GetExtraHeight")
                        )
                    );
                    codes.Insert(i + 7, new CodeInstruction(OpCodes.Add));
                    break;
                }
            }

            return codes;
        }

        public static IEnumerable<CodeInstruction> InjectExtraHeightIntoShopClick(
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
                            AccessTools.Method(typeof(CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher.InventoryMenuPatcher), "GetExtraHeight")
                        )
                    );
                    result.Add(new CodeInstruction(OpCodes.Add));
                }
            }

            return result;
        }

        public static GridViewport.SideLayoutPreference GetPreferredSide(IClickableMenu menu)
        {
            return menu.GetType().Name == "HugeChestMenu"
                ? GridViewport.SideLayoutPreference.Left
                : GridViewport.SideLayoutPreference.Right;
        }

        public static int GetPreferredSideOffsetPixels(IClickableMenu menu)
        {
            return menu.GetType().Name == "HugeChestMenu" ? 64 : 28;
        }

        public static ClickableComponent? GetArrowAnchorComponentOverride(IClickableMenu menu)
        {
            if (menu is ShopMenu shopMenu)
                return shopMenu.upArrow ?? shopMenu.downArrow;

            return null;
        }

        public static void AdjustShopMenuScrollLayout(
            ShopMenu menu,
            FieldInfo scrollBarRunnerField
        )
        {
            if (menu?.upArrow == null || menu.downArrow == null || menu.scrollBar == null)
                return;
            if (menu.forSaleButtons == null || menu.forSaleButtons.Count == 0)
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

            scrollBarRunnerField.SetValue(
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

            int targetY = maxIndex <= 0
                ? minY
                : minY + (int)Math.Round((maxY - minY) * (menu.currentItemIndex / (float)maxIndex));

            menu.scrollBar.bounds = new Rectangle(
                menu.scrollBar.bounds.X,
                targetY,
                menu.scrollBar.bounds.Width,
                menu.scrollBar.bounds.Height
            );
        }
    }
}
