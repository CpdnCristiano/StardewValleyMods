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

        public static int GetPlayerVisibleRows()
        {
            if (Game1.player.maxItems.Value <= DefaultMaxItems)
                return DefaultRowCount;

            int maxAllowed = GetDynamicMaxRows();
            int rows = Game1.player.maxItems.Value / DefaultColumnCount;
            return Math.Min(rows, maxAllowed);
        }

        public static int GetTotalRows(IList<Item> inventory)
        {
            return Math.Max(0, (inventory.Count + DefaultColumnCount - 1) / DefaultColumnCount);
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
