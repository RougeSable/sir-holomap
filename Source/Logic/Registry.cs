using System;
using System.Collections.Generic;

namespace SirHolomap
{
    // Ce que la carte relit sans cesse au jeu (les planètes, toutes les
    // quelques secondes), chacun gardé comme un seul objet aussi longtemps qu'il existe. Ce que
    // le joueur a sélectionné, survolé ou plongé est cet objet même : un
    // rafraîchissement le met à jour sur place, si bien que sa surbrillance ne tombe jamais.
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

        // L'objet de cet identifiant : celui déjà connu, ou un nouveau.
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

        // Ce qui n'a pas été gardé pendant ce rafraîchissement a disparu du monde.
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

        // Une sélection gardée d'un rafraîchissement à l'autre : l'objet courant du même
        // identifiant, ou rien quand il a disparu.
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
