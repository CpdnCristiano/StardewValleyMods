using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher
{
    internal static class GridViewportLayoutHelpers
    {
        public static bool BoundsOverlapHorizontally(Rectangle a, Rectangle b, int tolerance)
        {
            return a.Right >= b.Left - tolerance && a.Left <= b.Right + tolerance;
        }

        public static void WireSideColumnNavigation(
            List<ClickableComponent> slotColumn,
            List<ClickableComponent> buttonColumn,
            List<ClickableComponent> bottomComponents,
            int startingDynamicId,
            bool columnIsOnLeft
        )
        {
            buttonColumn = buttonColumn.Distinct().OrderBy(c => c.bounds.Center.Y).ToList();
            bottomComponents = bottomComponents.Distinct().ToList();

            int dynamicId = startingDynamicId;
            foreach (var comp in buttonColumn)
            {
                if (comp.myID == -1)
                    comp.myID = dynamicId++;
            }
            foreach (var comp in bottomComponents)
            {
                if (comp.myID == -1)
                    comp.myID = dynamicId++;
            }

            for (int i = 0; i < buttonColumn.Count; i++)
            {
                var comp = buttonColumn[i];
                comp.upNeighborID = i > 0 ? buttonColumn[i - 1].myID : -1;
                comp.downNeighborID = i < buttonColumn.Count - 1 ? buttonColumn[i + 1].myID : -1;

                ClickableComponent? closestSlot = null;
                int minDistance = int.MaxValue;
                foreach (var slot in slotColumn)
                {
                    int dist = Math.Abs(slot.bounds.Center.Y - comp.bounds.Center.Y);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestSlot = slot;
                    }
                }

                if (closestSlot != null)
                {
                    if (columnIsOnLeft)
                        comp.rightNeighborID = closestSlot.myID;
                    else
                        comp.leftNeighborID = closestSlot.myID;
                }
            }

            foreach (var slot in slotColumn)
            {
                ClickableComponent? closestButton = null;
                int minDistance = int.MaxValue;
                foreach (var comp in buttonColumn)
                {
                    int dist = Math.Abs(comp.bounds.Center.Y - slot.bounds.Center.Y);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestButton = comp;
                    }
                }

                if (closestButton != null)
                {
                    if (columnIsOnLeft)
                        slot.leftNeighborID = closestButton.myID;
                    else
                        slot.rightNeighborID = closestButton.myID;
                }
            }
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

            int chestRightEdge = chestSlots.Max(s => s.bounds.Right) - 16;
            int fullMinY =
                Math.Min(chestSlots.Min(s => s.bounds.Top), playerSlots.Min(s => s.bounds.Top)) - 16;
            int fullMaxY =
                Math.Max(chestSlots.Max(s => s.bounds.Bottom), playerSlots.Max(s => s.bounds.Bottom)) + 16;

            foreach (var component in menu.allClickableComponents)
            {
                if (component == null)
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
                if (component.bounds.Center.X <= chestRightEdge || component.bounds.Center.X >= chestRightEdge + 300)
                    continue;
                if (component.bounds.Center.Y < fullMinY || component.bounds.Center.Y > fullMaxY)
                    continue;

                buttons.Add(component);
            }

            return buttons.Distinct().OrderBy(component => component.bounds.Center.Y).ToList();
        }
    }
}
