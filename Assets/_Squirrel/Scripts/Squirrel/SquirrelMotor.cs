using UnityEngine;

namespace Squirrel
{
    /// <summary>
    /// Кинематическое перемещение на BoxCast'ах, без Rigidbody.
    /// Двигает transform по X, потом по Y и останавливается на коллайдерах (не триггерах).
    /// Свой BoxCollider2D должен быть триггером: так он не мешает собственным проверкам.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class SquirrelMotor : MonoBehaviour
    {
        public struct Contacts
        {
            public bool below, above, left, right;
        }

        [Tooltip("Зазор между белкой и стеной, ед")]
        [SerializeField] float skin = 0.015f;

        BoxCollider2D box;
        ContactFilter2D filter;
        readonly RaycastHit2D[] hits = new RaycastHit2D[16];

        public Contacts Last { get; private set; }

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = Physics2D.GetLayerCollisionMask(gameObject.layer)
            };
        }

        Vector2 Center => transform.TransformPoint(box.offset);
        Vector2 Size
        {
            get
            {
                Vector3 s = transform.lossyScale;
                return new Vector2(box.size.x * Mathf.Abs(s.x), box.size.y * Mathf.Abs(s.y));
            }
        }

        public Vector2 WorldCenter => Center;
        public Vector2 WorldSize => Size;
        /// <summary>Тело белки в мировых координатах — для попаданий врагов.</summary>
        public Rect WorldRect => new Rect(Center - Size * 0.5f, Size);

        /// <summary>Сколько можно пройти из origin в направлении dir, не врезавшись (не больше distance).</summary>
        float FreeDistance(Vector2 origin, Vector2 dir, float distance, out bool blocked)
        {
            blocked = false;
            Vector2 castSize = Size - Vector2.one * (skin * 2f);
            int n = Physics2D.BoxCast(origin, castSize, 0f, dir, filter, hits, distance + skin);
            float best = distance;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == box) continue;
                // Игнорируем поверхности, от которых мы и так уходим (например, начальное перекрытие).
                if (Vector2.Dot(h.normal, dir) >= 0f) continue;
                float d = Mathf.Max(0f, h.distance - skin);
                if (d < best) { best = d; blocked = true; }
            }
            return best;
        }

        /// <summary>Касается ли белка чего-то твёрдого в направлении dir.</summary>
        public bool Probe(Vector2 dir, float distance = 0.04f)
        {
            FreeDistance(Center, dir, distance, out bool blocked);
            return blocked;
        }

        /// <summary>Перемещение с остановкой о препятствия. cornerCorrection — сдвиг вбок при ударе головой об угол.</summary>
        public void Move(Vector2 delta, float cornerCorrection)
        {
            var c = new Contacts();

            if (!Mathf.Approximately(delta.x, 0f))
            {
                Vector2 dir = new Vector2(Mathf.Sign(delta.x), 0f);
                float d = FreeDistance(Center, dir, Mathf.Abs(delta.x), out bool blocked);
                transform.position += (Vector3)(dir * d);
                if (blocked) { if (dir.x > 0) c.right = true; else c.left = true; }
            }

            if (!Mathf.Approximately(delta.y, 0f))
            {
                Vector2 dir = new Vector2(0f, Mathf.Sign(delta.y));
                float dist = Mathf.Abs(delta.y);
                float d = FreeDistance(Center, dir, dist, out bool blocked);

                if (blocked && dir.y > 0f && cornerCorrection > 0f && TryCornerCorrect(dist, cornerCorrection))
                {
                    blocked = false;
                    d = dist;
                }

                transform.position += (Vector3)(dir * d);
                if (blocked) { if (dir.y > 0) c.above = true; else c.below = true; }
            }

            Last = c;
        }

        bool TryCornerCorrect(float upDistance, float maxShift)
        {
            const float step = 0.03f;
            for (float shift = step; shift <= maxShift + 0.0001f; shift += step)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 side = new Vector2(s, 0f);
                    float free = FreeDistance(Center, side, shift, out bool sideBlocked);
                    if (sideBlocked || free < shift) continue;
                    Vector2 shifted = Center + side * shift;
                    FreeDistance(shifted, Vector2.up, upDistance, out bool upBlocked);
                    if (upBlocked) continue;
                    transform.position += (Vector3)(side * shift);
                    return true;
                }
            }
            return false;
        }

        public void Teleport(Vector2 position)
        {
            transform.position = position;
            Last = new Contacts();
        }
    }
}
