using System.Collections.Generic;
using UnityEngine;

namespace Squirrel.Combat
{
    /// <summary>Брошенный жёлудь: летит по лёгкой дуге, бьёт первую цель, разбивается о стены.</summary>
    public class Acorn : MonoBehaviour
    {
        const float Radius = 0.18f;

        Vector2 velocity;
        float gravity, life;
        int damage;
        readonly List<Hittable> found = new List<Hittable>();

        public static Acorn Spawn(Vector2 position, Vector2 velocity, CombatSettings s, Sprite sprite, Material material)
        {
            var go = new GameObject("Acorn");
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.color = new Color(0.55f, 0.33f, 0.15f);
            sr.sortingOrder = 115;
            var a = go.AddComponent<Acorn>();
            a.velocity = velocity;
            a.gravity = s.acornGravity;
            a.life = s.acornLifetime;
            a.damage = s.acornDamage;
            return a;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            life -= dt;
            if (life <= 0f) { Destroy(gameObject); return; }

            velocity.y -= gravity * dt;
            Vector2 pos = transform.position;
            Vector2 step = velocity * dt;
            float len = step.magnitude;
            if (len > 0f && KinematicBox.Raycast(pos, step / len, len + Radius, out var wall))
            {
                transform.position = wall.point;
                if (HitTargets(Rect.MinMaxRect(Mathf.Min(pos.x, wall.point.x) - Radius, Mathf.Min(pos.y, wall.point.y) - Radius,
                                               Mathf.Max(pos.x, wall.point.x) + Radius, Mathf.Max(pos.y, wall.point.y) + Radius))) return;
                Destroy(gameObject);
                return;
            }

            Vector2 next = pos + step;
            transform.position = next;
            transform.Rotate(0f, 0f, -720f * dt);
            // Прямоугольник по всему пути за кадр, чтобы быстрый жёлудь не пролетал сквозь тонких врагов.
            HitTargets(Rect.MinMaxRect(Mathf.Min(pos.x, next.x) - Radius, Mathf.Min(pos.y, next.y) - Radius,
                                       Mathf.Max(pos.x, next.x) + Radius, Mathf.Max(pos.y, next.y) + Radius));
        }

        bool HitTargets(Rect area)
        {
            Hittable.Query(area, found);
            foreach (var h in found)
            {
                var info = new HitInfo { damage = damage, direction = velocity.normalized, source = HitSource.Acorn };
                if (h.TakeHit(info))
                {
                    HitStop.Freeze(0.02f);
                    Destroy(gameObject);
                    return true;
                }
            }
            return false;
        }
    }
}
