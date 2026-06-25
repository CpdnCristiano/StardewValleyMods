using CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Reflection;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.MenuAdapters
{
    internal sealed class MenuGridAdapter : IMenuGridAdapter
    {
        public MenuGridAdapter(IClickableMenu menu)
        {
            this.Menu = menu;
            this.InventoryMenus = MenuComponentFinder.FindInventoryMenus(menu);
        }

        public IClickableMenu Menu { get; }

        public IReadOnlyList<InventoryMenu> InventoryMenus { get; }
    }
}
