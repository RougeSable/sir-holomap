using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;

namespace SirHolomap
{
    // While the near space view is drawn by the game from the map's camera,
    // the grids the boxes of the panel leave out (or under the block
    // threshold) are hidden from the game's renderer too: what the view draws
    // and what the list shows stay the same. Only on this machine, only while
    // the map shows that view: every grid hidden here is shown again as soon
    // as the map closes, changes view, or a box is ticked again.
    internal static class GridHiding
    {
        private static readonly Dictionary<long, IMyEntity> s_hidden = new Dictionary<long, IMyEntity>();
        private static readonly List<long> s_gone = new List<long>();

        public static int Count
        {
            get { return s_hidden.Count; }
        }

        // Hides exactly these grids: the ones hidden before and not in the set
        // are shown again.
        public static void Apply(HashSet<long> hide)
        {
            s_gone.Clear();
            foreach (var pair in s_hidden)
            {
                if (!hide.Contains(pair.Key))
                    s_gone.Add(pair.Key);
            }
            foreach (var id in s_gone)
            {
                Show(s_hidden[id]);
                s_hidden.Remove(id);
            }
            s_gone.Clear();

            var entities = MyAPIGateway.Entities;
            if (entities == null)
                return;
            foreach (var id in hide)
            {
                if (s_hidden.ContainsKey(id))
                    continue;
                IMyEntity entity;
                if (!entities.TryGetEntityById(id, out entity) || entity == null || entity.MarkedForClose || entity.Closed)
                    continue;
                // Hidden by the game itself: not ours to show again later.
                if (!entity.Visible)
                    continue;
                try
                {
                    entity.Visible = false;
                    s_hidden[id] = entity;
                }
                catch (Exception)
                {
                }
            }
        }

        public static void RestoreAll()
        {
            if (s_hidden.Count == 0)
                return;
            foreach (var entity in s_hidden.Values)
                Show(entity);
            s_hidden.Clear();
        }

        private static void Show(IMyEntity entity)
        {
            try
            {
                if (entity == null || entity.Closed || entity.Visible)
                    return;
                entity.Visible = true;
                // Showing a grid shows all it carries: what the game had hidden
                // in it (a block's part, a seated character) is hidden again.
                HideAgain(entity, 0);
            }
            catch (Exception)
            {
            }
        }

        private static void HideAgain(IMyEntity entity, int depth)
        {
            if (depth > 16 || entity.Hierarchy == null)
                return;
            foreach (MyHierarchyComponentBase child in entity.Hierarchy.Children)
            {
                var inner = child != null && child.Container != null ? child.Container.Entity : null;
                if (inner == null || inner.Closed)
                    continue;
                if (!inner.Visible)
                {
                    inner.Visible = true;
                    inner.Visible = false;
                    continue;
                }
                HideAgain(inner, depth + 1);
            }
        }
    }
}
