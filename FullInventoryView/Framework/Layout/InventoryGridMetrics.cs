using StardewValley;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout
{
    internal static class InventoryGridMetrics
    {
        public const int DefaultRowHeight = 64;
        public const int DefaultColumnCount = 12;
        public const int DefaultRowCount = 3;
        public const int MaxRowCount = 7;
        public const int DefaultMaxItems = 36;

        public static int GetDynamicMaxRows()
        {
            int reservedHeight = 500;
            int availableHeight = Game1.uiViewport.Height - reservedHeight;
            int maxRows = availableHeight / DefaultRowHeight;
            return Math.Clamp(maxRows, DefaultRowCount, MaxRowCount);
        }

        public static int GetCurrentPlayerSlotCount(IList<Item>? playerInventoryOverride = null)
        {
            if (Game1.player == null)
                return 0;

            int count = Game1.player.maxItems.Value;
            if (Game1.player.Items != null)
                count = Math.Max(count, Game1.player.Items.Count);
            if (playerInventoryOverride != null)
                count = Math.Max(count, playerInventoryOverride.Count);

            return count;
        }

        public static bool PlayerHasExpandedInventory(IList<Item>? playerInventoryOverride = null)
        {
            return GetCurrentPlayerSlotCount(playerInventoryOverride) > DefaultMaxItems;
        }

        public static int GetPlayerVisibleRows(IList<Item>? playerInventoryOverride = null)
        {
            int slotCount = GetCurrentPlayerSlotCount(playerInventoryOverride);
            if (slotCount <= DefaultMaxItems)
                return DefaultRowCount;

            int maxAllowed = GetDynamicMaxRows();
            int rows = GetRequiredRows(slotCount, DefaultColumnCount);
            return Math.Min(rows, maxAllowed);
        }


        public static int GetEffectiveSlotCount(IList<Item>? inventory, bool isPlayerInventory = false)
        {
            int count = inventory?.Count ?? 0;
            if (isPlayerInventory && Game1.player != null)
                count = Math.Max(count, GetCurrentPlayerSlotCount(inventory));

            return count;
        }

        public static int GetRequiredRows(int slotCount, int columns = DefaultColumnCount)
        {
            if (slotCount <= 0)
                return 0;

            if (columns <= 0)
                columns = DefaultColumnCount;

            return (slotCount + columns - 1) / columns;
        }

        public static int GetTotalRows(IList<Item> inventory)
        {
            return GetRequiredRows(inventory.Count, DefaultColumnCount);
        }

        public static int GetExtraRow()
        {
            return Math.Max(0, GetPlayerVisibleRows() - DefaultRowCount);
        }

        public static int GetExtraHeight()
        {
            return GetExtraHeightForRows(GetPlayerVisibleRows());
        }

        public static int GetExtraHeightForRows(int rows)
        {
            int extraRows = Math.Max(0, rows - DefaultRowCount);
            if (extraRows <= 0)
                return 0;

            return (extraRows * DefaultRowHeight)
                + ((extraRows - 1) * IClickableMenu.spaceBetweenTabs);
        }

        public static int GetBillboardOffset()
        {
            int extra = GetExtraHeight();
            if (extra <= 0)
                return 0;

            return extra - DefaultRowHeight;
        }

        public static int GetColumns(int capacity, int rows)
        {
            if (rows <= 0)
                return DefaultColumnCount;

            int columns = capacity / rows;
            return columns > 0 ? columns : DefaultColumnCount;
        }

        public static int GetRowsThatFitInsideParent(
            IClickableMenu parentMenu,
            int inventoryYPosition,
            int fallbackRows
        )
        {
            int usableBottom = parentMenu.yPositionOnScreen
                + parentMenu.height
                - 192
                - IClickableMenu.borderWidth;
            int availableHeight = usableBottom - inventoryYPosition + DefaultRowHeight;
            int spacing = IClickableMenu.spaceBetweenTabs;
            int rowPitch = DefaultRowHeight + spacing;
            int rows = (availableHeight + 2 * spacing) / rowPitch;

            if (rows <= 0)
                rows = fallbackRows;

            return Math.Clamp(rows, DefaultRowCount, MaxRowCount);
        }

        public static int GetRowsForLargeStorage(
            IClickableMenu parentMenu,
            int inventoryYPosition,
            int currentRows
        )
        {
            int rowsThatFit = GetRowsThatFitInsideParent(parentMenu, inventoryYPosition, currentRows);

            // Keep the exact old special-case behavior for HugeChestMenu/Roger-style menus,
            // but make the rule live in layout metrics instead of the patcher.
            if (parentMenu.GetType().Name == "HugeChestMenu")
                return 4;

            return rowsThatFit;
        }

        public static bool ShouldVirtualizeStorageInventory(
            IList<Item>? actualInventory,
            int capacity,
            int rows
        )
        {
            if (actualInventory == null)
                return false;

            return actualInventory.Count > DefaultMaxItems
                || capacity > DefaultMaxItems
                || rows > MaxRowCount;
        }
    }
}
