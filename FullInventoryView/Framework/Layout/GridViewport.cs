using System;
using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Diagnostics;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.ExternalButtons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Layout
{
    public class GridViewport
    {
        // ---- Core state ----
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
        public int LastInventorySourceSignature { get; set; } = int.MinValue;

        public int Depth { get; set; } = 0;
        public IList<Item>? FullInventory { get; set; }
        public IList<Item>? OriginalInventory { get; set; }
        public int? OriginalMaxItems { get; set; }
        public Dictionary<ClickableComponent, int> OriginalButtonY { get; } = new();
        public Dictionary<ClickableComponent, Rectangle> OriginalButtonBounds { get; } = new();
        private readonly Dictionary<ClickableComponent, Rectangle> OriginalSlotBounds = new();
        public int LastAppliedSlotWindowSignature { get; private set; } = int.MinValue;
        public int LastInventoryPageSideLayoutSignature { get; private set; } = int.MinValue;
        private readonly Dictionary<string, List<Rectangle>> LastInventoryPageButtonTargets = new();
        private readonly Dictionary<string, List<Rectangle>> LastSideButtonTargets = new();

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

        public static string DescribeComponent(ClickableComponent? component)
        {
            if (component == null)
                return "<null>";

            string name = string.IsNullOrWhiteSpace(component.name) ? "<no-name>" : component.name;
            Rectangle bounds = component.bounds;
            return $"id={component.myID}, name={name}, x={bounds.X}, y={bounds.Y}, w={bounds.Width}, h={bounds.Height}";
        }

        private static string DescribeButtons(IEnumerable<ClickableComponent> buttons, int max = 8)
        {
            var parts = buttons
                .Where(button => button != null && !GridViewportLayoutHelpers.IsProtectedComponent(button))
                .Take(max)
                .Select(DescribeComponent)
                .ToList();

            int total = buttons.Count(button => button != null);
            string suffix = total > max ? $", ... +{total - max}" : string.Empty;
            return $"count={total} [{string.Join(" | ", parts)}{suffix}]";
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
            public Rectangle? OkButtonOriginalBounds { get; init; }
            public Rectangle? TrashButtonOriginalBounds { get; init; }
            public ClickableTextureComponent? PickerToggle { get; init; }
            public DiscreteColorPicker? ColorPicker { get; init; }
            public bool ShowScrollButtons { get; init; } = true;
            public List<ClickableComponent> ExtraClickableComponents { get; init; } = new();
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

        private static string GetLayoutButtonKey(ClickableComponent button)
        {
            // This key is used only for remembering layout positions across menu rebuilds.
            // Do not include myID here: vanilla and other mods may reuse -500/-1/default IDs
            // or assign a different ID to the same visual button after new ItemGrabMenu(...).
            // Use visual/semantic shape instead, and let the navigation code use IDs only
            // when it actually wires neighbors.
            string textureSignature = string.Empty;
            string hoverText = string.Empty;
            if (button is ClickableTextureComponent textureButton)
            {
                Rectangle source = textureButton.sourceRect;
                textureSignature = $"{source.X},{source.Y},{source.Width},{source.Height}";
                hoverText = textureButton.hoverText ?? string.Empty;
            }

            return string.Join(
                "|",
                button.GetType().FullName ?? string.Empty,
                button.name ?? string.Empty,
                button.bounds.Width,
                button.bounds.Height,
                textureSignature,
                hoverText
            );
        }

        private static void SaveTargetBounds(
            Dictionary<string, List<Rectangle>> targetCache,
            IEnumerable<ClickableComponent?> buttons
        )
        {
            targetCache.Clear();
            foreach (var group in buttons
                .Where(button => button != null && !GridViewportLayoutHelpers.IsProtectedComponent(button))
                .Cast<ClickableComponent>()
                .Distinct()
                .GroupBy(GetLayoutButtonKey))
            {
                targetCache[group.Key] = group
                    .OrderBy(button => button.bounds.Y)
                    .ThenBy(button => button.bounds.X)
                    .Select(button => button.bounds)
                    .ToList();
            }
        }

        private static bool ApplyTargetBounds(
            Dictionary<string, List<Rectangle>> targetCache,
            IEnumerable<ClickableComponent?> buttons,
            Func<ClickableComponent, bool>? canApply = null,
            string reason = "manual"
        )
        {
            if (targetCache.Count == 0)
                return false;

            bool matchedAny = false;
            foreach (var group in buttons
                .Where(button => button != null && !GridViewportLayoutHelpers.IsProtectedComponent(button))
                .Cast<ClickableComponent>()
                .Distinct()
                .GroupBy(GetLayoutButtonKey))
            {
                if (!targetCache.TryGetValue(group.Key, out var targets) || targets.Count == 0)
                    continue;

                var orderedButtons = group
                    .Where(button => canApply?.Invoke(button) != false)
                    .OrderBy(button => button.bounds.Y)
                    .ThenBy(button => button.bounds.X)
                    .ToList();
                if (orderedButtons.Count == 0)
                    continue;

                matchedAny = true;
                var orderedTargets = targets
                    .OrderBy(bounds => bounds.Y)
                    .ThenBy(bounds => bounds.X)
                    .ToList();

                int count = Math.Min(orderedButtons.Count, orderedTargets.Count);
                for (int i = 0; i < count; i++)
                {
                    var button = orderedButtons[i];
                    var target = orderedTargets[i];
                    if (button.bounds.X == target.X && button.bounds.Y == target.Y)
                        continue;

                    Log.Debug(
                        $"[FIV/SideLayoutCache] move cached target: reason={reason}, key={GetLayoutButtonKey(button)}, from={DescribeComponent(button)}, target={{X:{target.X} Y:{target.Y} Width:{target.Width} Height:{target.Height}}}"
                    );
                    GridViewportLayoutHelpers.SetBounds(
                        button,
                        new Rectangle(
                            target.X,
                            target.Y,
                            button.bounds.Width,
                            button.bounds.Height
                        )
                    );
                }
            }

            return matchedAny;
        }

        private int ComputeInventoryPageSideLayoutSignature(
            List<ClickableComponent> sideButtons,
            List<ClickableComponent> slots,
            bool showScrollButtons,
            int columns,
            ClickableComponent firstSlot,
            ClickableComponent lastRowAnchor
        )
        {
            var hash = new HashCode();
            hash.Add(showScrollButtons);
            hash.Add(columns);
            hash.Add(this.Menu.capacity);
            hash.Add(this.Menu.rows);
            hash.Add(slots.Count);
            hash.Add(firstSlot.bounds.X);
            hash.Add(firstSlot.bounds.Y);
            hash.Add(lastRowAnchor.bounds.X);
            hash.Add(lastRowAnchor.bounds.Y);
            hash.Add(lastRowAnchor.bounds.Bottom);

            foreach (var button in sideButtons
                .OrderBy(button => this.OriginalButtonBounds.TryGetValue(button, out Rectangle rect) ? rect.Center.X : button.bounds.Center.X)
                .ThenBy(button => this.OriginalButtonY.TryGetValue(button, out int y) ? y : button.bounds.Y)
                .ThenBy(GetLayoutButtonKey))
            {
                hash.Add(GetLayoutButtonKey(button));
                hash.Add(button.bounds.Width);
                hash.Add(button.bounds.Height);
                int originalCenterX = this.OriginalButtonBounds.TryGetValue(button, out Rectangle rect)
                    ? rect.Center.X
                    : button.bounds.Center.X;
                hash.Add(originalCenterX / 8);
            }

            return hash.ToHashCode();
        }

        public bool HasCachedSideButtonTargets => this.LastSideButtonTargets.Count > 0;

        public Dictionary<string, List<Rectangle>> ExportSideButtonTargets()
        {
            return this.LastSideButtonTargets.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToList()
            );
        }

        public void ImportSideButtonTargets(Dictionary<string, List<Rectangle>> targets)
        {
            this.LastSideButtonTargets.Clear();
            foreach (var pair in targets)
                this.LastSideButtonTargets[pair.Key] = pair.Value.ToList();
        }

        public bool ApplyCachedSideButtonTargets(
            IClickableMenu menu,
            string reason = "manual",
            Func<ClickableComponent, bool>? canApply = null
        )
        {
            if (menu.allClickableComponents == null || menu.allClickableComponents.Count == 0)
            {
                LayoutDiagnostics.DebugChanged($"SideLayoutCache:skip:{menu.GetType().Name}:{reason}", $"[FIV/SideLayoutCache] skip apply cached targets: menu={menu.GetType().Name}, reason={reason}, no clickable components, targets={this.LastSideButtonTargets.Count}");
                return false;
            }

            bool applied = ApplyTargetBounds(this.LastSideButtonTargets, menu.allClickableComponents, canApply, reason);
            if (applied)
                this.HasResolvedCustomLayout = true;
            LayoutDiagnostics.DebugChanged($"SideLayoutCache:apply:{menu.GetType().Name}:{reason}", $"[FIV/SideLayoutCache] apply cached targets: menu={menu.GetType().Name}, reason={reason}, components={menu.allClickableComponents.Count}, targetKeys={this.LastSideButtonTargets.Count}, applied={applied}");
            return applied;
        }

        public bool HasUncachedBottomSideButtonCandidates(SideButtonLayoutContext context)
        {
            var candidates = this.FindBottomSideButtons(context);
            bool hasUncached = candidates
                .GroupBy(GetLayoutButtonKey)
                .Any(group =>
                    !this.LastSideButtonTargets.TryGetValue(group.Key, out var targets)
                    || targets.Count < group.Count()
                );

            // This one is intentionally not DebugChanged: it is an investigation probe for
            // late buttons added by other mods after ItemGrabMenu construction/MenuChanged.
            var near = candidates.Count == 0 ? this.FindBottomSideProbeComponents(context) : new List<ClickableComponent>();
            string nearText = near.Count == 0 ? string.Empty : $", near={DescribeButtons(near, 12)}";
            Log.Debug($"[FIV/ChestSideLayout] bottom probe: menu={context.Menu.GetType().Name}#{context.Menu.GetHashCode()}, components={context.Menu.allClickableComponents?.Count ?? 0}, extra={context.ExtraClickableComponents.Count}, candidates={DescribeButtons(candidates)}, cachedKeys={this.LastSideButtonTargets.Count}, uncached={hasUncached}{nearText}");
            return hasUncached;
        }


    

        // ---- InventoryPage side layout ----
        public void LayoutInventoryPageButtons(
            InventoryPage page,
            List<ClickableComponent> slots,
            ClickableTextureComponent? organizeButton,
            bool showScrollButtons
        )
        {
            this.HasResolvedCustomLayout = true;
            if (slots.Count == 0)
            {
                LayoutDiagnostics.DebugChanged("InventoryPageSideLayout:skip:no-slots", "[FIV/InventoryPageSideLayout] skip: no slots");
                return;
            }

            LayoutDiagnostics.DebugChanged("InventoryPageSideLayout:start", $"[FIV/InventoryPageSideLayout] start: rows={this.Menu.rows}, capacity={this.Menu.capacity}, slots={slots.Count}, scrollRow={this.ScrollRow}, showScroll={showScrollButtons}, organize={DescribeComponent(organizeButton)}");

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
                    if (c == null || GridViewportLayoutHelpers.IsProtectedComponent(c))
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
            LayoutDiagnostics.DebugChanged("InventoryPageSideLayout:candidates", $"[FIV/InventoryPageSideLayout] candidates: {DescribeButtons(sideButtons)}, firstSlot={DescribeComponent(firstSlot)}, lastRowAnchor={DescribeComponent(lastRowAnchor)}, sideBandX={sideMinX}..{sideMaxX}, sideBandY={sideMinY}..{sideMaxY}");
            if (sideButtons.Count == 0)
            {
                LayoutDiagnostics.DebugChanged("InventoryPageSideLayout:skip:no-candidates", "[FIV/InventoryPageSideLayout] skip: no side button candidates");
                return;
            }

            foreach (var b in sideButtons.Where(button => !GridViewportLayoutHelpers.IsProtectedComponent(button)))
            {
                if (!this.OriginalButtonBounds.ContainsKey(b))
                    this.OriginalButtonBounds[b] = b.bounds;
                if (!this.OriginalButtonY.ContainsKey(b))
                    this.OriginalButtonY[b] = b.bounds.Y;
            }

            int layoutSignature = this.ComputeInventoryPageSideLayoutSignature(
                sideButtons,
                slots,
                showScrollButtons,
                columns,
                firstSlot,
                lastRowAnchor
            );
            if (layoutSignature == this.LastInventoryPageSideLayoutSignature)
            {
                bool appliedCachedTargets = ApplyTargetBounds(this.LastInventoryPageButtonTargets, sideButtons, button => !GridViewportLayoutHelpers.IsProtectedComponent(button));
                LayoutDiagnostics.DebugChanged("InventoryPageSideLayout:signature-hit", $"[FIV/InventoryPageSideLayout] signature hit: signature={layoutSignature}, appliedCachedTargets={appliedCachedTargets}, buttons={sideButtons.Count}");
                if (appliedCachedTargets)
                    return;
            }
            else
            {
                Log.Debug($"[FIV/InventoryPageSideLayout] signature miss: old={this.LastInventoryPageSideLayoutSignature}, new={layoutSignature}, buttons={sideButtons.Count}");
            }

            var sideColumns = GroupButtonsByOriginalColumn(sideButtons);
            if (sideColumns.Count == 0)
            {
                LayoutDiagnostics.DebugChanged("InventoryPageSideLayout:skip:no-columns", "[FIV/InventoryPageSideLayout] skip: grouping produced zero columns");
                return;
            }

            LayoutDiagnostics.DebugChanged("InventoryPageSideLayout:columns", $"[FIV/InventoryPageSideLayout] grouped columns={sideColumns.Count}: {string.Join("; ", sideColumns.Select((column, index) => $"col{index}={DescribeButtons(column, 5)}"))}");

            int startY = showScrollButtons
                ? this.UpArrow.bounds.Bottom + 16
                : firstSlot.bounds.Top + 8;
            int endY = showScrollButtons
                ? this.DownArrow.bounds.Top - 16
                : lastRowAnchor.bounds.Bottom - 8;
            int availableHeight = endY - startY;
            int spacing = 8;

            var orderedColumns = sideColumns
                .Where(c => c.Count > 0)
                .OrderBy(c => c.Average(b =>
                    this.OriginalButtonBounds.TryGetValue(b, out Rectangle rect)
                        ? rect.Center.X
                        : b.bounds.Center.X
                ))
                .ToList();

            if (orderedColumns.Count == 0)
                return;

            List<ClickableComponent> primaryColumn = organizeButton != null
                ? orderedColumns.FirstOrDefault(c => c.Contains(organizeButton)) ?? orderedColumns[0]
                : orderedColumns[0];

            var primaryButtons = BuildInventoryPageColumnOrder(primaryColumn, organizeButton);
            if (primaryButtons.Count == 0)
            {
                LayoutDiagnostics.DebugChanged("InventoryPageSideLayout:skip:no-primary", "[FIV/InventoryPageSideLayout] skip: primary column has no buttons");
                return;
            }

            LayoutDiagnostics.DebugChanged("InventoryPageSideLayout:area", $"[FIV/InventoryPageSideLayout] layout area: startY={startY}, endY={endY}, available={availableHeight}, primary={DescribeButtons(primaryButtons)}");

            int primaryTotalHeight = primaryButtons.Sum(b => b.bounds.Height);
            int primaryStackHeight = primaryTotalHeight + (spacing * (primaryButtons.Count - 1));
            int primaryY = startY + (availableHeight - primaryStackHeight) / 2;
            var rowY = new List<int>();
            int primaryCenterX = organizeButton != null && primaryButtons.Contains(organizeButton)
                ? anchorCenterX
                : GetOriginalColumnCenterX(primaryButtons);

            foreach (var b in primaryButtons)
            {
                if (GridViewportLayoutHelpers.IsProtectedComponent(b))
                    continue;
                GridViewportLayoutHelpers.SetBoundsX(b, primaryCenterX - (b.bounds.Width / 2));
                GridViewportLayoutHelpers.SetBoundsY(b, primaryY);
                rowY.Add(primaryY);
                primaryY += b.bounds.Height + spacing;
            }

            foreach (var column in orderedColumns)
            {
                if (ReferenceEquals(column, primaryColumn))
                    continue;

                var columnButtons = BuildInventoryPageColumnOrder(column, organizeButton);
                if (columnButtons.Count == 0)
                    continue;

                int columnCenterX = GetOriginalColumnCenterX(columnButtons);

                // Treat side buttons as a real grid. Secondary columns do not get their own
                // independent vertical centering, because that makes row 1/2/3 drift out of
                // alignment. Instead they are aligned to the primary column's row Y values.
                int startRow = Math.Max(0, (rowY.Count - columnButtons.Count + 1) / 2);
                for (int i = 0; i < columnButtons.Count; i++)
                {
                    var b = columnButtons[i];
                    int targetY;
                    int rowIndex = startRow + i;
                    if (rowIndex >= 0 && rowIndex < rowY.Count)
                    {
                        targetY = rowY[rowIndex];
                    }
                    else if (rowY.Count > 0)
                    {
                        int pitch = b.bounds.Height + spacing;
                        targetY = rowY[^1] + pitch * (rowIndex - rowY.Count + 1);
                    }
                    else
                    {
                        targetY = startY;
                    }

                    GridViewportLayoutHelpers.SetBoundsX(b, columnCenterX - (b.bounds.Width / 2));
                    GridViewportLayoutHelpers.SetBoundsY(b, targetY);
                }
            }

            this.LastInventoryPageSideLayoutSignature = layoutSignature;
            SaveTargetBounds(this.LastInventoryPageButtonTargets, sideButtons);
            Log.Debug($"[FIV/InventoryPageSideLayout] saved targets: signature={layoutSignature}, targets={this.LastInventoryPageButtonTargets.Count}, buttons={DescribeButtons(sideButtons)}");
        }

        private List<ClickableComponent> BuildInventoryPageColumnOrder(
            List<ClickableComponent> column,
            ClickableTextureComponent? organizeButton
        )
        {
            var buttons = column
                .Distinct()
                .OrderBy(b => this.OriginalButtonY.TryGetValue(b, out int y) ? y : b.bounds.Y)
                .ToList();

            if (organizeButton != null && buttons.Contains(organizeButton))
            {
                buttons.Remove(organizeButton);
                buttons.Insert(buttons.Count / 2, organizeButton);
            }

            return buttons;
        }

        private int GetOriginalColumnCenterX(List<ClickableComponent> buttons)
        {
            return (int)Math.Round(buttons.Average(b =>
                this.OriginalButtonBounds.TryGetValue(b, out Rectangle rect)
                    ? rect.Center.X
                    : b.bounds.Center.X
            ));
        }

    

        // ---- Chest/shop side-button layout ----
        public void LayoutSideButtons(SideButtonLayoutContext context)
        {
            this.HasResolvedCustomLayout = true;
            bool preferLeftSide = context.PreferredSide == SideLayoutPreference.Left;
            int preferredOffset = context.PreferredSideOffsetPixels;
            bool showScrollButtons = context.ShowScrollButtons;

            LayoutDiagnostics.DebugChanged($"ChestSideLayout:start:{context.Menu.GetType().Name}", $"[FIV/ChestSideLayout] start: menu={context.Menu.GetType().Name}, preferLeft={preferLeftSide}, offset={preferredOffset}, showScroll={showScrollButtons}, chestSlots={context.ChestSlots.Count}, playerSlots={context.PlayerSlots.Count}, chestColumns={context.ChestColumns}, playerColumns={context.PlayerColumns}, color={DescribeComponent(context.ColorButton)}, fill={DescribeComponent(context.FillButton)}, organize={DescribeComponent(context.OrganizeButton)}, special={DescribeComponent(context.SpecialButton)}, ok={DescribeComponent(context.OkButton)}, trash={DescribeComponent(context.TrashButton)}");

            context.Menu.allClickableComponents ??= new List<ClickableComponent>();
            this.SetScrollButtonsClickable(context.Menu, showScrollButtons);

            var specialBtn = context.SpecialButton;

            // Upper chest actions and lower player-inventory actions can live in
            // different original columns. This happens in vanilla large chest layouts:
            // the chest grid can be wider than the player inventory, so organize/fill/color
            // should stay anchored to the chest side while OK/trash stay anchored to the
            // player inventory side. Never use the organize column as the lower button anchor.
            var topAnchorBtn =
                context.OrganizeButton
                ?? context.FillButton
                ?? context.ColorButton
                ?? specialBtn
                ?? context.OkButton
                ?? context.TrashButton;

            if (topAnchorBtn == null)
            {
                if (!showScrollButtons)
                {
                    LayoutDiagnostics.DebugChanged("ChestSideLayout:skip:no-anchor", "[FIV/ChestSideLayout] skip: no side anchor and no scroll buttons");
                    return;
                }

                LayoutDiagnostics.DebugChanged("ChestSideLayout:fallback:arrows-only", "[FIV/ChestSideLayout] fallback: no side anchor, laying out arrows only");

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

                NavigationGraphBuilder.WireSideColumns(
                    fallbackSlotColumn,
                    new List<ClickableComponent> { this.UpArrow, this.DownArrow },
                    160000,
                    preferLeftSide
                );

                var fallbackButtons = new List<ClickableComponent> { this.UpArrow, this.DownArrow };
                this.UpdateAuxScrollBarVisibility(context, fallbackButtons);
                SaveTargetBounds(this.LastSideButtonTargets, fallbackButtons);
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
                topAnchorBtn,
                24,
                minY,
                maxY,
                context.PickerToggle,
                this.UpArrow,
                this.DownArrow,
                context.ChestSlots,
                context.PlayerSlots
            );
            alignedButtons.RemoveAll(button => ExternalSideButtonRegistry.IsLayoutDisabled(context.Menu, button));

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

            RememberOriginalButtonBounds(specialBtn, colorBtn, fillBtn, organizeBtn, okBtn, trashBtn);

            int lowerSideCenterX = GetLowerSideColumnCenterX(context, preferLeftSide, preferredOffset, okBtn, trashBtn);
            int arrowCenterX = lowerSideCenterX;
            if (context.ArrowAnchorComponentOverride != null)
            {
                arrowCenterX = context.ArrowAnchorComponentOverride.bounds.Center.X;
            }
            if (context.ArrowAnchorCenterXOverride.HasValue)
            {
                arrowCenterX = context.ArrowAnchorCenterXOverride.Value;
            }

            // External overlay buttons (especially Chests Anywhere's SortInventoryButton)
            // are recreated by the owning mod when the color picker/tabs rebuild the menu.
            // FIV previously added the old instance to allClickableComponents for gamepad
            // navigation, then kept seeing both the old and new instances on later layout
            // passes. That made the button walk right one column per rebuild. Clean stale
            // external siblings before collecting either top or bottom side buttons.
            PruneStaleExternalSideButtons(context);

            var bottomSideButtons = this.FindBottomSideButtons(context);
            var chestTopColumn = GridViewportLayoutHelpers.FindChestColumnButtons(
                context.Menu,
                context.ChestSlots,
                context.PlayerSlots,
                context.PickerToggle,
                this.UpArrow,
                this.DownArrow,
                okBtn,
                trashBtn
            )
                .Where(button => !ExternalSideButtonRegistry.IsLayoutDisabled(context.Menu, button))
                .Where(button => !bottomSideButtons.Contains(button))
                .Where(button => !IsExternalButtonOrStaleSibling(button, context))
                .Distinct()
                .ToList();

            AddIfValidSideButton(chestTopColumn, specialBtn, context);
            AddIfValidSideButton(chestTopColumn, colorBtn, context);
            AddIfValidSideButton(chestTopColumn, fillBtn, context);
            AddIfValidSideButton(chestTopColumn, organizeBtn, context);
            chestTopColumn.RemoveAll(button => bottomSideButtons.Contains(button));

            LayoutDiagnostics.DebugChanged("ChestSideLayout:top-candidates", $"[FIV/ChestSideLayout] chest top candidates: {DescribeButtons(chestTopColumn)}");
            LayoutDiagnostics.DebugChanged("ChestSideLayout:bottom-candidates", $"[FIV/ChestSideLayout] bottom candidates: {DescribeButtons(bottomSideButtons)}");

            if (organizeBtn == null)
            {
                organizeBtn = chestTopColumn.FirstOrDefault(c =>
                    c != colorBtn && c != fillBtn && c != okBtn && c != trashBtn && c != specialBtn
                );
            }

            foreach (var button in chestTopColumn.Where(button => !GridViewportLayoutHelpers.IsProtectedComponent(button)))
            {
                if (!this.OriginalButtonBounds.ContainsKey(button))
                    this.OriginalButtonBounds[button] = button.bounds;
                if (!this.OriginalButtonY.ContainsKey(button))
                    this.OriginalButtonY[button] = button.bounds.Y;
            }

            var chestButtonColumns = GroupButtonsByOriginalColumn(chestTopColumn);
            LayoutDiagnostics.DebugChanged("ChestSideLayout:columns", $"[FIV/ChestSideLayout] chest columns={chestButtonColumns.Count}: {string.Join("; ", chestButtonColumns.Select((column, index) => $"col{index}={DescribeButtons(column, 5)}"))}");
            foreach (var column in chestButtonColumns)
            {
                int currentChestY = context.ChestSlots[0].bounds.Y;
                foreach (var button in column.OrderBy(b => this.OriginalButtonY.TryGetValue(b, out int y) ? y : b.bounds.Y))
                {
                    Rectangle originalBounds = this.OriginalButtonBounds.TryGetValue(button, out Rectangle rect)
                        ? rect
                        : button.bounds;
                    int originalCenterX = originalBounds.Center.X;
                    GridViewportLayoutHelpers.SetBoundsX(button, originalCenterX - (button.bounds.Width / 2));
                    GridViewportLayoutHelpers.SetBoundsY(button, currentChestY);
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

            LayoutLowerVanillaButtons(
                context,
                lowerSideCenterX,
                okBtn,
                trashBtn,
                showScrollButtons,
                playerLastRowIndex
            );

            LayoutBottomSideButtons(
                bottomSideButtons,
                lowerSideCenterX,
                trashBtn,
                okBtn,
                showScrollButtons ? this.DownArrow : null,
                context,
                preferLeftSide
            );

            if (context.ColorPicker != null && context.PickerToggle != null && colorBtn != null)
            {
                GridViewportLayoutHelpers.SetBounds(context.PickerToggle, colorBtn.bounds);
            }

            var fixedPositionExternalButtons = GetFixedPositionExternalSideButtons(context);
            int playerTopY = context.PlayerSlots.Min(slot => slot.bounds.Top);

            var topSideButtons = new List<ClickableComponent>();
            topSideButtons.AddRange(chestTopColumn);
            topSideButtons.AddRange(fixedPositionExternalButtons.Where(button => button.bounds.Center.Y < playerTopY - 48));
            AddIfValidSideButton(topSideButtons, specialBtn, context);
            topSideButtons = topSideButtons.Distinct().OrderBy(c => c.bounds.Center.Y).ToList();

            var lowerSideButtons = new List<ClickableComponent>();
            lowerSideButtons.AddRange(bottomSideButtons);
            lowerSideButtons.AddRange(fixedPositionExternalButtons.Where(button => button.bounds.Center.Y >= playerTopY - 48));
            if (trashBtn != null)
                lowerSideButtons.Add(trashBtn);
            if (okBtn != null)
                lowerSideButtons.Add(okBtn);
            if (showScrollButtons)
            {
                lowerSideButtons.Add(this.UpArrow);
                lowerSideButtons.Add(this.DownArrow);
            }
            lowerSideButtons = lowerSideButtons.Distinct().OrderBy(c => c.bounds.Center.Y).ToList();

            var allSideButtons = new List<ClickableComponent>();
            allSideButtons.AddRange(topSideButtons);
            allSideButtons.AddRange(lowerSideButtons);
            allSideButtons = allSideButtons.Distinct().OrderBy(c => c.bounds.Center.Y).ToList();

            foreach (var comp in allSideButtons.Where(component => !GridViewportLayoutHelpers.IsProtectedComponent(component)))
            {
                if (!context.Menu.allClickableComponents.Contains(comp))
                    context.Menu.allClickableComponents.Add(comp);
            }

            var leftChestSlotColumn = BuildSingleEdgeSlotColumn(context.ChestSlots, context.ChestColumns, true);
            var rightChestSlotColumn = BuildSingleEdgeSlotColumn(context.ChestSlots, context.ChestColumns, false);
            var leftPlayerSlotColumn = BuildSingleEdgeSlotColumn(context.PlayerSlots, context.PlayerColumns, true);
            var rightPlayerSlotColumn = BuildSingleEdgeSlotColumn(context.PlayerSlots, context.PlayerColumns, false);

            var leftTopButtons = topSideButtons.Where(c => IsButtonOnLeftOfRelevantGrid(c, context)).ToList();
            var rightTopButtons = topSideButtons.Where(c => IsButtonOnRightOfRelevantGrid(c, context)).ToList();
            var leftLowerButtons = lowerSideButtons.Where(c => IsButtonOnLeftOfRelevantGrid(c, context)).ToList();
            var rightLowerButtons = lowerSideButtons.Where(c => IsButtonOnRightOfRelevantGrid(c, context)).ToList();

            if (leftTopButtons.Count > 0)
                NavigationGraphBuilder.WireSideColumns(leftChestSlotColumn, leftTopButtons, 160000, true);
            if (rightTopButtons.Count > 0)
                NavigationGraphBuilder.WireSideColumns(rightChestSlotColumn, rightTopButtons, 161000, false);
            if (leftLowerButtons.Count > 0)
                NavigationGraphBuilder.WireSideColumns(leftPlayerSlotColumn, leftLowerButtons, 162000, true);
            if (rightLowerButtons.Count > 0)
                NavigationGraphBuilder.WireSideColumns(rightPlayerSlotColumn, rightLowerButtons, 163000, false);

            EnsureChestsAnywhereSortBesideOkNavigation(context, lowerSideButtons, okBtn);

            PreventChestSlotsFromJumpingToLowerSideButtons(context, lowerSideButtons);
            LogSideNavigationSnapshot(context, topSideButtons, lowerSideButtons);

            EnsureArrowAnchorBridge(context, topSideButtons, lowerSideButtons, allSideButtons);

            // ItemGrabMenu side layout is now recomputed from live vanilla/mod fields every pass.
            // Do NOT cache/replay side-button targets here. Generic ClickableComponent buttons
            // often share the same anonymous key, and replaying those targets was moving OK,
            // trash, scroll arrows, and stale external buttons into the organize column after
            // color-picker/Chests Anywhere rebuilds.
            this.LastSideButtonTargets.Clear();
            LayoutDiagnostics.DebugChanged($"ChestSideLayout:saved:{context.Menu.GetType().Name}", $"[FIV/ChestSideLayout] saved targets: allButtons={DescribeButtons(allSideButtons)}, topLeft={leftTopButtons.Count}, topRight={rightTopButtons.Count}, lowerLeft={leftLowerButtons.Count}, lowerRight={rightLowerButtons.Count}, targetKeys={this.LastSideButtonTargets.Count}, cache=disabled-for-itemgrab");
        }

        private static void PruneStaleExternalSideButtons(SideButtonLayoutContext context)
        {
            if (context.Menu.allClickableComponents == null)
                return;

            var liveExternalButtons = context.ExtraClickableComponents
                .Where(button => button != null && !GridViewportLayoutHelpers.IsProtectedComponent(button))
                .Distinct()
                .ToList();

            int before = context.Menu.allClickableComponents.Count;
            bool hasLiveExternalButton = liveExternalButtons.Count > 0;
            context.Menu.allClickableComponents.RemoveAll(component =>
                component != null
                && !GridViewportLayoutHelpers.IsProtectedComponent(component)
                && !liveExternalButtons.Contains(component)
                && (
                    IsExternalButtonOrStaleSibling(component, context)
                    || (hasLiveExternalButton && IsKnownOrphanExternalButton(component))
                )
            );

            int removed = before - context.Menu.allClickableComponents.Count;
            if (removed > 0)
            {
                Log.Debug(
                    $"[FIV/ExternalButton] pruned stale side buttons: removed={removed}, live={DescribeButtons(liveExternalButtons)}, components={context.Menu.allClickableComponents.Count}"
                );
            }
        }

        private static bool IsKnownOrphanExternalButton(ClickableComponent component)
        {
            return string.Equals(component.name, "sort-inventory", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSingleChestsAnywhereSortButton(List<ClickableComponent> buttons)
        {
            return buttons.Count == 1 && IsKnownOrphanExternalButton(buttons[0]);
        }

        private static bool IsExternalButtonOrStaleSibling(
            ClickableComponent? component,
            SideButtonLayoutContext context
        )
        {
            if (component == null || context.ExtraClickableComponents.Count == 0)
                return false;

            foreach (var live in context.ExtraClickableComponents.Where(button => button != null))
            {
                // Registered lower-stack integrations like Chests Anywhere's sort button are
                // handled by FindBottomSideButtons and must not also enter the chest/top column.
                // Registered top-side integrations like RemoteFridgeStorage's fridge toggle should
                // stay discoverable here so the normal intelligent side-column layout can wire them.
                if (ReferenceEquals(component, live))
                    return IsKnownOrphanExternalButton(live);

                if (IsSameExternalButtonFamily(component, live))
                    return IsKnownOrphanExternalButton(component) || IsKnownOrphanExternalButton(live);
            }

            return false;
        }

        private static bool IsSameExternalButtonFamily(ClickableComponent candidate, ClickableComponent live)
        {
            if (candidate.bounds.Width != live.bounds.Width || candidate.bounds.Height != live.bounds.Height)
                return false;

            string candidateName = candidate.name ?? string.Empty;
            string liveName = live.name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(candidateName)
                && string.Equals(candidateName, liveName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Chests Anywhere's SortInventoryButton can keep a default/sentinel ID and a simple
            // text name. If a previous FIV layout assigned it a dynamic ID, it is still the same
            // external family and must not become a second side button.
            if (string.Equals(candidateName, "sort-inventory", StringComparison.OrdinalIgnoreCase)
                && string.Equals(liveName, "sort-inventory", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private void RememberOriginalButtonBounds(params ClickableComponent?[] buttons)
        {
            foreach (var button in buttons)
            {
                if (button == null || GridViewportLayoutHelpers.IsProtectedComponent(button))
                    continue;
                if (!this.OriginalButtonBounds.ContainsKey(button))
                    this.OriginalButtonBounds[button] = button.bounds;
                if (!this.OriginalButtonY.ContainsKey(button))
                    this.OriginalButtonY[button] = button.bounds.Y;
            }
        }

        private int GetLowerSideColumnCenterX(
            SideButtonLayoutContext context,
            bool preferLeftSide,
            int preferredOffset,
            ClickableComponent? okBtn,
            ClickableComponent? trashBtn
        )
        {
            int centerX;
            if (preferLeftSide)
            {
                Rectangle playerBounds = GetComponentListBounds(context.PlayerSlots);
                centerX = playerBounds.Left - preferredOffset - 32;
            }
            else
            {
                // Horizontal rule for normal lower ItemGrabMenu controls:
                // never derive X from organize, chest width, player-grid width, or a guessed side gap.
                // The OK button's X as it arrived for this layout pass is the anchor. FIV may adjust
                // Y to make room for scroll/buttons, but X must remain the original OK column.
                centerX = GetOriginalLowerAnchorCenterX(context, okBtn, trashBtn);
            }

            Log.Debug(
                $"[FIV/ChestLowerTrace] lower column resolved: centerX={centerX}, preferLeft={preferLeftSide}, okOriginal={(context.OkButtonOriginalBounds.HasValue ? context.OkButtonOriginalBounds.Value.ToString() : "<null>")}, trashOriginal={(context.TrashButtonOriginalBounds.HasValue ? context.TrashButtonOriginalBounds.Value.ToString() : "<null>")}, okLive={DescribeComponent(okBtn)}, trashLive={DescribeComponent(trashBtn)}, organize={DescribeComponent(context.OrganizeButton)}"
            );

            return centerX;
        }

        private static int GetOriginalLowerAnchorCenterX(
            SideButtonLayoutContext context,
            ClickableComponent? okBtn,
            ClickableComponent? trashBtn
        )
        {
            if (context.OkButtonOriginalBounds.HasValue)
                return context.OkButtonOriginalBounds.Value.Center.X;

            if (context.TrashButtonOriginalBounds.HasValue)
                return context.TrashButtonOriginalBounds.Value.Center.X;

            if (okBtn != null)
                return okBtn.bounds.Center.X;

            if (trashBtn != null)
                return trashBtn.bounds.Center.X;

            ClickableComponent? fallback = context.OrganizeButton
                ?? context.FillButton
                ?? context.ColorButton
                ?? context.SpecialButton;
            if (fallback != null)
                return fallback.bounds.Center.X;

            Rectangle playerBounds = GetComponentListBounds(context.PlayerSlots);
            return context.PreferredSide == SideLayoutPreference.Left
                ? playerBounds.Left - context.PreferredSideOffsetPixels - 32
                : playerBounds.Right + context.PreferredSideOffsetPixels + 32;
        }

        private void LayoutLowerVanillaButtons(
            SideButtonLayoutContext context,
            int lowerSideCenterX,
            ClickableComponent? okBtn,
            ClickableComponent? trashBtn,
            bool showScrollButtons,
            int playerLastRowIndex
        )
        {
            const int spacing = 8;

            Rectangle? originalOk = context.OkButtonOriginalBounds;
            Rectangle? originalTrash = context.TrashButtonOriginalBounds;

            ClickableComponent rowAnchor = context.PlayerSlots[Math.Min(context.PlayerSlots.Count - 1, playerLastRowIndex)];

            if (IsCompactLeftStorageLayout(context, showScrollButtons))
            {
                LayoutCompactLeftStorageLowerVanillaButtons(
                    context,
                    lowerSideCenterX,
                    okBtn,
                    trashBtn,
                    rowAnchor,
                    originalOk,
                    originalTrash
                );
                return;
            }

            if (okBtn != null)
            {
                // Horizontal rule: preserve the OK column captured at the start of this layout pass.
                // Never calculate X from organize, chest width, or player-grid width.
                // Vertical rule: without scroll arrows, OK is aligned with the last visible
                // player-inventory row. With scroll arrows, OK sits above the down arrow.
                GridViewportLayoutHelpers.SetBoundsX(okBtn, lowerSideCenterX - (okBtn.bounds.Width / 2));

                if (showScrollButtons)
                {
                    if (context.CenterOkBetweenArrows)
                    {
                        int availableHeight = this.DownArrow.bounds.Top - this.UpArrow.bounds.Bottom;
                        GridViewportLayoutHelpers.SetBoundsY(okBtn, this.UpArrow.bounds.Bottom + ((availableHeight - okBtn.bounds.Height) / 2));
                    }
                    else
                    {
                        GridViewportLayoutHelpers.SetBoundsY(okBtn, this.DownArrow.bounds.Y - spacing - okBtn.bounds.Height);
                    }
                }
                else
                {
                    GridViewportLayoutHelpers.SetBoundsY(okBtn, rowAnchor.bounds.Center.Y - (okBtn.bounds.Height / 2));
                }
            }

            if (trashBtn != null)
            {
                // Same horizontal rule as OK: use the original OK/lower column only. Vertically,
                // the trash can occupies the two rows above OK whenever OK exists.
                GridViewportLayoutHelpers.SetBoundsX(trashBtn, lowerSideCenterX - (trashBtn.bounds.Width / 2));

                if (okBtn != null)
                {
                    GridViewportLayoutHelpers.SetBoundsY(trashBtn, okBtn.bounds.Y - spacing - trashBtn.bounds.Height);
                }
                else if (showScrollButtons)
                {
                    GridViewportLayoutHelpers.SetBoundsY(trashBtn, this.DownArrow.bounds.Y - spacing - trashBtn.bounds.Height);
                }
                else
                {
                    GridViewportLayoutHelpers.SetBoundsY(trashBtn, rowAnchor.bounds.Center.Y - (trashBtn.bounds.Height / 2));
                }
            }

            Log.Debug(
                $"[FIV/ChestSideLayout] lower vanilla layout: showScroll={showScrollButtons}, lowerCenter={lowerSideCenterX}, rowAnchor={DescribeComponent(rowAnchor)}, okOriginal={(originalOk.HasValue ? originalOk.Value.ToString() : "<null>")}, okFinal={DescribeComponent(okBtn)}, trashOriginal={(originalTrash.HasValue ? originalTrash.Value.ToString() : "<null>")}, trashFinal={DescribeComponent(trashBtn)}, up={DescribeComponent(this.UpArrow)}, down={DescribeComponent(this.DownArrow)}"
            );
        }

        private void LayoutCompactLeftStorageLowerVanillaButtons(
            SideButtonLayoutContext context,
            int leftColumnCenterX,
            ClickableComponent? okBtn,
            ClickableComponent? trashBtn,
            ClickableComponent rowAnchor,
            Rectangle? originalOk,
            Rectangle? originalTrash
        )
        {
            const int spacing = 8;
            Rectangle playerBounds = GetComponentListBounds(context.PlayerSlots);

            // HugeChest/Roger-style menus only have four visible player rows. The left
            // side column must keep FIV's scroll arrows plus the tall trash can. OK does
            // not fit there once Chests Anywhere's sort button is present, so OK stays on
            // the opposite/right side while the scroll/trash cluster stays left.
            if (okBtn != null)
            {
                int okCenterX = context.OkButtonOriginalBounds.HasValue
                    ? context.OkButtonOriginalBounds.Value.Center.X
                    : playerBounds.Right + context.PreferredSideOffsetPixels + 32;
                GridViewportLayoutHelpers.SetBoundsX(okBtn, okCenterX - (okBtn.bounds.Width / 2));
                GridViewportLayoutHelpers.SetBoundsY(okBtn, rowAnchor.bounds.Center.Y - (okBtn.bounds.Height / 2));
            }

            if (trashBtn != null)
            {
                int minTrashY = this.UpArrow.bounds.Bottom + spacing;
                int maxTrashY = this.DownArrow.bounds.Top - spacing - trashBtn.bounds.Height;
                int trashY = maxTrashY >= minTrashY
                    ? minTrashY + ((maxTrashY - minTrashY) / 2)
                    : Math.Max(playerBounds.Top, this.UpArrow.bounds.Bottom);

                GridViewportLayoutHelpers.SetBoundsX(trashBtn, leftColumnCenterX - (trashBtn.bounds.Width / 2));
                GridViewportLayoutHelpers.SetBoundsY(trashBtn, trashY);
            }

            Log.Debug(
                $"[FIV/ChestSideLayout] lower compact-left layout: menu={context.Menu.GetType().Name}, leftCenter={leftColumnCenterX}, rowAnchor={DescribeComponent(rowAnchor)}, okOriginal={(originalOk.HasValue ? originalOk.Value.ToString() : "<null>")}, okFinal={DescribeComponent(okBtn)}, trashOriginal={(originalTrash.HasValue ? originalTrash.Value.ToString() : "<null>")}, trashFinal={DescribeComponent(trashBtn)}, up={DescribeComponent(this.UpArrow)}, down={DescribeComponent(this.DownArrow)}"
            );
        }

        private static List<ClickableComponent> GetFixedPositionExternalSideButtons(SideButtonLayoutContext context)
        {
            return ExternalSideButtonRegistry.GetFixedPositionButtons(context.Menu)
                .Select(entry => entry.Button)
                .Where(button =>
                    button != null
                    && !GridViewportLayoutHelpers.IsProtectedComponent(button)
                    && button != context.Menu.upperRightCloseButton
                    && button.name != "upperRightCloseButton"
                    && !context.ChestSlots.Contains(button)
                    && !context.PlayerSlots.Contains(button)
                    && LooksLikeSideButton(button)
                )
                .Distinct()
                .OrderBy(button => button.bounds.Center.Y)
                .ToList();
        }

        private static void AddIfValidSideButton(
            List<ClickableComponent> list,
            ClickableComponent? button,
            SideButtonLayoutContext context
        )
        {
            if (button == null || GridViewportLayoutHelpers.IsProtectedComponent(button) || list.Contains(button))
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

        private List<ClickableComponent> FindBottomSideButtons(SideButtonLayoutContext context)
        {
            var buttons = new List<ClickableComponent>();
            if (context.Menu.allClickableComponents == null || context.Menu.allClickableComponents.Count == 0)
                return buttons;

            var playerBounds = GetComponentListBounds(context.PlayerSlots);
            bool preferLeftSide = context.PreferredSide == SideLayoutPreference.Left;
            int anchorCenterX = GetOriginalLowerAnchorCenterX(context, context.OkButton, context.TrashButton);

            int minY = playerBounds.Top - 48;
            int maxY = playerBounds.Bottom + 96;
            int minX = preferLeftSide ? playerBounds.Left - 360 : playerBounds.Right - 24;
            int maxX = preferLeftSide ? playerBounds.Left + 24 : playerBounds.Right + 360;

            var excluded = new HashSet<ClickableComponent>();
            AddExcluded(excluded, context.ColorButton);
            AddExcluded(excluded, context.FillButton);
            AddExcluded(excluded, context.OrganizeButton);
            AddExcluded(excluded, context.SpecialButton);
            AddExcluded(excluded, context.OkButton);
            AddExcluded(excluded, context.TrashButton);
            AddExcluded(excluded, context.PickerToggle);
            AddExcluded(excluded, this.UpArrow);
            AddExcluded(excluded, this.DownArrow);
            if (context.Menu.upperRightCloseButton != null)
                excluded.Add(context.Menu.upperRightCloseButton);

            var registeredExternalButtons = new HashSet<ClickableComponent>(
                context.ExtraClickableComponents.Where(button => button != null)
            );

            foreach (var component in this.GetLayoutComponents(context))
            {
                if (component == null || GridViewportLayoutHelpers.IsProtectedComponent(component))
                    continue;
                if (excluded.Contains(component))
                    continue;
                if (component == context.Menu.upperRightCloseButton || component.name == "upperRightCloseButton")
                    continue;
                if (context.ChestSlots.Contains(component) || context.PlayerSlots.Contains(component))
                    continue;
                if (component.bounds.Center.Y < minY || component.bounds.Center.Y > maxY)
                    continue;

                bool isRegisteredExternalButton = registeredExternalButtons.Contains(component);
                bool isKnownExternalButton = IsKnownOrphanExternalButton(component);
                if (!isRegisteredExternalButton && !isKnownExternalButton)
                    continue;

                bool isInSideBand = component.bounds.Center.X >= minX && component.bounds.Center.X <= maxX;
                bool isNearAnchorColumn = Math.Abs(component.bounds.Center.X - anchorCenterX) <= 96;
                if (!isInSideBand && !isNearAnchorColumn)
                    continue;

                // This group is only for lower/player-inventory side buttons. Top chest buttons
                // are handled by FindChestColumnButtons and must not be pulled into the bottom stack.
                if (component.bounds.Center.Y < playerBounds.Top - 48)
                    continue;

                // From here on, lower side buttons must be explicit integrations/API buttons.
                // Do not auto-adopt anonymous 64x64 components around the inventory: CJB/other mods
                // can put their own arrows/helpers there, and FIV moving them breaks their layout.
                if (!LooksLikeSideButton(component) && !LooksLikeAnonymousBottomSideButton(component, anchorCenterX))
                    continue;

                buttons.Add(component);
            }

            foreach (var button in buttons.Where(button => !GridViewportLayoutHelpers.IsProtectedComponent(button)))
            {
                if (!this.OriginalButtonBounds.ContainsKey(button))
                    this.OriginalButtonBounds[button] = button.bounds;
                if (!this.OriginalButtonY.ContainsKey(button))
                    this.OriginalButtonY[button] = button.bounds.Y;
            }

            return NormalizeBottomSideButtons(buttons, context)
                .OrderBy(button => this.OriginalButtonY.TryGetValue(button, out int y) ? y : button.bounds.Y)
                .ThenBy(button => this.OriginalButtonBounds.TryGetValue(button, out Rectangle rect) ? rect.Center.X : button.bounds.Center.X)
                .ToList();
        }

        private static List<ClickableComponent> NormalizeBottomSideButtons(
            IEnumerable<ClickableComponent> buttons,
            SideButtonLayoutContext context
        )
        {
            var distinct = buttons
                .Where(button => button != null && !GridViewportLayoutHelpers.IsProtectedComponent(button))
                .Distinct()
                .ToList();
            if (distinct.Count <= 1)
                return distinct;

            var registered = new HashSet<ClickableComponent>(context.ExtraClickableComponents.Where(button => button != null && !GridViewportLayoutHelpers.IsProtectedComponent(button)));
            var result = new List<ClickableComponent>();
            foreach (var group in distinct.GroupBy(GetLayoutButtonKey))
            {
                var items = group.ToList();
                var registeredItems = items.Where(registered.Contains).ToList();

                // If a known integration/API button is present, prefer the registered live
                // component and drop stale components with the same visual key that FIV added to
                // allClickableComponents in a previous layout pass. This prevents Chests Anywhere's
                // SortInventoryButton from multiplying after color-picker/reinitialize rebuilds.
                if (registeredItems.Count > 0)
                    result.AddRange(registeredItems);
                else
                    result.AddRange(items);
            }

            return result.Distinct().ToList();
        }

        private IEnumerable<ClickableComponent> GetLayoutComponents(SideButtonLayoutContext context)
        {
            var seen = new HashSet<ClickableComponent>();
            if (context.Menu.allClickableComponents != null)
            {
                foreach (var component in context.Menu.allClickableComponents)
                {
                    if (component != null
                            && !GridViewportLayoutHelpers.IsProtectedComponent(component)
                            && !ExternalSideButtonRegistry.IsLayoutDisabled(context.Menu, component)
                            && seen.Add(component))
                        yield return component;
                }
            }

            foreach (var component in context.ExtraClickableComponents)
            {
                if (component != null
                        && !GridViewportLayoutHelpers.IsProtectedComponent(component)
                        && !ExternalSideButtonRegistry.IsLayoutDisabled(context.Menu, component)
                        && seen.Add(component))
                    yield return component;
            }
        }

        private List<ClickableComponent> FindBottomSideProbeComponents(SideButtonLayoutContext context)
        {
            var playerBounds = GetComponentListBounds(context.PlayerSlots);
            bool preferLeftSide = context.PreferredSide == SideLayoutPreference.Left;
            int anchorCenterX = GetOriginalLowerAnchorCenterX(context, context.OkButton, context.TrashButton);
            int minY = playerBounds.Top - 80;
            int maxY = playerBounds.Bottom + 140;
            int minX = preferLeftSide ? playerBounds.Left - 420 : playerBounds.Right - 80;
            int maxX = preferLeftSide ? playerBounds.Left + 80 : playerBounds.Right + 420;

            var excluded = new HashSet<ClickableComponent>();
            AddExcluded(excluded, context.ColorButton);
            AddExcluded(excluded, context.FillButton);
            AddExcluded(excluded, context.OrganizeButton);
            AddExcluded(excluded, context.SpecialButton);
            AddExcluded(excluded, context.OkButton);
            AddExcluded(excluded, context.TrashButton);
            AddExcluded(excluded, context.PickerToggle);
            AddExcluded(excluded, this.UpArrow);
            AddExcluded(excluded, this.DownArrow);

            return this.GetLayoutComponents(context)
                .Where(component =>
                    component != null
                    && !GridViewportLayoutHelpers.IsProtectedComponent(component)
                    && !excluded.Contains(component)
                    && component != context.Menu.upperRightCloseButton
                    && component.name != "upperRightCloseButton"
                    && !context.ChestSlots.Contains(component)
                    && !context.PlayerSlots.Contains(component)
                    && component.bounds.Width > 0
                    && component.bounds.Height > 0
                    && component.bounds.Width <= 220
                    && component.bounds.Height <= 220
                    && component.bounds.Center.Y >= minY
                    && component.bounds.Center.Y <= maxY
                    && (
                        (component.bounds.Center.X >= minX && component.bounds.Center.X <= maxX)
                        || Math.Abs(component.bounds.Center.X - anchorCenterX) <= 128
                    )
                )
                .Distinct()
                .OrderBy(component => component.bounds.Y)
                .ThenBy(component => component.bounds.X)
                .ToList();
        }

        private static void AddExcluded(HashSet<ClickableComponent> excluded, ClickableComponent? component)
        {
            if (component != null)
                excluded.Add(component);
        }

        private static bool LooksLikeSideButton(ClickableComponent component)
        {
            if (component.bounds.Width <= 0 || component.bounds.Height <= 0)
                return false;
            if (component.bounds.Width > 144 || component.bounds.Height > 144)
                return false;
            if (component.bounds.Width < 16 || component.bounds.Height < 16)
                return false;

            if (GridViewportLayoutHelpers.IsProtectedComponent(component))
                return false;

            return true;
        }

        private static bool LooksLikeAnonymousBottomSideButton(ClickableComponent component, int anchorCenterX)
        {
            if (GridViewportLayoutHelpers.IsProtectedComponent(component))
                return false;
            if (component.bounds.Width < 32 || component.bounds.Height < 32)
                return false;
            if (component.bounds.Width > 144 || component.bounds.Height > 144)
                return false;

            // Only accept anonymous/default components in the exact side column. This catches
            // manually-drawn mod buttons without treating every -500 helper/drop target as a button.
            return Math.Abs(component.bounds.Center.X - anchorCenterX) <= 72;
        }

        private static bool IsCompactLeftStorageLayout(SideButtonLayoutContext context, bool showScrollButtons)
        {
            return showScrollButtons
                && context.PreferredSide == SideLayoutPreference.Left
                && string.Equals(context.Menu.GetType().Name, "HugeChestMenu", StringComparison.Ordinal);
        }

        private void LayoutBottomSideButtons(
            List<ClickableComponent> bottomSideButtons,
            int columnCenterX,
            ClickableComponent? trashBtn,
            ClickableComponent? okBtn,
            ClickableComponent? downArrow,
            SideButtonLayoutContext context,
            bool preferLeftSide
        )
        {
            if (bottomSideButtons.Count == 0)
                return;

            const int spacing = 8;
            Rectangle playerBounds = GetComponentListBounds(context.PlayerSlots);
            int minimumTopY = context.ShowScrollButtons
                ? Math.Max(playerBounds.Top, this.UpArrow.bounds.Bottom + spacing)
                : Math.Min(playerBounds.Top, context.ChestSlots.Min(slot => slot.bounds.Top));

            if (IsCompactLeftStorageLayout(context, context.ShowScrollButtons)
                && trashBtn != null
                && IsSingleChestsAnywhereSortButton(bottomSideButtons))
            {
                var sortButton = bottomSideButtons[0];
                int sortDirection = preferLeftSide ? -1 : 1;
                if (!HorizontalBottomButtonsFit(trashBtn, sortButton.bounds.Width, spacing, sortDirection))
                    sortDirection *= -1;

                LayoutBottomSideButtonsBesideAnchor(
                    bottomSideButtons,
                    trashBtn,
                    spacing,
                    sortDirection
                );

                LayoutDiagnostics.DebugChanged(
                    "ChestSideLayout:bottom-placement",
                    $"[FIV/ChestSideLayout] bottom placement: mode=compact-left-sort-beside-trash, sort={DescribeComponent(sortButton)}, trash={DescribeComponent(trashBtn)}, direction={(sortDirection > 0 ? "right" : "left")}, minTopY={minimumTopY}"
                );
                return;
            }

            // Chests Anywhere's SortInventoryButton has an explicit native layout:
            //   trash can
            //   sort-inventory
            //   OK
            // Its ChestOverlay creates the sort button immediately above OK, then moves the
            // trash can above the sort button. Preserve that order whenever it fits. If the
            // expanded inventory/scroll buttons leave too little vertical room, move only the
            // sort button beside OK and leave the trash/OK column in the normal FIV lower layout.
            if (trashBtn != null && okBtn != null && IsSingleChestsAnywhereSortButton(bottomSideButtons))
            {
                var sortButton = bottomSideButtons[0];
                int sortTopY = okBtn.bounds.Y - spacing - sortButton.bounds.Height;
                int trashTopY = sortTopY - spacing - trashBtn.bounds.Height;

                if (trashTopY >= minimumTopY)
                {
                    GridViewportLayoutHelpers.SetBoundsX(sortButton, columnCenterX - (sortButton.bounds.Width / 2));
                    GridViewportLayoutHelpers.SetBoundsY(sortButton, sortTopY);
                    GridViewportLayoutHelpers.SetBoundsX(trashBtn, columnCenterX - (trashBtn.bounds.Width / 2));
                    GridViewportLayoutHelpers.SetBoundsY(trashBtn, trashTopY);

                    LayoutDiagnostics.DebugChanged(
                        "ChestSideLayout:bottom-placement",
                        $"[FIV/ChestSideLayout] bottom placement: mode=chestsanywhere-native-between-trash-ok, sort={DescribeComponent(sortButton)}, trash={DescribeComponent(trashBtn)}, ok={DescribeComponent(okBtn)}, trashTopY={trashTopY}, minTopY={minimumTopY}"
                    );
                    return;
                }

                int sortOutwardDirection = preferLeftSide ? -1 : 1;
                if (!HorizontalBottomButtonsFit(okBtn, sortButton.bounds.Width, spacing, sortOutwardDirection))
                    sortOutwardDirection *= -1;

                LayoutBottomSideButtonsBesideAnchor(
                    bottomSideButtons,
                    okBtn,
                    spacing,
                    sortOutwardDirection
                );

                LayoutDiagnostics.DebugChanged(
                    "ChestSideLayout:bottom-placement",
                    $"[FIV/ChestSideLayout] bottom placement: mode=chestsanywhere-collision-beside-ok, sort={DescribeComponent(sortButton)}, trash={DescribeComponent(trashBtn)}, ok={DescribeComponent(okBtn)}, direction={(sortOutwardDirection > 0 ? "right" : "left")}, trashTopY={trashTopY}, minTopY={minimumTopY}"
                );
                return;
            }

            // Preferred lower stack order when OK and trash exist:
            //   trash
            //   external lower buttons
            //   OK
            //   scroll down (if visible)
            if (trashBtn != null && okBtn != null)
            {
                int totalButtonsHeight = bottomSideButtons.Sum(button => button.bounds.Height)
                    + spacing * Math.Max(0, bottomSideButtons.Count - 1);
                int buttonsTopY = okBtn.bounds.Y - spacing - totalButtonsHeight;
                int trashTopY = buttonsTopY - spacing - trashBtn.bounds.Height;

                if (trashTopY >= minimumTopY)
                {
                    GridViewportLayoutHelpers.SetBoundsX(trashBtn, columnCenterX - (trashBtn.bounds.Width / 2));
                    GridViewportLayoutHelpers.SetBoundsY(trashBtn, trashTopY);

                    int currentY = buttonsTopY;
                    foreach (var button in bottomSideButtons)
                    {
                        GridViewportLayoutHelpers.SetBoundsX(button, columnCenterX - (button.bounds.Width / 2));
                        GridViewportLayoutHelpers.SetBoundsY(button, currentY);
                        currentY += button.bounds.Height + spacing;
                    }

                    LayoutDiagnostics.DebugChanged(
                        "ChestSideLayout:bottom-placement",
                        $"[FIV/ChestSideLayout] bottom placement: mode=between-trash-ok, buttons={DescribeButtons(bottomSideButtons)}, trash={DescribeComponent(trashBtn)}, ok={DescribeComponent(okBtn)}, minTopY={minimumTopY}"
                    );
                    return;
                }
            }

            ClickableComponent? lowerAnchor = trashBtn ?? okBtn ?? downArrow;
            if (lowerAnchor == null)
                return;

            int totalHeight = bottomSideButtons.Sum(button => button.bounds.Height)
                + spacing * Math.Max(0, bottomSideButtons.Count - 1);
            int stackedTopY = lowerAnchor.bounds.Y - spacing - totalHeight;

            if (stackedTopY >= minimumTopY)
            {
                int currentY = stackedTopY;
                foreach (var button in bottomSideButtons)
                {
                    GridViewportLayoutHelpers.SetBoundsX(button, columnCenterX - (button.bounds.Width / 2));
                    GridViewportLayoutHelpers.SetBoundsY(button, currentY);
                    currentY += button.bounds.Height + spacing;
                }
                LayoutDiagnostics.DebugChanged(
                    "ChestSideLayout:bottom-placement",
                    $"[FIV/ChestSideLayout] bottom placement: mode=above-trash, buttons={DescribeButtons(bottomSideButtons)}, topY={stackedTopY}, minTopY={minimumTopY}, anchor={DescribeComponent(lowerAnchor)}"
                );
                return;
            }

            // There isn't enough vertical room in the lower stack. Put the custom lower
            // button(s) beside OK as the last fallback.
            ClickableComponent horizontalAnchor = okBtn ?? trashBtn ?? lowerAnchor;
            int rowWidth = bottomSideButtons.Sum(button => button.bounds.Width)
                + spacing * Math.Max(0, bottomSideButtons.Count - 1);
            int outwardDirection = preferLeftSide ? -1 : 1;
            if (!HorizontalBottomButtonsFit(horizontalAnchor, rowWidth, spacing, outwardDirection))
                outwardDirection *= -1;

            LayoutBottomSideButtonsBesideAnchor(
                bottomSideButtons,
                horizontalAnchor,
                spacing,
                outwardDirection
            );

            LayoutDiagnostics.DebugChanged(
                "ChestSideLayout:bottom-placement",
                $"[FIV/ChestSideLayout] bottom placement: mode=beside-ok, buttons={DescribeButtons(bottomSideButtons)}, rowWidth={rowWidth}, direction={(outwardDirection > 0 ? "right" : "left")}, stackedTopY={stackedTopY}, minTopY={minimumTopY}, anchor={DescribeComponent(horizontalAnchor)}"
            );
        }

        private static bool HorizontalBottomButtonsFit(
            ClickableComponent anchor,
            int rowWidth,
            int spacing,
            int direction
        )
        {
            const int margin = 8;
            int viewportWidth = Game1.uiViewport.Width;
            if (viewportWidth <= 0)
                viewportWidth = Game1.viewport.Width;

            if (direction > 0)
                return anchor.bounds.Right + spacing + rowWidth <= viewportWidth - margin;

            return anchor.bounds.Left - spacing - rowWidth >= margin;
        }

        private static void EnsureChestsAnywhereSortBesideOkNavigation(
            SideButtonLayoutContext context,
            List<ClickableComponent> lowerSideButtons,
            ClickableComponent? okBtn
        )
        {
            if (okBtn == null)
                return;

            ClickableComponent? sortButton = lowerSideButtons.FirstOrDefault(IsKnownOrphanExternalButton);
            if (sortButton == null || GridViewportLayoutHelpers.IsProtectedComponent(sortButton))
                return;

            // Native Chests Anywhere layout keeps sort-inventory in the same vertical column
            // between trash and OK. The generic side-column navigation already handles that.
            // This method only fixes the collision fallback where FIV has moved sort-inventory
            // horizontally beside OK.
            if (Math.Abs(sortButton.bounds.Center.X - okBtn.bounds.Center.X) <= 24)
                return;

            bool sortIsLeftOfOk = sortButton.bounds.Center.X < okBtn.bounds.Center.X;
            if (sortIsLeftOfOk)
            {
                GridViewportLayoutHelpers.SetLeftNeighbor(okBtn, sortButton.myID);
                GridViewportLayoutHelpers.SetRightNeighbor(sortButton, okBtn.myID);
            }
            else
            {
                GridViewportLayoutHelpers.SetRightNeighbor(okBtn, sortButton.myID);
                GridViewportLayoutHelpers.SetLeftNeighbor(sortButton, okBtn.myID);
            }

            // Keep vertical navigation for the side-by-side sort button consistent with the
            // OK column. Up should return to the nearest lower-column control above OK
            // (normally the trash can), and Down should continue to the next lower-column
            // control below OK (normally FIV's Scroll Down arrow when visible).
            int okColumnTolerance = Math.Max(32, okBtn.bounds.Width / 2);
            int referenceY = Math.Min(sortButton.bounds.Center.Y, okBtn.bounds.Center.Y);
            var okColumnControls = lowerSideButtons
                .Where(button =>
                    button != null
                    && button != sortButton
                    && !GridViewportLayoutHelpers.IsProtectedComponent(button)
                    && Math.Abs(button.bounds.Center.X - okBtn.bounds.Center.X) <= okColumnTolerance
                )
                .Distinct()
                .ToList();

            ClickableComponent? above = okColumnControls
                .Where(button => button.bounds.Center.Y < referenceY)
                .OrderBy(button => referenceY - button.bounds.Center.Y)
                .FirstOrDefault();
            ClickableComponent? below = okColumnControls
                .Where(button => button.bounds.Center.Y > referenceY)
                .OrderBy(button => button.bounds.Center.Y - referenceY)
                .FirstOrDefault();

            if (above != null)
                GridViewportLayoutHelpers.SetUpNeighbor(sortButton, above.myID);
            if (below != null)
                GridViewportLayoutHelpers.SetDownNeighbor(sortButton, below.myID);

            // The entry point from the player inventory edge should remain the OK column.
            // When the sort button is moved outward to the left, the generic closest-by-Y
            // wiring can otherwise make player slots jump directly to sort-inventory, which
            // skips the intended OK -> sort horizontal step.
            bool sideIsLeft = IsButtonOnLeftOfRelevantGrid(okBtn, context) || IsButtonOnLeftOfRelevantGrid(sortButton, context);
            bool sideIsRight = IsButtonOnRightOfRelevantGrid(okBtn, context) || IsButtonOnRightOfRelevantGrid(sortButton, context);
            if (sideIsLeft || sideIsRight)
            {
                List<ClickableComponent> edgeSlots = BuildSingleEdgeSlotColumn(context.PlayerSlots, context.PlayerColumns, sideIsLeft);
                foreach (var slot in edgeSlots.Where(slot => slot != null && !GridViewportLayoutHelpers.IsProtectedComponent(slot)))
                {
                    if (sideIsLeft && slot.leftNeighborID == sortButton.myID)
                        GridViewportLayoutHelpers.SetLeftNeighbor(slot, okBtn.myID);
                    else if (sideIsRight && slot.rightNeighborID == sortButton.myID)
                        GridViewportLayoutHelpers.SetRightNeighbor(slot, okBtn.myID);
                }
            }

            LayoutDiagnostics.DebugChanged(
                "ChestsAnywhereSortNavigation:beside-ok",
                $"[FIV/NavGraph] ChestsAnywhere sort beside OK navigation: sort={DescribeComponent(sortButton)}, ok={DescribeComponent(okBtn)}, above={DescribeComponent(above)}, below={DescribeComponent(below)}, side={(sortIsLeftOfOk ? "left-of-ok" : "right-of-ok")}"
            );
        }

        private static void LayoutBottomSideButtonsBesideAnchor(
            List<ClickableComponent> bottomSideButtons,
            ClickableComponent anchor,
            int spacing,
            int direction
        )
        {
            const int margin = 8;
            int viewportWidth = Game1.uiViewport.Width;
            if (viewportWidth <= 0)
                viewportWidth = Game1.viewport.Width;

            if (direction > 0)
            {
                int currentX = anchor.bounds.Right + spacing;
                foreach (var button in bottomSideButtons)
                {
                    GridViewportLayoutHelpers.SetBoundsX(button, currentX);
                    GridViewportLayoutHelpers.SetBoundsY(button, anchor.bounds.Center.Y - (button.bounds.Height / 2));
                    currentX += button.bounds.Width + spacing;
                }
            }
            else
            {
                int currentX = anchor.bounds.Left - spacing;
                foreach (var button in bottomSideButtons)
                {
                    currentX -= button.bounds.Width;
                    GridViewportLayoutHelpers.SetBoundsX(button, currentX);
                    GridViewportLayoutHelpers.SetBoundsY(button, anchor.bounds.Center.Y - (button.bounds.Height / 2));
                    currentX -= spacing;
                }
            }

            foreach (var button in bottomSideButtons)
            {
                if (button.bounds.Left < margin)
                    GridViewportLayoutHelpers.SetBoundsX(button, margin);
                if (viewportWidth > 0 && button.bounds.Right > viewportWidth - margin)
                    GridViewportLayoutHelpers.SetBoundsX(button, viewportWidth - margin - button.bounds.Width);
            }
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
                        GridViewportLayoutHelpers.OffsetBoundsX(button, deltaX);
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
            slots.AddRange(BuildSingleEdgeSlotColumn(chestSlots, chestColumns, leftEdge));
            slots.AddRange(BuildSingleEdgeSlotColumn(playerSlots, playerColumns, leftEdge));
            return slots;
        }

        private static List<ClickableComponent> BuildSingleEdgeSlotColumn(
            List<ClickableComponent> slots,
            int columns,
            bool leftEdge
        )
        {
            var edgeSlots = new List<ClickableComponent>();
            if (slots.Count == 0 || columns <= 0)
                return edgeSlots;

            int start = leftEdge ? 0 : columns - 1;
            for (int idx = start; idx < slots.Count; idx += columns)
            {
                if (idx >= 0 && idx < slots.Count)
                    edgeSlots.Add(slots[idx]);
            }
            return edgeSlots;
        }

        private static void PreventChestSlotsFromJumpingToLowerSideButtons(
            SideButtonLayoutContext context,
            List<ClickableComponent> lowerSideButtons
        )
        {
            var lowerIds = lowerSideButtons
                .Where(button => button != null && button.myID >= 0)
                .Select(button => button.myID)
                .ToHashSet();
            if (lowerIds.Count == 0)
                return;

            foreach (var chestSlot in context.ChestSlots.Where(slot => slot != null))
            {
                if (!lowerIds.Contains(chestSlot.downNeighborID))
                    continue;

                var playerTarget = FindPlayerSlotBelowByColumn(context.PlayerSlots, chestSlot);
                int oldDown = chestSlot.downNeighborID;
                GridViewportLayoutHelpers.SetDownNeighbor(chestSlot, playerTarget?.myID ?? -500);
                Log.Debug(
                    $"[FIV/NavGraph] fixed chest->lower leak: chest={DescribeComponent(chestSlot)}, oldDown={oldDown}, newDown={chestSlot.downNeighborID}, playerTarget={DescribeComponent(playerTarget)}"
                );
            }
        }

        private static ClickableComponent? FindPlayerSlotBelowByColumn(
            List<ClickableComponent> playerSlots,
            ClickableComponent chestSlot
        )
        {
            ClickableComponent? closest = null;
            int bestDistance = int.MaxValue;
            int chestX = chestSlot.bounds.Center.X;
            foreach (var slot in playerSlots.Where(slot => slot != null))
            {
                int distance = Math.Abs(slot.bounds.Center.X - chestX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closest = slot;
                }
            }

            // If the chest is much wider than the player inventory, some bottom chest slots
            // don't have a sensible player slot underneath them. In that case, leave DOWN
            // unhandled instead of jumping to a lower side button like the scroll arrow.
            return bestDistance <= 48 ? closest : null;
        }

        private static void LogSideNavigationSnapshot(
            SideButtonLayoutContext context,
            List<ClickableComponent> topSideButtons,
            List<ClickableComponent> lowerSideButtons
        )
        {
            Log.Debug(
                $"[FIV/NavGraph] snapshot: top={DescribeButtons(topSideButtons)}, lower={DescribeButtons(lowerSideButtons)}, chestLastRow={DescribeButtons(GetLastRowSlots(context.ChestSlots, context.ChestColumns), 16)}, playerFirstRow={DescribeButtons(GetFirstRowSlots(context.PlayerSlots, context.PlayerColumns), 16)}"
            );
        }

        private static List<ClickableComponent> GetLastRowSlots(List<ClickableComponent> slots, int columns)
        {
            if (slots.Count == 0 || columns <= 0)
                return new List<ClickableComponent>();
            int start = Math.Max(0, slots.Count - columns);
            return slots.Skip(start).Take(columns).ToList();
        }

        private static List<ClickableComponent> GetFirstRowSlots(List<ClickableComponent> slots, int columns)
        {
            if (slots.Count == 0 || columns <= 0)
                return new List<ClickableComponent>();
            return slots.Take(columns).ToList();
        }

        private void EnsureArrowAnchorBridge(
            SideButtonLayoutContext context,
            List<ClickableComponent> topSideButtons,
            List<ClickableComponent> lowerSideButtons,
            List<ClickableComponent> allSideButtons
        )
        {
            this.UpdateAuxScrollBarVisibility(context, allSideButtons);
            if (!context.ShowScrollButtons)
                return;

            // The upper scroll arrow is the first lower/player-side control. When it is visually
            // below a top/chest-side button in the same side column (usually Organize), gamepad
            // navigation must bridge the two clusters. Splitting top buttons from lower buttons
            // prevented chest slots from jumping to lower controls, but it also isolated UpArrow.
            ClickableComponent? anchor = context.ArrowAnchorComponentOverride;
            if (anchor == null || !IsValidArrowAnchor(anchor, topSideButtons))
            {
                anchor = topSideButtons
                    .Where(button => IsValidArrowAnchor(button, topSideButtons))
                    .Where(button => button.bounds.Center.Y < this.UpArrow.bounds.Center.Y)
                    .Where(button => GridViewportLayoutHelpers.BoundsOverlapHorizontally(button.bounds, this.UpArrow.bounds, 32))
                    .OrderBy(button => this.UpArrow.bounds.Center.Y - button.bounds.Center.Y)
                    .FirstOrDefault();
            }

            if (anchor == null)
            {
                Log.Debug(
                    $"[FIV/NavGraph] upper arrow bridge skipped: no same-column top anchor, up={DescribeComponent(this.UpArrow)}, top={DescribeButtons(topSideButtons)}, lower={DescribeButtons(lowerSideButtons)}"
                );
                return;
            }

            bool hasIntermediateComponent = allSideButtons.Any(c =>
                c != null
                && c != this.UpArrow
                && c != this.DownArrow
                && c != anchor
                && GridViewportLayoutHelpers.BoundsOverlapHorizontally(c.bounds, this.UpArrow.bounds, 32)
                && c.bounds.Center.Y > anchor.bounds.Center.Y
                && c.bounds.Center.Y < this.UpArrow.bounds.Center.Y
            );

            if (hasIntermediateComponent)
            {
                Log.Debug(
                    $"[FIV/NavGraph] upper arrow bridge skipped: intermediate component, anchor={DescribeComponent(anchor)}, up={DescribeComponent(this.UpArrow)}, side={DescribeButtons(allSideButtons)}"
                );
                return;
            }

            GridViewportLayoutHelpers.SetDownNeighbor(anchor, this.UpArrow.myID);
            GridViewportLayoutHelpers.SetUpNeighbor(this.UpArrow, anchor.myID);
            Log.Debug(
                $"[FIV/NavGraph] upper arrow bridge: anchor={DescribeComponent(anchor)}, up={DescribeComponent(this.UpArrow)}"
            );
        }

        private bool IsValidArrowAnchor(ClickableComponent? button, List<ClickableComponent> topSideButtons)
        {
            return button != null
                && button != this.UpArrow
                && button != this.DownArrow
                && topSideButtons.Contains(button);
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
    

        // ---- Scroll and input ----
        public int GetTotalRows(IList<Item> items)
        {
            int columns = this.Menu.capacity / this.Menu.rows;
            if (columns <= 0)
                columns = InventoryGridMetrics.DefaultColumnCount;

            return InventoryGridMetrics.GetRequiredRows(this.GetEffectiveItemCount(items), columns);
        }

        private int GetEffectiveItemCount(IList<Item> items)
        {
            bool isPlayerInventory = ReferenceEquals(items, Game1.player?.Items);
            return InventoryGridMetrics.GetEffectiveSlotCount(items, isPlayerInventory);
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

        public bool Scroll(IList<Item> items, int delta, string reason = "manual")
        {
            int max = this.GetMaxScrollRow(items);
            int previous = this.ScrollRow;
            this.ScrollRow = Math.Clamp(this.ScrollRow + delta, 0, max);
            if (this.ScrollRow == previous)
                return false;

            this.ApplyVisibleSlotWindow(items, $"scroll:{reason}");
            Game1.playSound("shwip");
            return true;
        }

        public void ApplyVisibleSlotWindow(IList<Item> items, string reason = "manual")
        {
            if (items == null || this.Menu.inventory == null || this.Menu.inventory.Count == 0)
                return;

            int columns = InventoryGridMetrics.GetColumns(this.Menu.capacity, this.Menu.rows);
            int effectiveCount = this.GetEffectiveItemCount(items);
            int totalRows = InventoryGridMetrics.GetRequiredRows(effectiveCount, columns);
            int maxScroll = Math.Max(0, totalRows - this.Menu.rows);
            int oldScroll = this.ScrollRow;
            this.ScrollRow = Math.Clamp(this.ScrollRow, 0, maxScroll);

            var hash = new HashCode();
            hash.Add(this.Menu.GetHashCode());
            hash.Add(this.Menu.capacity);
            hash.Add(this.Menu.rows);
            hash.Add(columns);
            hash.Add(this.Menu.inventory.Count);
            hash.Add(items.Count);
            hash.Add(effectiveCount);
            hash.Add(this.ScrollRow);
            if (this.Menu.inventory.Count > 0)
            {
                var first = this.Menu.inventory[0].bounds;
                hash.Add(first.X);
                hash.Add(first.Y);
                hash.Add(first.Width);
                hash.Add(first.Height);
            }

            int signature = hash.ToHashCode();
            if (signature == this.LastAppliedSlotWindowSignature && oldScroll == this.ScrollRow)
                return;

            this.LastAppliedSlotWindowSignature = signature;

            var slots = this.Menu.inventory;
            int visibleCapacity = Math.Min(this.Menu.capacity, slots.Count);
            int visibleStart = this.ScrollRow * columns;

            for (int i = 0; i < visibleCapacity; i++)
            {
                ClickableComponent slot = slots[i];
                if (slot == null)
                    continue;

                if (!this.OriginalSlotBounds.ContainsKey(slot))
                    this.OriginalSlotBounds[slot] = slot.bounds;

                // Keep slot geometry stable. The item offset is handled by the inventory
                // source/window; scrolling should not move side buttons or rebuild the parent menu.
                GridViewportLayoutHelpers.SetBounds(slot, this.OriginalSlotBounds[slot]);

                int row = i / columns;
                int col = i % columns;

                // Internal grid navigation is owned by the viewport. Boundary links are left
                // untouched so side buttons, trash, OK, drop target, and chest/top links stay intact.
                if (col > 0 && i - 1 < slots.Count && slots[i - 1] != null)
                    GridViewportLayoutHelpers.SetLeftNeighbor(slot, slots[i - 1].myID);
                if (col < columns - 1 && i + 1 < slots.Count && slots[i + 1] != null)
                    GridViewportLayoutHelpers.SetRightNeighbor(slot, slots[i + 1].myID);
                if (row > 0 && i - columns >= 0 && slots[i - columns] != null)
                    GridViewportLayoutHelpers.SetUpNeighbor(slot, slots[i - columns].myID);
                if (row < this.Menu.rows - 1 && i + columns < slots.Count && slots[i + columns] != null)
                    GridViewportLayoutHelpers.SetDownNeighbor(slot, slots[i + columns].myID);
            }

            LayoutDiagnostics.DebugChanged(
                $"GridViewport:slot-window:{this.Menu.GetHashCode()}",
                $"[FIV/GridViewport] slot window applied: reason={reason}, menuHash={this.Menu.GetHashCode()}, rows={this.Menu.rows}, capacity={this.Menu.capacity}, columns={columns}, sourceCount={items.Count}, effectiveSlots={effectiveCount}, visibleStart={visibleStart}, scrollRow={oldScroll}->{this.ScrollRow}, maxScroll={maxScroll}"
            );
        }

        public void Draw(SpriteBatch b, IList<Item> items)
        {
            if (items == null || this.GetEffectiveItemCount(items) <= this.Menu.capacity)
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
            if (items == null || this.GetEffectiveItemCount(items) <= this.Menu.capacity)
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
            if (items == null || this.GetEffectiveItemCount(items) <= this.Menu.capacity)
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
            if (items == null || this.GetEffectiveItemCount(items) <= this.Menu.capacity)
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
            if (items == null || this.GetEffectiveItemCount(items) <= this.Menu.capacity)
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
                    this.Scroll(items, currentDirection, "right-stick");
                }
            }
            this.LastRightStickDirection = currentDirection;
        }

        public void UpdatePointerInteraction(IList<Item> items)
        {
            if (items == null || this.GetEffectiveItemCount(items) <= this.Menu.capacity)
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
                this.ApplyVisibleSlotWindow(items, "scrollbar-drag");
                return;
            }

            float progress = maxY == minY ? 0f : (thumbY - minY) / (float)(maxY - minY);
            int previousScrollRow = this.ScrollRow;
            this.ScrollRow = Math.Clamp((int)Math.Round(progress * maxScroll), 0, maxScroll);
            if (previousScrollRow != this.ScrollRow)
                this.ApplyVisibleSlotWindow(items, "scrollbar-drag");
            if (previousY != thumbY)
            {
                Game1.playSound("shiny4");
            }
        }
    

    }
}
