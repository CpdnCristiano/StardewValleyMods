using StardewValley;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Collections
{
    internal sealed class ScrollableInventoryList : IList<Item>
    {
        private readonly int _offset;
        private readonly int _capacity;
        private readonly int _virtualCount;

        public ScrollableInventoryList(IList<Item> underlying, int offset, int capacity, int? virtualCount = null)
        {
            Underlying = underlying;
            _offset = offset;
            _capacity = capacity;
            _virtualCount = Math.Max(underlying.Count, virtualCount ?? underlying.Count);
        }

        public IList<Item> Underlying { get; }

        public Item this[int index]
        {
            get
            {
                int actualIndex = _offset + index;
                return actualIndex >= 0 && actualIndex < Underlying.Count ? Underlying[actualIndex] : null!;
            }
            set
            {
                int actualIndex = _offset + index;
                if (actualIndex < 0 || actualIndex >= _virtualCount)
                    return;

                while (Underlying.Count <= actualIndex && Underlying.Count < _virtualCount)
                    Underlying.Add(null!);

                if (actualIndex < Underlying.Count)
                    Underlying[actualIndex] = value;
            }
        }

        public int Count => Math.Max(0, Math.Min(_capacity, _virtualCount - _offset));
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
