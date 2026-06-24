using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher
{
    public class GridViewport
    {
        public enum SideLayoutPreference
        {
            Right,
            Left,
        }

        public InventoryMenu Menu { get; }
        public int ScrollRow { get; set; } = 0;

        public ClickableTextureComponent UpArrow { get; private set; }
        public ClickableTextureComponent DownArrow { get; private set; }
        public ClickableTextureComponent ScrollBarThumb { get; private set; }
        public Rectangle ScrollBarRunner { get; private set; }
        public bool ShowAuxScrollBar { get; private set; } = false;
        public bool IsDraggingAuxScrollBar { get; private set; } = false;
        public int AuxScrollDragOffset { get; private set; } = 0;
        public bool HasResolvedCustomLayout { get; set; } = false;

        public int LastRightStickDirection { get; set; } = 0;
        public int LastPageComponentsHash { get; set; } = int.MinValue;

        public int Depth { get; set; } = 0;
        public IList<Item>? FullInventory { get; set; }
        public IList<Item>? OriginalInventory { get; set; }
        public int? OriginalMaxItems { get; set; }
        public Dictionary<ClickableComponent, int> OriginalButtonY { get; } = new();
        public Dictionary<ClickableComponent, Rectangle> OriginalButtonBounds { get; } = new();

        public GridViewport(InventoryMenu menu)
        {
            this.Menu = menu;
            this.UpArrow = new ClickableTextureComponent(
                "Scroll Up",
                new Rectangle(0, 0, 44, 48),
                null,
                "Scroll Up",
                Game1.mouseCursors,
                new Rectangle(421, 459, 11, 12),
                4f
            );
            this.DownArrow = new ClickableTextureComponent(
                "Scroll Down",
                new Rectangle(0, 0, 44, 48),
                null,
                "Scroll Down",
                Game1.mouseCursors,
                new Rectangle(421, 472, 11, 12),
                4f
            );
            this.ScrollBarThumb = new ClickableTextureComponent(
                "Scroll Thumb",
                new Rectangle(0, 0, 24, 40),
                null,
                "Scroll Thumb",
                Game1.mouseCursors,
                new Rectangle(435, 463, 6, 10),
                4f
            );
            this.ScrollBarRunner = Rectangle.Empty;
        }

        public bool CustomArrowLayout { get; set; } = false;

        public sealed class SideButtonLayoutContext
        {
            public IClickableMenu Menu { get; init; } = null!;
            public List<ClickableComponent> ChestSlots { get; init; } = null!;
            public List<ClickableComponent> PlayerSlots { get; init; } = null!;
            public int ChestColumns { get; init; }
            public int PlayerColumns { get; init; }
            public SideLayoutPreference PreferredSide { get; init; } = SideLayoutPreference.Right;
            public int PreferredSideOffsetPixels { get; init; } = 28;
            public int? ArrowAnchorCenterXOverride { get; init; }
            public ClickableComponent? ArrowAnchorComponentOverride { get; init; }
            public bool CenterOkBetweenArrows { get; init; }
            public ClickableComponent? ColorButton { get; init; }
            public ClickableComponent? FillButton { get; init; }
            public ClickableComponent? OrganizeButton { get; init; }
            public ClickableComponent? OkButton { get; init; }
            public ClickableComponent? TrashButton { get; init; }
            public ClickableTextureComponent? PickerToggle { get; init; }
            public DiscreteColorPicker? ColorPicker { get; init; }
        }

        public void LayoutArrows()
        {
            if (this.CustomArrowLayout)
                return;
            var slots = this.Menu.inventory;
            if (slots == null || slots.Count == 0)
                return;

            int columns = this.Menu.capacity / this.Menu.rows;
            if (columns <= 0)
                columns = 12;

            int lastColIndex = columns - 1;
            int bottomIndex = (this.Menu.rows - 1) * columns;

            var topAnchor = slots[Math.Min(slots.Count - 1, lastColIndex)];
            var bottomAnchor = slots[Math.Min(slots.Count - 1, bottomIndex + lastColIndex)];

            ClickableTextureComponent? organizeButton = null;
            if (Game1.activeClickableMenu != null)
            {
                var menuType = Game1.activeClickableMenu.GetType();
                var orgField = menuType.GetField(
                    "organizeButton",
                    System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (orgField != null)
                {
                    organizeButton =
                        orgField.GetValue(Game1.activeClickableMenu) as ClickableTextureComponent;
                }
                else if (
                    Game1.activeClickableMenu is GameMenu gameMenu
                    && gameMenu.GetCurrentPage() is InventoryPage page
                )
                {
                    organizeButton = page.organizeButton;
                }
            }

            int targetX =
                organizeButton != null
                    ? organizeButton.bounds.Center.X - (this.UpArrow.bounds.Width / 2)
                    : topAnchor.bounds.Right + 24;

            this.UpArrow.bounds = new Rectangle(targetX, topAnchor.bounds.Top, 44, 48);
            this.DownArrow.bounds = new Rectangle(
                targetX,
                bottomAnchor.bounds.Bottom,
                44,
                48
            );
        }

        public void EnsureClickableComponents(IClickableMenu menu)
        {
            menu.allClickableComponents ??= new List<ClickableComponent>();
            if (!menu.allClickableComponents.Contains(this.UpArrow))
                menu.allClickableComponents.Add(this.UpArrow);
            if (!menu.allClickableComponents.Contains(this.DownArrow))
                menu.allClickableComponents.Add(this.DownArrow);
        }

        public void LayoutInventoryPageButtons(
            InventoryPage page,
            List<ClickableComponent> slots,
            ClickableTextureComponent? organizeButton
        )
        {
            this.HasResolvedCustomLayout = true;
            if (slots.Count == 0)
                return;

            ClickableComponent firstSlot = slots[0];
            int columns = this.Menu.capacity / this.Menu.rows;
            if (columns <= 0)
                columns = 12;

            int bottomSlotIndex = Math.Min(slots.Count - 1, (this.Menu.rows - 1) * columns);
            ClickableComponent lastRowAnchor = slots[bottomSlotIndex];

            int anchorCenterX =
                organizeButton != null
                    ? organizeButton.bounds.Center.X
                    : lastRowAnchor.bounds.Right + 32;

            this.UpArrow.bounds.X = anchorCenterX - (this.UpArrow.bounds.Width / 2);
            this.UpArrow.bounds.Y = firstSlot.bounds.Top;
            this.DownArrow.bounds.X = anchorCenterX - (this.DownArrow.bounds.Width / 2);
            this.DownArrow.bounds.Y = lastRowAnchor.bounds.Bottom - this.DownArrow.bounds.Height;

            var rightEdge = lastRowAnchor.bounds.Right - 16;
            var middleButtons = new List<ClickableComponent>();

            if (page.allClickableComponents != null)
            {
                foreach (var c in page.allClickableComponents)
                {
                    if (c == null)
                        continue;
                    if (c == page.trashCan || c.name == "trashCan")
                        continue;
                    if (c == page.upperRightCloseButton || c.name == "upperRightCloseButton")
                        continue;
                    if (c == this.UpArrow || c.name == "Scroll Up")
                        continue;
                    if (c == this.DownArrow || c.name == "Scroll Down")
                        continue;

                    bool isAlignedX = organizeButton != null
                        ? c.bounds.Right >= organizeButton.bounds.Left
                            && c.bounds.Left <= organizeButton.bounds.Right
                        : c.bounds.Center.X > rightEdge && c.bounds.Center.X < rightEdge + 300;

                    if (
                        isAlignedX
                        && c.bounds.Center.Y >= firstSlot.bounds.Y - 16
                        && c.bounds.Center.Y <= lastRowAnchor.bounds.Bottom + 16
                    )
                    {
                        middleButtons.Add(c);
                    }
                }
            }

            if (organizeButton != null && !middleButtons.Contains(organizeButton))
                middleButtons.Add(organizeButton);

            middleButtons = middleButtons.Distinct().ToList();
            if (middleButtons.Count == 0)
                return;

            foreach (var b in middleButtons)
            {
                if (!this.OriginalButtonBounds.ContainsKey(b))
                    this.OriginalButtonBounds[b] = b.bounds;
                if (!this.OriginalButtonY.ContainsKey(b))
                    this.OriginalButtonY[b] = b.bounds.Y;
            }

            middleButtons = middleButtons.OrderBy(b => this.OriginalButtonY[b]).ToList();
            if (organizeButton != null)
            {
                middleButtons.Remove(organizeButton);
                middleButtons.Insert(middleButtons.Count / 2, organizeButton);
            }

            int startY = this.UpArrow.bounds.Bottom + 16;
            int endY = this.DownArrow.bounds.Top - 16;
            int availableHeight = endY - startY;
            int totalButtonsHeight = middleButtons.Sum(b => b.bounds.Height);
            int spacing = 8;
            int stackHeight = totalButtonsHeight + (spacing * (middleButtons.Count - 1));
            int currentY = startY + (availableHeight - stackHeight) / 2;

            foreach (var b in middleButtons)
            {
                b.bounds.X = anchorCenterX - (b.bounds.Width / 2);
                b.bounds.Y = currentY;
                currentY += b.bounds.Height + spacing;
            }
        }

        public void LayoutSideButtons(SideButtonLayoutContext context)
        {
            this.HasResolvedCustomLayout = true;
            bool preferLeftSide = context.PreferredSide == SideLayoutPreference.Left;
            int preferredOffset = context.PreferredSideOffsetPixels;
            var topAnchorBtn =
                context.ColorButton
                ?? context.FillButton
                ?? context.OrganizeButton
                ?? context.OkButton
                ?? context.TrashButton;
            var sideAnchorBtn =
                context.OkButton
                ?? context.TrashButton
                ?? context.OrganizeButton
                ?? context.FillButton
                ?? context.ColorButton;

            if (sideAnchorBtn == null)
            {
                int targetX;
                if (context.ArrowAnchorComponentOverride != null)
                {
                    targetX =
                        context.ArrowAnchorComponentOverride.bounds.Center.X
                        - (this.UpArrow.bounds.Width / 2);
                }
                else if (context.ArrowAnchorCenterXOverride.HasValue)
                {
                    targetX = context.ArrowAnchorCenterXOverride.Value - (this.UpArrow.bounds.Width / 2);
                }
                else
                {
                    targetX = preferLeftSide
                        ? context.PlayerSlots[0].bounds.Left - preferredOffset - this.UpArrow.bounds.Width
                        : context.PlayerSlots[context.PlayerColumns - 1].bounds.Right + preferredOffset;
                }
                this.UpArrow.bounds.X = targetX;
                this.UpArrow.bounds.Y = context.PlayerSlots[0].bounds.Top;
                this.DownArrow.bounds.X = targetX;
                this.DownArrow.bounds.Y = context.PlayerSlots[
                    Math.Min(context.PlayerSlots.Count - 1, (this.Menu.rows - 1) * context.PlayerColumns)
                ].bounds.Bottom - this.DownArrow.bounds.Height;

                var fallbackRightmostSlots = new List<ClickableComponent>();
                if (preferLeftSide)
                {
                    for (int idx = 0; idx < context.PlayerSlots.Count; idx += context.PlayerColumns)
                    {
                        fallbackRightmostSlots.Add(context.PlayerSlots[idx]);
                    }
                }
                else
                {
                    for (
                        int idx = context.PlayerColumns - 1;
                        idx < context.PlayerSlots.Count;
                        idx += context.PlayerColumns
                    )
                    {
                        fallbackRightmostSlots.Add(context.PlayerSlots[idx]);
                    }
                }

                GridViewportLayoutHelpers.WireSideColumnNavigation(
                    fallbackRightmostSlots,
                    new List<ClickableComponent> { this.UpArrow, this.DownArrow },
                    new List<ClickableComponent>(),
                    160000,
                    preferLeftSide
                );

                var fallbackButtons = new List<ClickableComponent> { this.UpArrow, this.DownArrow };
                this.UpdateAuxScrollBarVisibility(context, fallbackButtons);
                if (context.ArrowAnchorComponentOverride != null)
                {
                    bool hasIntermediateComponent = fallbackButtons.Any(c =>
                        c != null
                        && c != this.UpArrow
                        && c != this.DownArrow
                        && c != context.ArrowAnchorComponentOverride
                        && c.bounds.Center.Y > context.ArrowAnchorComponentOverride.bounds.Center.Y
                        && c.bounds.Center.Y < this.UpArrow.bounds.Center.Y
                    );

                    if (!hasIntermediateComponent)
                    {
                        context.ArrowAnchorComponentOverride.downNeighborID = this.UpArrow.myID;
                        this.UpArrow.upNeighborID = context.ArrowAnchorComponentOverride.myID;
                    }
                }
                return;
            }

            int minY =
                Math.Min(
                    context.ChestSlots.Min(s => s.bounds.Top),
                    context.PlayerSlots.Min(s => s.bounds.Top)
                ) - 16;
            int maxY =
                Math.Max(
                    context.ChestSlots.Max(s => s.bounds.Bottom),
                    context.PlayerSlots.Max(s => s.bounds.Bottom)
                ) + 16;

            var alignedButtons = GridViewportLayoutHelpers.GetComponentsOnAxisX(
                context.Menu,
                sideAnchorBtn,
                24,
                minY,
                maxY,
                context.PickerToggle,
                this.UpArrow,
                this.DownArrow,
                context.ChestSlots,
                context.PlayerSlots
            );

            var colorBtn = context.ColorButton ?? alignedButtons.FirstOrDefault();
            var fillBtn = context.FillButton ?? alignedButtons.FirstOrDefault(c => c != colorBtn);
            var organizeBtn = context.OrganizeButton;
            var okBtn = context.OkButton;
            var trashBtn = context.TrashButton;

            if (organizeBtn == null)
            {
                organizeBtn = alignedButtons.FirstOrDefault(c =>
                    c != colorBtn && c != fillBtn && c != okBtn && c != trashBtn
                );
            }

            int sideAnchorCenterX = sideAnchorBtn.bounds.Center.X;
            int topAnchorCenterX = (topAnchorBtn ?? sideAnchorBtn).bounds.Center.X;
            int arrowCenterX = preferLeftSide
                ? context.PlayerSlots[0].bounds.Left - preferredOffset - (this.UpArrow.bounds.Width / 2)
                : sideAnchorCenterX;
            if (context.ArrowAnchorComponentOverride != null)
            {
                arrowCenterX = context.ArrowAnchorComponentOverride.bounds.Center.X;
            }
            if (context.ArrowAnchorCenterXOverride.HasValue)
            {
                arrowCenterX = context.ArrowAnchorCenterXOverride.Value;
            }

            var chestTopColumn = GridViewportLayoutHelpers.FindChestColumnButtons(
                context.Menu,
                context.ChestSlots,
                context.PlayerSlots,
                context.PickerToggle,
                this.UpArrow,
                this.DownArrow,
                okBtn,
                trashBtn
            ).Distinct().ToList();

            if (organizeBtn == null)
            {
                organizeBtn = chestTopColumn.FirstOrDefault(c =>
                    c != colorBtn && c != fillBtn && c != okBtn && c != trashBtn
                );
            }

            if (chestTopColumn.Count == 0)
            {
                if (colorBtn != null)
                    chestTopColumn.Add(colorBtn);
                if (fillBtn != null && fillBtn != colorBtn)
                    chestTopColumn.Add(fillBtn);
                if (organizeBtn != null && organizeBtn != colorBtn && organizeBtn != fillBtn)
                    chestTopColumn.Add(organizeBtn);
            }

            int currentChestY = context.ChestSlots[0].bounds.Y;
            foreach (var button in chestTopColumn)
            {
                button.bounds.X = topAnchorCenterX - (button.bounds.Width / 2);
                button.bounds.Y = currentChestY;
                currentChestY += button.bounds.Height + 8;
            }

            int playerLastRowIndex = Math.Min(
                context.PlayerSlots.Count - 1,
                (this.Menu.rows - 1) * context.PlayerColumns
            );
            this.UpArrow.bounds.X = arrowCenterX - (this.UpArrow.bounds.Width / 2);
            this.UpArrow.bounds.Y = context.PlayerSlots[0].bounds.Top;
            this.DownArrow.bounds.X = arrowCenterX - (this.DownArrow.bounds.Width / 2);
            this.DownArrow.bounds.Y =
                context.PlayerSlots[playerLastRowIndex].bounds.Bottom
                - this.DownArrow.bounds.Height;

            if (okBtn != null)
            {
                okBtn.bounds.X = sideAnchorCenterX - (okBtn.bounds.Width / 2);
                if (context.CenterOkBetweenArrows)
                {
                    int centerY =
                        this.UpArrow.bounds.Bottom
                        + ((this.DownArrow.bounds.Top - this.UpArrow.bounds.Bottom - okBtn.bounds.Height) / 2);
                    okBtn.bounds.Y = centerY;
                }
                else
                {
                    okBtn.bounds.Y = this.DownArrow.bounds.Y - 8 - okBtn.bounds.Height;
                }
            }
            if (trashBtn != null)
            {
                int trashBaseY = okBtn != null ? okBtn.bounds.Y : this.DownArrow.bounds.Y;
                trashBtn.bounds.X = sideAnchorCenterX - (trashBtn.bounds.Width / 2);
                trashBtn.bounds.Y = trashBaseY - 8 - trashBtn.bounds.Height;
            }

            if (context.ColorPicker != null && context.PickerToggle != null && colorBtn != null)
            {
                context.PickerToggle.bounds = colorBtn.bounds;
            }

            var allSideButtons = new List<ClickableComponent>();
            allSideButtons.AddRange(chestTopColumn);
            if (trashBtn != null)
                allSideButtons.Add(trashBtn);
            if (okBtn != null)
                allSideButtons.Add(okBtn);
            allSideButtons.Add(this.UpArrow);
            allSideButtons.Add(this.DownArrow);
            allSideButtons = allSideButtons.Distinct().OrderBy(c => c.bounds.Y).ToList();

            foreach (var comp in allSideButtons)
            {
                if (!context.Menu.allClickableComponents.Contains(comp))
                    context.Menu.allClickableComponents.Add(comp);
            }

            var leftSlotColumn = new List<ClickableComponent>();
            for (int idx = 0; idx < context.ChestSlots.Count; idx += context.ChestColumns)
                leftSlotColumn.Add(context.ChestSlots[idx]);
            for (int idx = 0; idx < context.PlayerSlots.Count; idx += context.PlayerColumns)
                leftSlotColumn.Add(context.PlayerSlots[idx]);

            var rightSlotColumn = new List<ClickableComponent>();
            for (int idx = context.ChestColumns - 1; idx < context.ChestSlots.Count; idx += context.ChestColumns)
                rightSlotColumn.Add(context.ChestSlots[idx]);
            for (int idx = context.PlayerColumns - 1; idx < context.PlayerSlots.Count; idx += context.PlayerColumns)
                rightSlotColumn.Add(context.PlayerSlots[idx]);

            if (preferLeftSide)
            {
                GridViewportLayoutHelpers.WireSideColumnNavigation(
                    leftSlotColumn,
                    new List<ClickableComponent> { this.UpArrow, this.DownArrow },
                    new List<ClickableComponent>(),
                    160000,
                    true
                );

                var rightSideButtons = allSideButtons
                    .Where(c => c != this.UpArrow && c != this.DownArrow)
                    .ToList();
                if (rightSideButtons.Count > 0)
                {
                    GridViewportLayoutHelpers.WireSideColumnNavigation(
                        rightSlotColumn,
                        rightSideButtons,
                        new List<ClickableComponent>(),
                        161000,
                        false
                    );
                }
            }
            else
            {
                GridViewportLayoutHelpers.WireSideColumnNavigation(
                    rightSlotColumn,
                    allSideButtons,
                    new List<ClickableComponent>(),
                    160000,
                    false
                );
            }

            EnsureArrowAnchorBridge(context, allSideButtons);
        }

        private void EnsureArrowAnchorBridge(
            SideButtonLayoutContext context,
            List<ClickableComponent> allSideButtons
        )
        {
            this.UpdateAuxScrollBarVisibility(context, allSideButtons);

            var anchor = context.ArrowAnchorComponentOverride;
            if (anchor == null)
                return;

            bool hasIntermediateComponent = allSideButtons.Any(c =>
                c != null
                && c != this.UpArrow
                && c != this.DownArrow
                && c != anchor
                && c.bounds.Center.Y > anchor.bounds.Center.Y
                && c.bounds.Center.Y < this.UpArrow.bounds.Center.Y
            );

            if (!hasIntermediateComponent)
            {
                anchor.downNeighborID = this.UpArrow.myID;
                this.UpArrow.upNeighborID = anchor.myID;
            }
        }

        private void UpdateAuxScrollBarVisibility(
            SideButtonLayoutContext context,
            List<ClickableComponent> allSideButtons
        )
        {
            this.ShowAuxScrollBar = false;
            this.IsDraggingAuxScrollBar = false;
            this.ScrollBarRunner = Rectangle.Empty;

            bool hasIntermediateComponentBetweenArrows = allSideButtons.Any(c =>
                c != null
                && c != this.UpArrow
                && c != this.DownArrow
                && GridViewportLayoutHelpers.BoundsOverlapHorizontally(c.bounds, this.UpArrow.bounds, 24)
                && c.bounds.Center.Y > this.UpArrow.bounds.Center.Y
                && c.bounds.Center.Y < this.DownArrow.bounds.Center.Y
            );

            if (hasIntermediateComponentBetweenArrows)
                return;

            int runnerX = this.UpArrow.bounds.X + 12;
            int runnerY = this.UpArrow.bounds.Bottom + 4;
            int runnerBottom = this.DownArrow.bounds.Top - 4;
            int runnerHeight = Math.Max(8, runnerBottom - runnerY);

            this.ScrollBarRunner = new Rectangle(
                runnerX,
                runnerY,
                this.ScrollBarThumb.bounds.Width,
                runnerHeight
            );
            this.ShowAuxScrollBar = true;
        }

        public int GetTotalRows(IList<Item> items)
        {
            int columns = this.Menu.capacity / this.Menu.rows;
            if (columns <= 0)
                columns = 12;
            return Math.Max(0, (items.Count + columns - 1) / columns);
        }

        public int GetMaxScrollRow(IList<Item> items)
        {
            return Math.Max(0, this.GetTotalRows(items) - this.Menu.rows);
        }

        public bool CanScroll(IList<Item> items, int direction)
        {
            int next = this.ScrollRow + direction;
            return next >= 0 && next <= this.GetMaxScrollRow(items);
        }

        public void Scroll(IList<Item> items, int delta)
        {
            int max = this.GetMaxScrollRow(items);
            this.ScrollRow = Math.Clamp(this.ScrollRow + delta, 0, max);
            Game1.playSound("shwip");
        }

        public void Draw(SpriteBatch b, IList<Item> items)
        {
            if (items == null || items.Count <= this.Menu.capacity)
                return;

            if (!this.CustomArrowLayout && !this.HasResolvedCustomLayout)
            {
                this.LayoutArrows();
            }

            float upAlpha = this.CanScroll(items, -1) ? 1f : 0.35f;
            float downAlpha = this.CanScroll(items, 1) ? 1f : 0.35f;

            this.SyncAuxScrollBarThumb(items);
            this.UpArrow.draw(b, Color.White * upAlpha, 0.9f);
            this.DownArrow.draw(b, Color.White * downAlpha, 0.9f);
            if (this.ShowAuxScrollBar && this.ScrollBarRunner.Height > 0)
            {
                IClickableMenu.drawTextureBox(
                    b,
                    Game1.mouseCursors,
                    new Rectangle(403, 383, 6, 6),
                    this.ScrollBarRunner.X,
                    this.ScrollBarRunner.Y,
                    this.ScrollBarRunner.Width,
                    this.ScrollBarRunner.Height,
                    Color.White,
                    4f
                );
                this.ScrollBarThumb.draw(b);
            }
        }

        public void PerformHoverAction(int x, int y, IList<Item> items)
        {
            if (items == null || items.Count <= this.Menu.capacity)
                return;

            bool canScrollUp = this.CanScroll(items, -1);
            bool canScrollDown = this.CanScroll(items, 1);

            this.UpArrow.scale = this.UpArrow.containsPoint(x, y) && canScrollUp ? 4.1f : 4f;
            this.DownArrow.scale = this.DownArrow.containsPoint(x, y) && canScrollDown ? 4.1f : 4f;
            this.ScrollBarThumb.scale =
                this.ShowAuxScrollBar && this.ScrollBarThumb.containsPoint(x, y) ? 4.1f : 4f;
        }

        public bool ReceiveLeftClick(int x, int y, IList<Item> items)
        {
            if (items == null || items.Count <= this.Menu.capacity)
                return false;

            if (this.UpArrow.containsPoint(x, y))
            {
                if (this.CanScroll(items, -1))
                {
                    this.Scroll(items, -1);
                }
                return true;
            }
            if (this.DownArrow.containsPoint(x, y))
            {
                if (this.CanScroll(items, 1))
                {
                    this.Scroll(items, 1);
                }
                return true;
            }
            if (this.ShowAuxScrollBar)
            {
                if (this.ScrollBarThumb.containsPoint(x, y))
                {
                    this.IsDraggingAuxScrollBar = true;
                    this.AuxScrollDragOffset = y - this.ScrollBarThumb.bounds.Y;
                    return true;
                }
                if (this.ScrollBarRunner.Contains(x, y))
                {
                    this.IsDraggingAuxScrollBar = true;
                    this.AuxScrollDragOffset = this.ScrollBarThumb.bounds.Height / 2;
                    this.UpdateAuxScrollFromMouse(y, items);
                    return true;
                }
            }
            return false;
        }

        public bool ReceiveScrollWheelAction(int direction, IList<Item> items)
        {
            if (items == null || items.Count <= this.Menu.capacity)
                return false;

            int delta =
                direction > 0 ? -1
                : direction < 0 ? 1
                : 0;
            if (delta != 0 && this.CanScroll(items, delta))
            {
                this.Scroll(items, delta);
                return true;
            }
            return false;
        }

        public void UpdateGamepad(IList<Item> items)
        {
            if (items == null || items.Count <= this.Menu.capacity)
                return;

            GamePadState gamePadState = GamePad.GetState(PlayerIndex.One);
            float thumbY = gamePadState.ThumbSticks.Right.Y;
            int currentDirection =
                thumbY >= 0.5f ? -1
                : thumbY <= -0.5f ? 1
                : 0;

            if (currentDirection != 0 && currentDirection != this.LastRightStickDirection)
            {
                if (this.CanScroll(items, currentDirection))
                {
                    this.Scroll(items, currentDirection);
                }
            }
            this.LastRightStickDirection = currentDirection;
        }

        public void UpdatePointerInteraction(IList<Item> items)
        {
            if (items == null || items.Count <= this.Menu.capacity)
                return;
            if (!this.IsDraggingAuxScrollBar)
                return;

            if (Mouse.GetState().LeftButton == ButtonState.Pressed)
            {
                this.UpdateAuxScrollFromMouse(Game1.getMouseY(), items);
            }
            else
            {
                this.IsDraggingAuxScrollBar = false;
            }
        }

        private void SyncAuxScrollBarThumb(IList<Item> items)
        {
            if (!this.ShowAuxScrollBar || this.ScrollBarRunner.Height <= 0)
                return;

            int maxScroll = this.GetMaxScrollRow(items);
            int minY = this.ScrollBarRunner.Y;
            int maxY = Math.Max(minY, this.ScrollBarRunner.Bottom - this.ScrollBarThumb.bounds.Height);
            int targetY;

            if (maxScroll <= 0)
            {
                targetY = minY;
            }
            else
            {
                float progress = this.ScrollRow / (float)maxScroll;
                targetY = minY + (int)Math.Round((maxY - minY) * progress);
            }

            this.ScrollBarThumb.bounds = new Rectangle(
                this.ScrollBarRunner.X,
                targetY,
                this.ScrollBarThumb.bounds.Width,
                this.ScrollBarThumb.bounds.Height
            );
        }

        private void UpdateAuxScrollFromMouse(int mouseY, IList<Item> items)
        {
            if (!this.ShowAuxScrollBar || this.ScrollBarRunner.Height <= 0)
                return;

            int maxScroll = this.GetMaxScrollRow(items);
            int minY = this.ScrollBarRunner.Y;
            int maxY = Math.Max(minY, this.ScrollBarRunner.Bottom - this.ScrollBarThumb.bounds.Height);
            int thumbY = Math.Clamp(mouseY - this.AuxScrollDragOffset, minY, maxY);
            int previousY = this.ScrollBarThumb.bounds.Y;

            this.ScrollBarThumb.bounds = new Rectangle(
                this.ScrollBarThumb.bounds.X,
                thumbY,
                this.ScrollBarThumb.bounds.Width,
                this.ScrollBarThumb.bounds.Height
            );

            if (maxScroll <= 0)
            {
                this.ScrollRow = 0;
                return;
            }

            float progress = maxY == minY ? 0f : (thumbY - minY) / (float)(maxY - minY);
            this.ScrollRow = Math.Clamp((int)Math.Round(progress * maxScroll), 0, maxScroll);
            if (previousY != thumbY)
            {
                Game1.playSound("shiny4");
            }
        }
    }
}
