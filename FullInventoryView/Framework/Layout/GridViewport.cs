using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout
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
            public ClickableComponent? SpecialButton { get; init; }
            public ClickableComponent? OkButton { get; init; }
            public ClickableComponent? TrashButton { get; init; }
            public ClickableTextureComponent? PickerToggle { get; init; }
            public DiscreteColorPicker? ColorPicker { get; init; }
            public bool ShowScrollButtons { get; init; } = true;
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
                columns = InventoryGridMetrics.DefaultColumnCount;

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
            this.SetScrollButtonsClickable(menu, true);
        }

        public void SetScrollButtonsClickable(IClickableMenu menu, bool enabled)
        {
            menu.allClickableComponents ??= new List<ClickableComponent>();
            if (enabled)
            {
                if (!menu.allClickableComponents.Contains(this.UpArrow))
                    menu.allClickableComponents.Add(this.UpArrow);
                if (!menu.allClickableComponents.Contains(this.DownArrow))
                    menu.allClickableComponents.Add(this.DownArrow);
            }
            else
            {
                menu.allClickableComponents.Remove(this.UpArrow);
                menu.allClickableComponents.Remove(this.DownArrow);
            }
        }

        public void LayoutInventoryPageButtons(
            InventoryPage page,
            List<ClickableComponent> slots,
            ClickableTextureComponent? organizeButton,
            bool showScrollButtons
        )
        {
            this.HasResolvedCustomLayout = true;
            if (slots.Count == 0)
                return;

            ClickableComponent firstSlot = slots[0];
            int columns = this.Menu.capacity / this.Menu.rows;
            if (columns <= 0)
                columns = InventoryGridMetrics.DefaultColumnCount;

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

            int slotsRightEdge = slots.Max(s => s.bounds.Right);
            int sideMinX = slotsRightEdge - 16;
            int sideMaxX = slotsRightEdge + 360;
            int sideMinY = firstSlot.bounds.Y - 16;
            int sideMaxY = lastRowAnchor.bounds.Bottom + 16;
            var sideButtons = new List<ClickableComponent>();

            if (page.allClickableComponents != null)
            {
                foreach (var c in page.allClickableComponents)
                {
                    if (c == null)
                        continue;
                    if (slots.Contains(c))
                        continue;
                    if (c == page.trashCan || c.name == "trashCan")
                        continue;
                    if (c == page.portrait || c.name == "charPortrait")
                        continue;
                    if (c == page.upperRightCloseButton || c.name == "upperRightCloseButton")
                        continue;
                    if (c == this.UpArrow || c.name == "Scroll Up")
                        continue;
                    if (c == this.DownArrow || c.name == "Scroll Down")
                        continue;

                    bool isInsideVerticalBand =
                        c.bounds.Center.Y >= sideMinY && c.bounds.Center.Y <= sideMaxY;
                    if (!isInsideVerticalBand)
                        continue;

                    bool isPrimaryColumn = organizeButton != null
                        ? c.bounds.Right >= organizeButton.bounds.Left
                            && c.bounds.Left <= organizeButton.bounds.Right
                        : false;

                    // InventoryPage can have mod buttons in a second side column. Keep the scope
                    // limited to buttons already placed at the right side of the item grid.
                    bool isSideColumn = c.bounds.Center.X > sideMinX && c.bounds.Center.X < sideMaxX;

                    if (isPrimaryColumn || isSideColumn)
                        sideButtons.Add(c);
                }
            }

            if (organizeButton != null && !sideButtons.Contains(organizeButton))
                sideButtons.Add(organizeButton);

            sideButtons = sideButtons.Distinct().ToList();
            if (sideButtons.Count == 0)
                return;

            foreach (var b in sideButtons)
            {
                if (!this.OriginalButtonBounds.ContainsKey(b))
                    this.OriginalButtonBounds[b] = b.bounds;
                if (!this.OriginalButtonY.ContainsKey(b))
                    this.OriginalButtonY[b] = b.bounds.Y;
            }

            var sideColumns = GroupButtonsByOriginalColumn(sideButtons);
            if (sideColumns.Count == 0)
                return;

            int startY = showScrollButtons
                ? this.UpArrow.bounds.Bottom + 16
                : firstSlot.bounds.Top + 8;
            int endY = showScrollButtons
                ? this.DownArrow.bounds.Top - 16
                : lastRowAnchor.bounds.Bottom - 8;
            int availableHeight = endY - startY;
            int spacing = 8;

            foreach (var column in sideColumns)
            {
                var columnButtons = column
                    .Distinct()
                    .OrderBy(b => this.OriginalButtonY.TryGetValue(b, out int y) ? y : b.bounds.Y)
                    .ToList();
                if (columnButtons.Count == 0)
                    continue;

                bool isPrimaryColumn = organizeButton != null && columnButtons.Contains(organizeButton);
                if (isPrimaryColumn)
                {
                    columnButtons.Remove(organizeButton!);
                    columnButtons.Insert(columnButtons.Count / 2, organizeButton!);
                }

                int columnCenterX = isPrimaryColumn
                    ? anchorCenterX
                    : (int)Math.Round(columnButtons.Average(b =>
                        this.OriginalButtonBounds.TryGetValue(b, out Rectangle rect)
                            ? rect.Center.X
                            : b.bounds.Center.X
                    ));

                int totalButtonsHeight = columnButtons.Sum(b => b.bounds.Height);
                int stackHeight = totalButtonsHeight + (spacing * (columnButtons.Count - 1));
                int currentY = startY + (availableHeight - stackHeight) / 2;

                foreach (var b in columnButtons)
                {
                    b.bounds.X = columnCenterX - (b.bounds.Width / 2);
                    b.bounds.Y = currentY;
                    currentY += b.bounds.Height + spacing;
                }
            }
        }

        public void LayoutSideButtons(SideButtonLayoutContext context)
        {
            this.HasResolvedCustomLayout = true;
            bool preferLeftSide = context.PreferredSide == SideLayoutPreference.Left;
            int preferredOffset = context.PreferredSideOffsetPixels;
            bool showScrollButtons = context.ShowScrollButtons;

            context.Menu.allClickableComponents ??= new List<ClickableComponent>();
            this.SetScrollButtonsClickable(context.Menu, showScrollButtons);

            var specialBtn = context.SpecialButton;
            var sideAnchorBtn =
                context.OkButton
                ?? context.TrashButton
                ?? context.OrganizeButton
                ?? context.FillButton
                ?? context.ColorButton
                ?? specialBtn;

            if (sideAnchorBtn == null)
            {
                if (!showScrollButtons)
                    return;

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

                var fallbackSlotColumn = BuildEdgeSlotColumn(
                    context.ChestSlots,
                    context.PlayerSlots,
                    context.ChestColumns,
                    context.PlayerColumns,
                    preferLeftSide
                );

                GridViewportLayoutHelpers.WireSideColumnsNavigation(
                    fallbackSlotColumn,
                    new List<ClickableComponent> { this.UpArrow, this.DownArrow },
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
                        GridViewportLayoutHelpers.SetDownNeighbor(context.ArrowAnchorComponentOverride, this.UpArrow.myID);
                        GridViewportLayoutHelpers.SetUpNeighbor(this.UpArrow, context.ArrowAnchorComponentOverride.myID);
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
                    c != colorBtn && c != fillBtn && c != okBtn && c != trashBtn && c != specialBtn
                );
            }

            int sideAnchorCenterX = sideAnchorBtn.bounds.Center.X;
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

            AddIfValidSideButton(chestTopColumn, specialBtn, context);
            AddIfValidSideButton(chestTopColumn, colorBtn, context);
            AddIfValidSideButton(chestTopColumn, fillBtn, context);
            AddIfValidSideButton(chestTopColumn, organizeBtn, context);

            if (organizeBtn == null)
            {
                organizeBtn = chestTopColumn.FirstOrDefault(c =>
                    c != colorBtn && c != fillBtn && c != okBtn && c != trashBtn && c != specialBtn
                );
            }

            foreach (var button in chestTopColumn)
            {
                if (!this.OriginalButtonBounds.ContainsKey(button))
                    this.OriginalButtonBounds[button] = button.bounds;
                if (!this.OriginalButtonY.ContainsKey(button))
                    this.OriginalButtonY[button] = button.bounds.Y;
            }

            var chestButtonColumns = GroupButtonsByOriginalColumn(chestTopColumn);
            foreach (var column in chestButtonColumns)
            {
                int currentChestY = context.ChestSlots[0].bounds.Y;
                foreach (var button in column.OrderBy(b => this.OriginalButtonY.TryGetValue(b, out int y) ? y : b.bounds.Y))
                {
                    Rectangle originalBounds = this.OriginalButtonBounds.TryGetValue(button, out Rectangle rect)
                        ? rect
                        : button.bounds;
                    int originalCenterX = originalBounds.Center.X;
                    button.bounds.X = originalCenterX - (button.bounds.Width / 2);
                    button.bounds.Y = currentChestY;
                    currentChestY += button.bounds.Height + 8;
                }
            }
            ResolveColumnHorizontalCollisions(chestButtonColumns, preferLeftSide, 16);

            int playerLastRowIndex = Math.Min(
                context.PlayerSlots.Count - 1,
                (this.Menu.rows - 1) * context.PlayerColumns
            );
            if (showScrollButtons)
            {
                this.UpArrow.bounds.X = arrowCenterX - (this.UpArrow.bounds.Width / 2);
                this.UpArrow.bounds.Y = context.PlayerSlots[0].bounds.Top;
                this.DownArrow.bounds.X = arrowCenterX - (this.DownArrow.bounds.Width / 2);
                this.DownArrow.bounds.Y =
                    context.PlayerSlots[playerLastRowIndex].bounds.Bottom
                    - this.DownArrow.bounds.Height;
            }

            if (showScrollButtons && okBtn != null)
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
            if (showScrollButtons && trashBtn != null)
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
            AddIfValidSideButton(allSideButtons, specialBtn, context);
            if (trashBtn != null)
                allSideButtons.Add(trashBtn);
            if (okBtn != null)
                allSideButtons.Add(okBtn);
            if (showScrollButtons)
            {
                allSideButtons.Add(this.UpArrow);
                allSideButtons.Add(this.DownArrow);
            }
            allSideButtons = allSideButtons.Distinct().OrderBy(c => c.bounds.Center.Y).ToList();

            foreach (var comp in allSideButtons)
            {
                if (!context.Menu.allClickableComponents.Contains(comp))
                    context.Menu.allClickableComponents.Add(comp);
            }

            var leftSlotColumn = BuildEdgeSlotColumn(
                context.ChestSlots,
                context.PlayerSlots,
                context.ChestColumns,
                context.PlayerColumns,
                true
            );
            var rightSlotColumn = BuildEdgeSlotColumn(
                context.ChestSlots,
                context.PlayerSlots,
                context.ChestColumns,
                context.PlayerColumns,
                false
            );

            var leftButtons = allSideButtons.Where(c => IsButtonOnLeftOfRelevantGrid(c, context)).ToList();
            var rightButtons = allSideButtons.Where(c => IsButtonOnRightOfRelevantGrid(c, context)).ToList();

            if (leftButtons.Count > 0)
            {
                GridViewportLayoutHelpers.WireSideColumnsNavigation(
                    leftSlotColumn,
                    leftButtons,
                    160000,
                    true
                );
            }
            if (rightButtons.Count > 0)
            {
                GridViewportLayoutHelpers.WireSideColumnsNavigation(
                    rightSlotColumn,
                    rightButtons,
                    161000,
                    false
                );
            }

            EnsureArrowAnchorBridge(context, allSideButtons);
        }

        private static void AddIfValidSideButton(
            List<ClickableComponent> list,
            ClickableComponent? button,
            SideButtonLayoutContext context
        )
        {
            if (button == null || list.Contains(button))
                return;
            if (button == context.Menu.upperRightCloseButton || button.name == "upperRightCloseButton")
                return;
            if (context.ChestSlots.Contains(button) || context.PlayerSlots.Contains(button))
                return;
            int minY = context.ChestSlots.Min(s => s.bounds.Top) - 16;
            int maxY = Math.Max(context.ChestSlots.Max(s => s.bounds.Bottom), context.PlayerSlots.Max(s => s.bounds.Bottom)) + 16;
            if (button.bounds.Center.Y < minY || button.bounds.Center.Y > maxY)
                return;
            list.Add(button);
        }

        private List<List<ClickableComponent>> GroupButtonsByOriginalColumn(List<ClickableComponent> buttons)
        {
            var columns = new List<List<ClickableComponent>>();
            foreach (var button in buttons.Distinct().OrderBy(b =>
                this.OriginalButtonBounds.TryGetValue(b, out Rectangle rect) ? rect.Center.X : b.bounds.Center.X
            ))
            {
                int centerX = this.OriginalButtonBounds.TryGetValue(button, out Rectangle original)
                    ? original.Center.X
                    : button.bounds.Center.X;
                var column = columns.FirstOrDefault(c =>
                {
                    var first = c[0];
                    int firstX = this.OriginalButtonBounds.TryGetValue(first, out Rectangle firstOriginal)
                        ? firstOriginal.Center.X
                        : first.bounds.Center.X;
                    return Math.Abs(firstX - centerX) <= 24;
                });
                if (column == null)
                {
                    column = new List<ClickableComponent>();
                    columns.Add(column);
                }
                column.Add(button);
            }

            foreach (var column in columns)
            {
                column.Sort((a, b) =>
                    (this.OriginalButtonY.TryGetValue(a, out int ay) ? ay : a.bounds.Y)
                    .CompareTo(this.OriginalButtonY.TryGetValue(b, out int by) ? by : b.bounds.Y)
                );
            }
            return columns;
        }

        private static void ResolveColumnHorizontalCollisions(
            List<List<ClickableComponent>> columns,
            bool preferLeftSide,
            int minimumGap
        )
        {
            if (columns.Count <= 1)
                return;

            var ordered = columns
                .Where(c => c.Count > 0)
                .OrderBy(c => c.Average(b => b.bounds.Center.X))
                .ToList();
            if (preferLeftSide)
                ordered.Reverse();

            Rectangle previousBounds = GetColumnBounds(ordered[0]);
            for (int i = 1; i < ordered.Count; i++)
            {
                var current = ordered[i];
                Rectangle currentBounds = GetColumnBounds(current);
                int deltaX = 0;
                if (preferLeftSide)
                {
                    int allowedRight = previousBounds.Left - minimumGap;
                    if (currentBounds.Right > allowedRight)
                        deltaX = allowedRight - currentBounds.Right;
                }
                else
                {
                    int allowedLeft = previousBounds.Right + minimumGap;
                    if (currentBounds.Left < allowedLeft)
                        deltaX = allowedLeft - currentBounds.Left;
                }

                if (deltaX != 0)
                {
                    foreach (var button in current)
                        button.bounds.X += deltaX;
                    currentBounds = GetColumnBounds(current);
                }
                previousBounds = currentBounds;
            }
        }

        private static Rectangle GetColumnBounds(List<ClickableComponent> column)
        {
            int left = column.Min(c => c.bounds.Left);
            int top = column.Min(c => c.bounds.Top);
            int right = column.Max(c => c.bounds.Right);
            int bottom = column.Max(c => c.bounds.Bottom);
            return new Rectangle(left, top, right - left, bottom - top);
        }

        private static bool IsButtonOnLeftOfRelevantGrid(ClickableComponent button, SideButtonLayoutContext context)
        {
            var bounds = GetRelevantGridBounds(button, context);
            return button.bounds.Center.X < bounds.Left;
        }

        private static bool IsButtonOnRightOfRelevantGrid(ClickableComponent button, SideButtonLayoutContext context)
        {
            var bounds = GetRelevantGridBounds(button, context);
            return button.bounds.Center.X > bounds.Right;
        }

        private static Rectangle GetRelevantGridBounds(ClickableComponent button, SideButtonLayoutContext context)
        {
            Rectangle chestBounds = GetComponentListBounds(context.ChestSlots);
            Rectangle playerBounds = GetComponentListBounds(context.PlayerSlots);
            int y = button.bounds.Center.Y;

            if (y >= chestBounds.Top - 16 && y <= chestBounds.Bottom + 16)
                return chestBounds;
            if (y >= playerBounds.Top - 16 && y <= playerBounds.Bottom + 16)
                return playerBounds;

            int chestDistance = Math.Min(Math.Abs(y - chestBounds.Top), Math.Abs(y - chestBounds.Bottom));
            int playerDistance = Math.Min(Math.Abs(y - playerBounds.Top), Math.Abs(y - playerBounds.Bottom));
            return chestDistance <= playerDistance ? chestBounds : playerBounds;
        }

        private static Rectangle GetComponentListBounds(List<ClickableComponent> components)
        {
            int left = components.Min(c => c.bounds.Left);
            int top = components.Min(c => c.bounds.Top);
            int right = components.Max(c => c.bounds.Right);
            int bottom = components.Max(c => c.bounds.Bottom);
            return new Rectangle(left, top, right - left, bottom - top);
        }

        private static List<ClickableComponent> BuildEdgeSlotColumn(
            List<ClickableComponent> chestSlots,
            List<ClickableComponent> playerSlots,
            int chestColumns,
            int playerColumns,
            bool leftEdge
        )
        {
            var slots = new List<ClickableComponent>();
            if (leftEdge)
            {
                for (int idx = 0; idx < chestSlots.Count; idx += chestColumns)
                    slots.Add(chestSlots[idx]);
                for (int idx = 0; idx < playerSlots.Count; idx += playerColumns)
                    slots.Add(playerSlots[idx]);
            }
            else
            {
                for (int idx = chestColumns - 1; idx < chestSlots.Count; idx += chestColumns)
                    slots.Add(chestSlots[idx]);
                for (int idx = playerColumns - 1; idx < playerSlots.Count; idx += playerColumns)
                    slots.Add(playerSlots[idx]);
            }
            return slots;
        }

        private void EnsureArrowAnchorBridge(
            SideButtonLayoutContext context,
            List<ClickableComponent> allSideButtons
        )
        {
            this.UpdateAuxScrollBarVisibility(context, allSideButtons);
            if (!context.ShowScrollButtons)
                return;

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
                GridViewportLayoutHelpers.SetDownNeighbor(anchor, this.UpArrow.myID);
                GridViewportLayoutHelpers.SetUpNeighbor(this.UpArrow, anchor.myID);
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
            if (!context.ShowScrollButtons)
                return;

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
                columns = InventoryGridMetrics.DefaultColumnCount;
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
