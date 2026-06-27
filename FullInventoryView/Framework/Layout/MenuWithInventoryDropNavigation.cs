using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout
{
    internal static class MenuWithInventoryDropNavigation
    {
        private const int DropItemInvisibleButtonId = 107;

        public static void Preserve(IClickableMenu menu, IEnumerable<InventoryMenu> inventoryMenus)
        {
            // ItemGrabMenu has its own vanilla drop-area wiring. Do not rewrite the
            // invisible drop target there: the button must keep its original bounds and
            // native neighbor behavior. InventoryPage keeps the wrap behavior only in
            // the inventory screen, where vanilla already supports the 360 loop.
            if (menu is ItemGrabMenu)
                return;

            if (menu is not MenuWithInventory menuWithInventory)
                return;

            var dropButton = menuWithInventory.dropItemInvisibleButton;
            if (dropButton == null || GridViewportLayoutHelpers.IsProtectedComponent(dropButton))
                return;

            var orderedMenus = inventoryMenus
                .Where(m => m?.inventory != null && m.inventory.Count > 0)
                .OrderBy(m => m.yPositionOnScreen)
                .ToList();
            if (orderedMenus.Count == 0)
                return;

            // In ItemGrabMenu there are two grids: chest/top first, player/bottom second.
            // The invisible drop target belongs to the item-grab surface, and Stardew's
            // controller loop expects RIGHT from the drop target to return to the first
            // chest slot, not to a lower player/backpack slot or a side button from the
            // previous wrap. Keep this rule centralized so InventoryPage and ItemGrabMenu
            // don't drift into two different navigation models.
            ClickableComponent primaryReturnSlot = orderedMenus[0].inventory[0];

            dropButton.myID = DropItemInvisibleButtonId;
            menu.allClickableComponents ??= new List<ClickableComponent>();
            if (!menu.allClickableComponents.Contains(dropButton))
                menu.allClickableComponents.Add(dropButton);

            foreach (var inventoryMenu in orderedMenus)
            {
                PreserveForInventoryMenu(menu, inventoryMenu, dropButton, primaryReturnSlot);
            }

            GridViewportLayoutHelpers.SetRightNeighbor(dropButton, primaryReturnSlot.myID);
        }

        private static void PreserveForInventoryMenu(
            IClickableMenu menu,
            InventoryMenu inventoryMenu,
            ClickableComponent dropButton,
            ClickableComponent primaryReturnSlot
        )
        {
            var slots = inventoryMenu.inventory?.Where(c => c != null).ToList();
            if (slots == null || slots.Count == 0)
                return;

            var firstSlot = slots[0];
            var leftSideButtons = FindLeftSideButtons(menu, slots, dropButton);

            if (leftSideButtons.Count > 0)
            {
                WireDropThroughLeftSideButtons(dropButton, firstSlot, leftSideButtons, primaryReturnSlot);
                return;
            }

            // Vanilla maps the first inventory slot directly to the invisible drop target.
            // Keep that behavior whenever there is no left-side scroll/button column between
            // the slot and the drop area.
            GridViewportLayoutHelpers.SetLeftNeighbor(firstSlot, dropButton.myID);
            GridViewportLayoutHelpers.SetRightNeighbor(dropButton, primaryReturnSlot.myID);
        }

        private static List<ClickableComponent> FindLeftSideButtons(
            IClickableMenu menu,
            List<ClickableComponent> slots,
            ClickableComponent dropButton
        )
        {
            if (menu.allClickableComponents == null || slots.Count == 0)
                return new List<ClickableComponent>();

            int slotsLeft = slots.Min(s => s.bounds.Left);
            int slotsTop = slots.Min(s => s.bounds.Top) - 24;
            int slotsBottom = slots.Max(s => s.bounds.Bottom) + 24;
            var slotSet = new HashSet<ClickableComponent>(slots);

            return menu
                .allClickableComponents
                .Where(c =>
                    c != null
                    && !GridViewportLayoutHelpers.IsProtectedComponent(c)
                    && c != dropButton
                    && !slotSet.Contains(c)
                    && c != menu.upperRightCloseButton
                    && c.name != "upperRightCloseButton"
                    && c.bounds.Center.X < slotsLeft
                    && c.bounds.Center.X > slotsLeft - 360
                    && c.bounds.Center.Y >= slotsTop
                    && c.bounds.Center.Y <= slotsBottom
                )
                .Distinct()
                .OrderBy(c => c.bounds.Center.X)
                .ThenBy(c => c.bounds.Center.Y)
                .ToList();
        }

        private static void WireDropThroughLeftSideButtons(
            ClickableComponent dropButton,
            ClickableComponent firstSlot,
            List<ClickableComponent> leftSideButtons,
            ClickableComponent primaryReturnSlot
        )
        {
            leftSideButtons = leftSideButtons
                .Where(button => button != null && !GridViewportLayoutHelpers.IsProtectedComponent(button))
                .Distinct()
                .ToList();

            foreach (var button in leftSideButtons)
            {
                if (button.myID == -1)
                    button.myID = 170000 + leftSideButtons.IndexOf(button);
            }

            var outerLeftColumn = GroupColumns(leftSideButtons, 24)
                .OrderBy(column => column.Average(c => c.bounds.Center.X))
                .FirstOrDefault();

            if (outerLeftColumn == null || outerLeftColumn.Count == 0)
            {
                GridViewportLayoutHelpers.SetLeftNeighbor(firstSlot, dropButton.myID);
                GridViewportLayoutHelpers.SetRightNeighbor(dropButton, primaryReturnSlot.myID);
                return;
            }

            // If the left-side arrows/buttons take over the slot's LEFT navigation, the drop
            // target must become the next LEFT step from the outermost left column. This keeps
            // both paths reachable:
            //   slot 0 -> left side column -> drop area
            // instead of letting the side column swallow access to the drop area.
            foreach (var button in outerLeftColumn)
            {
                GridViewportLayoutHelpers.SetLeftNeighbor(button, dropButton.myID);
            }

            // Do not point the drop target back to the side-column button that sent us here.
            // RIGHT from the invisible drop target must always return to the primary slot 0
            // chosen by Preserve(), otherwise the 360 wrap feels reversed.
            GridViewportLayoutHelpers.SetRightNeighbor(dropButton, primaryReturnSlot.myID);
        }

        private static List<List<ClickableComponent>> GroupColumns(
            List<ClickableComponent> buttons,
            int tolerance
        )
        {
            var columns = new List<List<ClickableComponent>>();
            foreach (var button in buttons)
            {
                var column = columns.FirstOrDefault(c =>
                    System.Math.Abs(c[0].bounds.Center.X - button.bounds.Center.X) <= tolerance
                );
                if (column == null)
                {
                    column = new List<ClickableComponent>();
                    columns.Add(column);
                }
                column.Add(button);
            }

            foreach (var column in columns)
                column.Sort((a, b) => a.bounds.Center.Y.CompareTo(b.bounds.Center.Y));

            return columns;
        }

        private static ClickableComponent? FindClosestByY(
            IEnumerable<ClickableComponent> components,
            int centerY
        )
        {
            ClickableComponent? closest = null;
            int bestDistance = int.MaxValue;
            foreach (var component in components)
            {
                int distance = System.Math.Abs(component.bounds.Center.Y - centerY);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closest = component;
                }
            }

            return closest;
        }
    }
}
