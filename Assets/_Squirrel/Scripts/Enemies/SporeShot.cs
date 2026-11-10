using System.Collections.Generic;
using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.Enemies
{
    /// <summary>Спора плевуна: летит прямо, ранит белку, гаснет о стены. Её можно сбить хвостом или жёлудем.</summary>
    public class SporeShot : Hittable
    {
        static readonly List<SporeShot> live = new List<SporeShot>();

        Vector2 velocity;
        float life = 4f;
        SquirrelHealth target;
        SquirrelMotor targetMotor;

        public override bool GivesSap => false;

        public static SporeShot Spawn(Vector2 position, Vector2 velocity, Sprite sprite, Material material)
        {
            var go = new GameObject("Spore");
            go.transform.position = position;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.color = new Color(0.85f, 1f, 0.45f);
            sr.sortingOrder = 105;
            var s = go.AddComponent<SporeShot>();
            s.hitSize = new Vector2(0.4f, 0.4f);
            s.hitOffset = Vector2.zero;
            s.velocity = velocity;
            return s;
        }

        public static void ClearAll()
        {
            foreach (var s in live.ToArray())
                if (s != null) Destroy(s.gameObject);
            live.Clear();
        }

        void Awake()
        {
            target = FindAnyObjectByType<SquirrelHealth>();
            if (target != null) targetMotor = target.GetComponent<SquirrelMotor>();
            live.Add(this);
        }

        void OnDestroy() => live.Remove(this);

        public override bool TakeHit(in HitInfo hit)
        {
            Destroy(gameObject);
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            life -= dt;
            Vector2 step = velocity * dt;
            float len = step.magnitude;
            if (life <= 0f || (len > 0f && KinematicBox.Raycast(transform.position, step / len, len + 0.15f, out _)))
            {
                Destroy(gameObject);
                return;
            }
            transform.position += (Vector3)step;
            transform.Rotate(0f, 0f, 200f * dt);

            if (target != null && targetMotor != null && !target.IsDead && HitRect.Overlaps(targetMotor.WorldRect))
            {
                target.Damage(1, transform.position);
                Destroy(gameObject);
            }
        }
    }
}
