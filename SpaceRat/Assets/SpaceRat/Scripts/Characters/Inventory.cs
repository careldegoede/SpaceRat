using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceRat.Characters
{
    /// <summary>
    /// What the pilot is carrying: item ids with counts (e.g. "fusion_cell"). No UI yet; other systems
    /// check <see cref="Has"/> and listen to <see cref="Changed"/>.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public string displayName;
            public int count;
        }

        [SerializeField] List<Entry> items = new();

        public IReadOnlyList<Entry> Items => items;

        /// <summary>Raised with the item id after any add or remove.</summary>
        public event Action<string> Changed;

        public bool Has(string id, int count = 1) => Count(id) >= count;

        public int Count(string id)
        {
            foreach (var e in items) if (e.id == id) return e.count;
            return 0;
        }

        public void Add(string id, string displayName = null, int count = 1)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].id != id) continue;
                var e = items[i];
                e.count += count;
                items[i] = e;
                Changed?.Invoke(id);
                return;
            }
            items.Add(new Entry { id = id, displayName = displayName ?? id, count = count });
            Changed?.Invoke(id);
        }

        public bool Remove(string id, int count = 1)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].id != id) continue;
                if (items[i].count < count) return false;
                var e = items[i];
                e.count -= count;
                if (e.count <= 0) items.RemoveAt(i); else items[i] = e;
                Changed?.Invoke(id);
                return true;
            }
            return false;
        }
    }
}
