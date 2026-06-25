using StardewValley;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Collections
{
    internal sealed class ScrollableInventoryList : IList<Item>
    {
        private readonly int _offset;
        private readonly int _capacity;

        public ScrollableInventoryList(IList<Item> underlying, int offset, int capacity)
        {
            Underlying = underlying;
            _offset = offset;
            _capacity = capacity;
        }

        public IList<Item> Underlying { get; }

        public Item this[int index]
        {
            get => Underlying[_offset + index];
            set => Underlying[_offset + index] = value;
        }

        public int Count => Math.Max(0, Math.Min(_capacity, Underlying.Count - _offset));
        public bool IsReadOnly => Underlying.IsReadOnly;

        public void Add(Item item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(Item item) => IndexOf(item) >= 0;

        public void CopyTo(Item[] array, int arrayIndex)
        {
            for (int i = 0; i < Count; i++)
                array[arrayIndex + i] = this[i];
        }

        public IEnumerator<Item> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return this[i];
        }

        public int IndexOf(Item item)
        {
            for (int i = 0; i < Count; i++)
            {
                if (Equals(this[i], item))
                    return i;
            }
            return -1;
        }

        public void Insert(int index, Item item) => throw new NotSupportedException();

        public bool Remove(Item item) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
