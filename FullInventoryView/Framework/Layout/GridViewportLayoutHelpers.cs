using Microsoft.Xna.Framework;
using CpdnCristiano.StardewValleyMod.Common.Log;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout
{
    internal static class GridViewportLayoutHelpers
    {
        private const int DropItemInvisibleButtonId = 107;
        private const int StardewDefaultProtectedButtonId = -500;

        public static bool IsProtectedComponent(ClickableComponent? component)
        {
            return component?.myID == DropItemInvisibleButtonId
                || component?.myID == StardewDefaultProtectedButtonId;
        }

        public static bool IsProtectedComponentId(int componentId)
        {
            return componentId == DropItemInvisibleButtonId
                || componentId == StardewDefaultProtectedButtonId;
        }

        public static bool BoundsOverlapHorizontally(Rectangle a, Rectangle b, int tolerance)
        {
            return a.Right >= b.Left - tolerance && a.Left <= b.Right + tolerance;
        }

        public static void SetUpNeighbor(ClickableComponent component, int neighborId)
        {
            if (IsProtectedComponent(component) || IsProtectedComponentId(neighborId))
                return;
            component.upNeighborID = neighborId;
        }

        public static void SetDownNeighbor(ClickableComponent component, int neighborId)
        {
            if (IsProtectedComponent(component) || IsProtectedComponentId(neighborId))
                return;
            component.downNeighborID = neighborId;
        }

        public static void SetLeftNeighbor(ClickableComponent component, int neighborId)
        {
            if (IsProtectedComponent(component) || IsProtectedComponentId(neighborId))
                return;
            component.leftNeighborID = neighborId;
        }

        public static void SetRightNeighbor(ClickableComponent component, int neighborId)
        {
            if (IsProtectedComponent(component) || IsProtectedComponentId(neighborId))
                return;
            component.rightNeighborID = neighborId;
        }

        public static void SetBounds(ClickableComponent component, Rectangle bounds)
        {
            if (IsProtectedComponent(component))
                return;
            component.bounds = bounds;
        }

        public static void SetBoundsX(ClickableComponent component, int x)
        {
            if (IsProtectedComponent(component))
                return;
            component.bounds.X = x;
        }

        public static void SetBoundsY(ClickableComponent component, int y)
        {
            if (IsProtectedComponent(component))
                return;
            component.bounds.Y = y;
        }

        public static void OffsetBoundsX(ClickableComponent component, int deltaX)
        {
            if (IsProtectedComponent(component))
                return;
            component.bounds.X += deltaX;
        }

        public static void WireSideColumnNavigation(
            List<ClickableComponent> slotColumn,
            List<ClickableComponent> buttonColumn,
            List<ClickableComponent> bottomComponents,
            int startingDynamicId,
            bool columnIsOnLeft,
            bool wrapOutwardToDrop = false
        )
        {
            WireSideColumnsNavigation(slotColumn, buttonColumn, startingDynamicId, columnIsOnLeft, wrapOutwardToDrop);
        }

        public static void WireSideColumnsNavigation(
            List<ClickableComponent> slotColumn,
            List<ClickableComponent> sideButtons,
            int startingDynamicId,
            bool buttonsAreOnLeft,
            bool wrapOutwardToDrop = false
        )
        {
            slotColumn = slotColumn.Where(c => c != null && !IsProtectedComponent(c)).Distinct().ToList();
            sideButtons = sideButtons
                .Where(c => c != null && !IsProtectedComponent(c))
                .Distinct()
                .OrderBy(c => c.bounds.Center.X)
                .ThenBy(c => c.bounds.Center.Y)
                .ToList();

            if (sideButtons.Count == 0)
                return;

            int dynamicId = startingDynamicId;
            var usedIds = new HashSet<int>(slotColumn
                .Select(c => c.myID)
                .Where(id => id >= 0));
            foreach (var comp in sideButtons)
            {
                bool invalidOrSentinelId = comp.myID < 0;
                bool idAlreadyOwned = comp.myID >= 0 && usedIds.Contains(comp.myID);

                // Directional gamepad navigation resolves neighbors by myID, so any unassigned
                // non-protected side button that passed our filters gets a stable positive dynamic ID.
                // Protected components (107 and -500) were filtered out before this point.
                if (invalidOrSentinelId || idAlreadyOwned)
                {
                    while (usedIds.Contains(dynamicId))
                        dynamicId++;
                    comp.myID = dynamicId++;
                }

                usedIds.Add(comp.myID);
            }

            var columns = GroupColumns(sideButtons, 24);
            foreach (var column in columns)
            {
                for (int i = 0; i < column.Count; i++)
                {
                    var comp = column[i];
                    if (i > 0)
                        SetUpNeighbor(comp, column[i - 1].myID);
                    if (i < column.Count - 1)
                        SetDownNeighbor(comp, column[i + 1].myID);
                }
            }

            foreach (var comp in sideButtons)
            {
                var leftButton = FindClosestHorizontalButton(comp, columns, toLeft: true);
                var rightButton = FindClosestHorizontalButton(comp, columns, toLeft: false);

                if (leftButton != null)
                    SetLeftNeighbor(comp, leftButton.myID);
                else if (!buttonsAreOnLeft)
                {
                    var closestSlot = FindClosestByY(slotColumn, comp.bounds.Center.Y);
                    if (closestSlot != null)
                        SetLeftNeighbor(comp, closestSlot.myID);
                }
                else if (wrapOutwardToDrop)
                {
                    // InventoryPage-only 360 wrap. ItemGrabMenu must keep its native drop
                    // area behavior and must not have side buttons force-linked to 107.
                    SetLeftNeighbor(comp, DropItemInvisibleButtonId);
                    Log.Debug($"[FIV/NavGraph] inventory-page outward wrap left -> drop: button={GridViewport.DescribeComponent(comp)}");
                }

                if (rightButton != null)
                    SetRightNeighbor(comp, rightButton.myID);
                else if (buttonsAreOnLeft)
                {
                    var closestSlot = FindClosestByY(slotColumn, comp.bounds.Center.Y);
                    if (closestSlot != null)
                        SetRightNeighbor(comp, closestSlot.myID);
                }
                else if (wrapOutwardToDrop)
                {
                    // InventoryPage-only 360 wrap. ItemGrabMenu must keep its native drop
                    // area behavior and must not have side buttons force-linked to 107.
                    SetRightNeighbor(comp, DropItemInvisibleButtonId);
                    Log.Debug($"[FIV/NavGraph] inventory-page outward wrap right -> drop: button={GridViewport.DescribeComponent(comp)}");
                }
            }

            foreach (var slot in slotColumn)
            {
                var closestButton = FindClosestByY(sideButtons, slot.bounds.Center.Y);
                if (closestButton == null)
                    continue;

                if (buttonsAreOnLeft)
                    SetLeftNeighbor(slot, closestButton.myID);
                else
                    SetRightNeighbor(slot, closestButton.myID);
            }
        }

        private static List<List<ClickableComponent>> GroupColumns(
            List<ClickableComponent> buttons,
            int tolerance
        )
        {
            var columns = new List<List<ClickableComponent>>();
            foreach (var button in buttons.OrderBy(c => c.bounds.Center.X).ThenBy(c => c.bounds.Center.Y))
            {
                var column = columns.FirstOrDefault(c =>
                    Math.Abs(c[0].bounds.Center.X - button.bounds.Center.X) <= tolerance
                );
                if (column == null)
                {
                    column = new List<ClickableComponent>();
                    columns.Add(column);
                }
                column.Add(button);
            }

            foreach (var column in columns)
            {
                column.Sort((a, b) => a.bounds.Center.Y.CompareTo(b.bounds.Center.Y));
            }
            columns.Sort((a, b) => a.Average(c => c.bounds.Center.X).CompareTo(b.Average(c => c.bounds.Center.X)));
            return columns;
        }

        private static ClickableComponent? FindClosestByY(
            List<ClickableComponent> components,
            int centerY
        )
        {
            ClickableComponent? closest = null;
            int bestDistance = int.MaxValue;
            foreach (var component in components)
            {
                int distance = Math.Abs(component.bounds.Center.Y - centerY);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closest = component;
                }
            }
            return closest;
        }

        private static ClickableComponent? FindClosestHorizontalButton(
            ClickableComponent source,
            List<List<ClickableComponent>> columns,
            bool toLeft
        )
        {
            ClickableComponent? closest = null;
            int bestScore = int.MaxValue;
            int sourceX = source.bounds.Center.X;
            int sourceY = source.bounds.Center.Y;

            foreach (var column in columns)
            {
                if (column.Contains(source))
                    continue;

                int columnX = (int)column.Average(c => c.bounds.Center.X);
                if (toLeft && columnX >= sourceX)
                    continue;
                if (!toLeft && columnX <= sourceX)
                    continue;

                foreach (var candidate in column)
                {
                    int xDistance = Math.Abs(candidate.bounds.Center.X - sourceX);
                    int yDistance = Math.Abs(candidate.bounds.Center.Y - sourceY);
                    int score = (xDistance * 4) + yDistance;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        closest = candidate;
                    }
                }
            }
            return closest;
        }

        public static List<ClickableComponent> GetComponentsOnAxisX(
            IClickableMenu menu,
            ClickableComponent anchorBtn,
            int xTolerance,
            int minY,
            int maxY,
            ClickableComponent? excludedButton,
            ClickableComponent? upArrow,
            ClickableComponent? downArrow,
            List<ClickableComponent> chestSlots,
            List<ClickableComponent> playerSlots
        )
        {
            var alignedButtons = new List<ClickableComponent>();
            if (menu.allClickableComponents == null)
                return alignedButtons;

            int anchorCenterX = anchorBtn.bounds.Center.X;
            foreach (var component in menu.allClickableComponents)
            {
                if (component == null)
                    continue;
                if (IsProtectedComponent(component))
                    continue;
                if (IsDropAreaComponent(menu, component))
                    continue;
                if (component == upArrow || component == downArrow || component == excludedButton)
                    continue;
                if (component == menu.upperRightCloseButton || component.name == "upperRightCloseButton")
                    continue;
                if (chestSlots.Contains(component) || playerSlots.Contains(component))
                    continue;
                if (Math.Abs(component.bounds.Center.X - anchorCenterX) > xTolerance)
                    continue;
                if (component.bounds.Center.Y < minY || component.bounds.Center.Y > maxY)
                    continue;

                alignedButtons.Add(component);
            }

            return alignedButtons.Distinct().OrderBy(component => component.bounds.Center.Y).ToList();
        }

        private static bool IsDropAreaComponent(IClickableMenu menu, ClickableComponent component)
        {
            if (component == null)
                return false;

            if (IsProtectedComponent(component))
                return true;

            if (menu is MenuWithInventory menuWithInventory
                && ReferenceEquals(component, menuWithInventory.dropItemInvisibleButton))
                return true;

            return false;
        }

        public static List<ClickableComponent> FindChestColumnButtons(
            IClickableMenu menu,
            List<ClickableComponent> chestSlots,
            List<ClickableComponent> playerSlots,
            ClickableComponent? excludedButton,
            ClickableComponent? upArrow,
            ClickableComponent? downArrow,
            ClickableComponent? okBtn,
            ClickableComponent? trashBtn
        )
        {
            var buttons = new List<ClickableComponent>();
            if (menu.allClickableComponents == null)
                return buttons;

            int slotsLeftEdge = Math.Min(chestSlots.Min(s => s.bounds.Left), playerSlots.Min(s => s.bounds.Left));
            int slotsRightEdge = Math.Max(chestSlots.Max(s => s.bounds.Right), playerSlots.Max(s => s.bounds.Right));
            int fullMinY =
                Math.Min(chestSlots.Min(s => s.bounds.Top), playerSlots.Min(s => s.bounds.Top)) - 16;
            int fullMaxY =
                Math.Max(chestSlots.Max(s => s.bounds.Bottom), playerSlots.Max(s => s.bounds.Bottom)) + 16;

            foreach (var component in menu.allClickableComponents)
            {
                if (component == null)
                    continue;
                if (IsProtectedComponent(component))
                    continue;
                if (IsDropAreaComponent(menu, component))
                    continue;
                if (IsUnidentifiedNonVisualComponent(component))
                    continue;
                if (
                    component == excludedButton
                    || component == upArrow
                    || component == downArrow
                    || component == okBtn
                    || component == trashBtn
                )
                    continue;
                if (component == menu.upperRightCloseButton || component.name == "upperRightCloseButton")
                    continue;
                if (chestSlots.Contains(component) || playerSlots.Contains(component))
                    continue;

                bool isRightSide = component.bounds.Center.X > slotsRightEdge && component.bounds.Center.X < slotsRightEdge + 300;
                bool isLeftSide = component.bounds.Center.X < slotsLeftEdge && component.bounds.Center.X > slotsLeftEdge - 300;
                if (!isRightSide && !isLeftSide)
                    continue;
                if (component.bounds.Center.Y < fullMinY || component.bounds.Center.Y > fullMaxY)
                    continue;

                buttons.Add(component);
            }

            return buttons.Distinct().OrderBy(component => component.bounds.Center.Y).ToList();
        }

        private static bool IsUnidentifiedNonVisualComponent(ClickableComponent component)
        {
            return IsProtectedComponent(component);
        }
    }
}
