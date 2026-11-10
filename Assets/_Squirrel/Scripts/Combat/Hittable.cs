using System.Collections.Generic;
using UnityEngine;

namespace Squirrel.Combat
{
    public enum HitSource { Tail, Acorn, Enemy, Edge }

    public struct HitInfo
    {
        public int damage;
        /// <summary>Куда направлен удар (единичный вектор).</summary>
        public Vector2 direction;
        public HitSource source;
    }

    /// <summary>
    /// Всё, по чему может попасть белка: враги, споры, манекен.
    /// Попадания считаются по прямоугольникам из общего реестра, без физики:
    /// так не нужен Rigidbody и нет задержки синхронизации трансформов.
    /// </summary>
    public abstract class Hittable : MonoBehaviour
    {
        static readonly List<Hittable> all = new List<Hittable>();

        [Tooltip("Размер «тела» для попаданий и касаний")]
        [SerializeField] protected Vector2 hitSize = Vector2.one;
        [Tooltip("Сдвиг центра тела от позиции объекта (позиция обычно у ног)")]
        [SerializeField] protected Vector2 hitOffset = new Vector2(0f, 0.5f);

        public Vector2 HitCenter => (Vector2)transform.position + hitOffset;
        public Vector2 HitSize => hitSize;
        public Rect HitRect => new Rect(HitCenter - hitSize * 0.5f, hitSize);

        public virtual bool CanBeHit => isActiveAndEnabled;
        /// <summary>Даёт ли попадание хвостом сок (споры — нет).</summary>
        public virtual bool GivesSap => true;

        /// <summary>Возвращает true, если попадание засчитано (сок, отскок, стоп-кадр).</summary>
        public abstract bool TakeHit(in HitInfo hit);

        protected virtual void OnEnable() => all.Add(this);
        protected virtual void OnDisable() => all.Remove(this);

        /// <summary>Все цели, чьё тело пересекается с area. Список копируется: цели могут исчезать при попадании.</summary>
        public static void Query(Rect area, List<Hittable> results)
        {
            results.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                var h = all[i];
                if (h != null && h.CanBeHit && h.HitRect.Overlaps(area)) results.Add(h);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(HitCenter, hitSize);
        }
    }
}
