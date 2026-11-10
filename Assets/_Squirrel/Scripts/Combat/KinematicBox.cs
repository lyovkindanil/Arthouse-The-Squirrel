using System.Collections.Generic;
using UnityEngine;

namespace Squirrel.Combat
{
    /// <summary>
    /// Перемещение прямоугольника с остановкой о твёрдые коллайдеры (не триггеры) — для врагов и снарядов.
    /// У врагов нет своих коллайдеров, поэтому они не мешают ни себе, ни белке.
    /// </summary>
    public static class KinematicBox
    {
        const float Skin = 0.02f;
        static readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        static readonly List<Collider2D> overlaps = new List<Collider2D>();

        static ContactFilter2D Filter
        {
            get
            {
                var f = new ContactFilter2D { useTriggers = false };
                f.SetLayerMask(Physics2D.DefaultRaycastLayers);
                return f;
            }
        }

        /// <summary>Двигает прямоугольник center/size на delta: сначала по X, потом по Y. Возвращает новый центр.</summary>
        public static Vector2 Move(Vector2 center, Vector2 size, Vector2 delta, out bool hitX, out bool hitY)
        {
            hitX = hitY = false;
            Vector2 cast = size - Vector2.one * (Skin * 2f);
            if (!Mathf.Approximately(delta.x, 0f))
            {
                var dir = new Vector2(Mathf.Sign(delta.x), 0f);
                center += dir * Free(center, cast, dir, Mathf.Abs(delta.x), out hitX);
            }
            if (!Mathf.Approximately(delta.y, 0f))
            {
                var dir = new Vector2(0f, Mathf.Sign(delta.y));
                center += dir * Free(center, cast, dir, Mathf.Abs(delta.y), out hitY);
            }
            return center;
        }

        static float Free(Vector2 origin, Vector2 cast, Vector2 dir, float dist, out bool blocked)
        {
            blocked = false;
            int n = Physics2D.BoxCast(origin, cast, 0f, dir, Filter, hits, dist + Skin);
            float best = dist;
            for (int i = 0; i < n; i++)
            {
                if (Vector2.Dot(hits[i].normal, dir) >= 0f) continue;
                float d = Mathf.Max(0f, hits[i].distance - Skin);
                if (d < best) { best = d; blocked = true; }
            }
            return best;
        }

        /// <summary>Луч до первой твёрдой поверхности.</summary>
        public static bool Raycast(Vector2 from, Vector2 dir, float dist, out RaycastHit2D hit)
        {
            int n = Physics2D.Raycast(from, dir, Filter, hits, dist);
            hit = default;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (hits[i].distance < best) { best = hits[i].distance; hit = hits[i]; }
            return n > 0;
        }

        /// <summary>Нет ли стен между двумя точками.</summary>
        public static bool LineOfSight(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 0.001f) return true;
            return !Raycast(from, d / len, len, out _);
        }

        public static bool IsSolid(Vector2 point)
        {
            return Physics2D.OverlapPoint(point, Filter, overlaps) > 0;
        }
    }
}
