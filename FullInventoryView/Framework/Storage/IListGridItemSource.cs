using StardewValley;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Storage
{
    internal sealed class IListGridItemSource : IGridItemSource
    {
        private readonly IList<Item> items;

        public IListGridItemSource(IList<Item> items)
        {
            this.items = items;
        }

        public int Count => this.items.Count;

        public Item? GetItem(int index)
        {
            return index >= 0 && index < this.items.Count ? this.items[index] : null;
        }

        public void SetItem(int index, Item? item)
        {
            if (index < 0 || index >= this.items.Count)
                return;

            // Stardew inventory lists intentionally allow empty/null slots even when the generic type is Item.
            this.items[index] = item!;
        }
    }
}
