using System.Reflection;
using System.Reflection.Emit;
using CpdnCristiano.StardewValleyMods.Common.Log;
using CpdnCristiano.StardewValleyMods.Common.Patching;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;
using static StardewValley.Menus.InventoryMenu;

namespace CpdnCristiano.StardewValleyMods.FullInventoryView.Patcher
{
    internal class InventoryMenuPatcher : BasePatcher
    {
        private static readonly int DEFAULT_ROW_HEIGHT = 64;

        private static readonly int DEFAULT_COLUMN_COUNT = 12;

        private static readonly int DEFAULT_ROW_COUNT = 3;

        private static readonly int MAX_ROW_COUNT = 7;

        private static readonly int DEFAULT_MAX_ITEMS = 36;

        private static int GetRows()
        {
            if (Game1.player.maxItems.Value <= DEFAULT_MAX_ITEMS)
            {
                return DEFAULT_ROW_COUNT;
            }
            int rows = Game1.player.maxItems.Value / DEFAULT_COLUMN_COUNT;

            return rows > MAX_ROW_COUNT ? MAX_ROW_COUNT : rows;
        }

        private static int GetExtraRow()
        {
            int rows = GetRows();
            return rows > DEFAULT_ROW_COUNT ? (rows - DEFAULT_ROW_COUNT) : 0;
        }

        public static int GetExtraHeight()
        {
            return (GetExtraRow() * DEFAULT_ROW_HEIGHT)
                + ((GetExtraRow() - 1) * IClickableMenu.spaceBetweenTabs);
        }

        private static int GetCapacity()
        {
            return GetRows() * DEFAULT_COLUMN_COUNT;
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
                prefix: this.GetHarmonyMethod(nameof(InventoryPagePrefix))
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

            MethodInfo method = typeof(IClickableMenu).GetMethod(
                "drawHorizontalPartition",
                BindingFlags.NonPublic | BindingFlags.Instance,
                new Type[]
                {
                    typeof(SpriteBatch),
                    typeof(int),
                    typeof(bool),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                }
            )!;

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
                var isStorageShop = _isStorageShopField?.GetValue(__instance) as bool? ?? false;
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

        static bool isWithinBoundsPrefix(
            IClickableMenu __instance,
            ref bool __result,
            int x,
            ref int y
        )
        {
            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS && __instance is InventoryPage)
            {
                int extraSpace = GetExtraHeight();
                y += extraSpace;
            }

            return true;
        }

        private static void iClickableMenuPrefix(
            IClickableMenu __instance,
            ref int y,
            ref int height
        )
        {
            if (
                !(
                    __instance is GameMenu
                    || __instance is MenuWithInventory
                    || __instance is ShopMenu
                    || __instance is TailoringMenu
                )
            )
                return;

            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
            {
                if (__instance is GameMenu)
                {
                    int extraEspace = GetExtraHeight() / 2;
                    y -= extraEspace;
                }
                else if (__instance is MuseumMenu)
                {
                    int extraEspace = GetExtraHeight();
                    height += extraEspace;
                }
                else if (__instance is ItemGrabMenu)
                {
                    int extraEspace = GetExtraHeight();
                    height += extraEspace;
                    y -= extraEspace / 2 - DEFAULT_ROW_HEIGHT;
                }
                else
                {
                    int extraEspace = GetExtraHeight();
                    height += extraEspace;
                    y -= extraEspace / 2;
                }
            }
        }

        private static void iClickableMenuPostifix(IClickableMenu __instance)
        {
            if (!(__instance is GameMenu || __instance is MenuWithInventory))
                return;

            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS) { }
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

            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
            {
                if (rows == DEFAULT_ROW_COUNT)
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

        private static void CraftingPagePrefix(ref int y, ref int height)
        {
            if (Game1.player.maxItems.Value > DEFAULT_MAX_ITEMS)
            {
                int extraSpace = GetExtraHeight();
                height += extraSpace;
            }
        }
    }
}
