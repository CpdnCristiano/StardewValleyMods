using StardewValley;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Storage
{
    internal interface IGridItemSource
    {
        int Count { get; }

        Item? GetItem(int index);

        void SetItem(int index, Item? item);
    }
}
