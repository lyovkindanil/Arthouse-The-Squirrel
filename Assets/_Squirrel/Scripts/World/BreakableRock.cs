using System;
using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.World
{
    /// <summary>Камень, который можно разбить хвостом или жёлудем. Твёрдый, пока цел.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class BreakableRock : Hittable
    {
        [SerializeField] int hits = 3;
        [SerializeField] SpriteRenderer sprite;

        int left;
        Color baseColor;
        float shake;
        Vector3 basePos;

        public bool Broken { get; private set; }
        public bool Unbreakable { get; set; }
        public int HitsLeft => left;
        public event Action BrokenEvent;

        public override bool GivesSap => false;
        public override bool CanBeHit => base.CanBeHit && !Broken;

        void Awake()
        {
            left = hits;
            if (sprite != null) { baseColor = sprite.color; basePos = sprite.transform.localPosition; }
        }

        public override bool TakeHit(in HitInfo hit)
        {
            if (Broken || Unbreakable) return false;
            left -= Mathf.Max(1, hit.damage);
            shake = 0.15f;
            if (sprite != null) sprite.color = Color.Lerp(baseColor, Color.black, 0.3f * (hits - left) / hits);
            if (left <= 0) Break();
            return true;
        }

        /// <summary>Разбить без удара (загрузка сохранения).</summary>
        public void BreakSilently()
        {
            Broken = true;
            GetComponent<BoxCollider2D>().enabled = false;
            if (sprite != null) sprite.enabled = false;
        }

        void Break()
        {
            BreakSilently();
            BrokenEvent?.Invoke();
        }

        void Update()
        {
            if (sprite == null || shake <= 0f) return;
            shake -= Time.deltaTime;
            sprite.transform.localPosition = basePos + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.06f * (shake > 0f ? 1f : 0f));
        }
    }
}
