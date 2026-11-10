using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.Enemies
{
    /// <summary>
    /// Плевун — гриб, который сидит на месте и, видя белку, надувается и плюёт спорой в её сторону.
    /// Учит подходить между выстрелами, сбивать споры хвостом и пользоваться жёлудем.
    /// </summary>
    public class Spitter : Enemy
    {
        [SerializeField] float range = 10f;
        [SerializeField] float fireInterval = 2.2f;
        [SerializeField] float windupTime = 0.5f;
        [SerializeField] float shotSpeed = 7f;
        [SerializeField] Transform visual;
        [SerializeField] Sprite shotSprite;
        [SerializeField] Material shotMaterial;

        float timer;
        bool winding;
        Vector3 visualScale = Vector3.one;

        public int ShotsFired { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (visual != null) visualScale = visual.localScale;
            timer = fireInterval * 0.5f;
        }

        protected override void OnReset()
        {
            timer = fireInterval * 0.5f;
            winding = false;
            if (visual != null) visual.localScale = visualScale;
        }

        // Сидит крепко: не отлетает.
        protected override Vector2 KnockbackFor(in HitInfo hit) => Vector2.zero;

        protected override void Tick(float dt)
        {
            bool sees = SeesTarget();
            if (!winding)
            {
                timer -= dt;
                if (timer <= 0f && sees) { winding = true; timer = windupTime; }
                if (visual != null) visual.localScale = visualScale;
            }
            else
            {
                timer -= dt;
                float k = 1f - Mathf.Clamp01(timer / windupTime);
                if (visual != null) visual.localScale = new Vector3(visualScale.x * (1f + 0.25f * k), visualScale.y * (1f - 0.15f * k), visualScale.z);
                if (timer <= 0f)
                {
                    winding = false;
                    timer = fireInterval;
                    if (sees) Fire();
                }
            }
        }

        void Fire()
        {
            Vector2 from = HitCenter + new Vector2(0f, hitSize.y * 0.3f);
            Vector2 dir = (TargetCenter - from);
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.left;
            SporeShot.Spawn(from, dir * shotSpeed, shotSprite, shotMaterial);
            ShotsFired++;
        }

        bool SeesTarget()
        {
            if (!HasTarget) return false;
            Vector2 t = TargetCenter;
            Vector2 eye = HitCenter + new Vector2(0f, hitSize.y * 0.3f);
            return Vector2.Distance(eye, t) <= range && KinematicBox.LineOfSight(eye, t);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(0.8f, 1f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
