using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher
{
    public class GridViewport
    {
        public InventoryMenu Menu { get; }
        public int ScrollRow { get; set; } = 0;

        public ClickableTextureComponent UpArrow { get; private set; }
        public ClickableTextureComponent DownArrow { get; private set; }

        public int LastRightStickDirection { get; set; } = 0;

        public int Depth { get; set; } = 0;
        public IList<Item>? FullInventory { get; set; }
        public IList<Item>? OriginalInventory { get; set; }
        public int? OriginalMaxItems { get; set; }

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
        }

        public bool CustomArrowLayout { get; set; } = false;

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

            this.LayoutArrows();

            float upAlpha = this.CanScroll(items, -1) ? 1f : 0.35f;
            float downAlpha = this.CanScroll(items, 1) ? 1f : 0.35f;

            this.UpArrow.draw(b, Color.White * upAlpha, 0.9f);
            this.DownArrow.draw(b, Color.White * downAlpha, 0.9f);
        }

        public void PerformHoverAction(int x, int y, IList<Item> items)
        {
            if (items == null || items.Count <= this.Menu.capacity)
                return;

            bool canScrollUp = this.CanScroll(items, -1);
            bool canScrollDown = this.CanScroll(items, 1);

            this.UpArrow.scale = this.UpArrow.containsPoint(x, y) && canScrollUp ? 4.1f : 4f;
            this.DownArrow.scale = this.DownArrow.containsPoint(x, y) && canScrollDown ? 4.1f : 4f;
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
    }
}
