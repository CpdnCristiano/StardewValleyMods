using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.MenuAdapters
{
    internal interface IMenuGridAdapter
    {
        IClickableMenu Menu { get; }

        IReadOnlyList<InventoryMenu> InventoryMenus { get; }
    }
}
