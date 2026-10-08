using System;
using System.Collections.Generic;

namespace SirHolomap
{
    // Things the map reads again and again from the game (the planets, every
    // few seconds), each kept as one object for as long as it exists. What
    // the player selected, hovered or dived into is that very object: a
    // refresh updates it in place, so its highlight never drops.
    public sealed class Registry<T> where T : class
    {
        private readonly Dictionary<long, T> m_items = new Dictionary<long, T>();
        private readonly HashSet<long> m_seen = new HashSet<long>();
        private readonly List<long> m_gone = new List<long>();
        private readonly Func<long, T> m_create;

        public Registry(Func<long, T> create)
        {
            if (create == null)
                throw new ArgumentNullException("create");
            m_create = create;
        }

        public int Count
        {
            get { return m_items.Count; }
        }

        public void BeginRefresh()
        {
            m_seen.Clear();
        }

        // The object of this id: the one already known, or a new one.
        public T Keep(long id)
        {
            T item;
            if (!m_items.TryGetValue(id, out item))
            {
                item = m_create(id);
                m_items[id] = item;
            }
            m_seen.Add(id);
            return item;
        }

        // What was not kept during this refresh is gone from the world.
        public void EndRefresh()
        {
            m_gone.Clear();
            foreach (var id in m_items.Keys)
            {
                if (!m_seen.Contains(id))
                    m_gone.Add(id);
            }
            foreach (var id in m_gone)
                m_items.Remove(id);
            m_gone.Clear();
        }

        public bool TryGet(long id, out T item)
        {
            return m_items.TryGetValue(id, out item);
        }

        // A selection kept across refreshes: the current object of the same
        // id, or nothing when it is gone.
        public T Follow(T selected, Func<T, long> idOf)
        {
            if (selected == null)
                return null;
            T current;
            return m_items.TryGetValue(idOf(selected), out current) ? current : null;
        }

        public void Clear()
        {
            m_items.Clear();
            m_seen.Clear();
        }
    }
}
