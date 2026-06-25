using StardewValley;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout
{
    internal static class InventoryMenuConstructorLayout
    {
        public static void Apply(
            ref int yPosition,
            ref IList<Item> actualInventory,
            ref bool playerInventory,
            ref int capacity,
            ref int rows,
            IClickableMenu? currentParentMenu,
            bool isCalledFromMuseum
        )
        {
            // Current behavior must remain scoped to the player's inventory.
            // Storage/chest inventories are intentionally not changed yet; the storage
            // framework below is API preparation only and will be wired in a future patch.
            if (actualInventory is not null && actualInventory != Game1.player.Items)
                return;

            ApplyPlayerInventoryLayout(
                ref yPosition,
                ref capacity,
                ref rows,
                playerInventory,
                actualInventory,
                currentParentMenu,
                isCalledFromMuseum
            );
        }

        private static void ApplyPlayerInventoryLayout(
            ref int yPosition,
            ref int capacity,
            ref int rows,
            bool playerInventory,
            IList<Item>? actualInventory,
            IClickableMenu? currentParentMenu,
            bool isCalledFromMuseum
        )
        {
            if (isCalledFromMuseum)
            {
                rows = InventoryGridMetrics.DefaultRowCount;
                capacity = rows * InventoryGridMetrics.DefaultColumnCount;
                return;
            }

            if (!InventoryGridMetrics.PlayerHasExpandedInventory(actualInventory))
                return;

            if (currentParentMenu is ItemGrabMenu grabMenu)
            {
                rows = InventoryGridMetrics.GetRowsForLargeStorage(
                    grabMenu,
                    yPosition,
                    rows
                );
                capacity = rows * InventoryGridMetrics.DefaultColumnCount;
                return;
            }

            int dynamicMax = InventoryGridMetrics.GetDynamicMaxRows();
            if (rows > dynamicMax)
            {
                rows = dynamicMax;
                capacity = rows * InventoryGridMetrics.DefaultColumnCount;
            }
            else if (rows == InventoryGridMetrics.DefaultRowCount)
            {
                if (playerInventory)
                {
                    int extraSpace = InventoryGridMetrics.GetExtraHeight();
                    yPosition -= extraSpace;
                }

                rows = InventoryGridMetrics.GetPlayerVisibleRows(actualInventory);
                capacity = rows * InventoryGridMetrics.DefaultColumnCount;
            }
        }

        private static void ApplyStorageInventoryLayout(
            ref int yPosition,
            ref int capacity,
            ref int rows,
            IList<Item> actualInventory,
            IClickableMenu? currentParentMenu
        )
        {
            if (currentParentMenu is not MenuWithInventory menuWithInventory)
                return;

            if (!InventoryGridMetrics.ShouldVirtualizeStorageInventory(actualInventory, capacity, rows))
                return;

            rows = InventoryGridMetrics.GetRowsForLargeStorage(
                menuWithInventory,
                yPosition,
                rows
            );
            capacity = rows * InventoryGridMetrics.DefaultColumnCount;
        }
    }
}
