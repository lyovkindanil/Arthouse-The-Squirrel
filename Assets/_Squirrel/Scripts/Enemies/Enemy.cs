using System.Collections.Generic;
using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.Enemies
{
    /// <summary>
    /// Основа врага: очки здоровья, вспышка при попадании, отбрасывание, урон касанием.
    /// Мёртвый враг не выключается, а прячется: так его легко вернуть, когда белка возвращается в гнездо после смерти.
    /// </summary>
    public abstract class Enemy : Hittable
    {
        static readonly List<Enemy> registry = new List<Enemy>();

        [SerializeField] protected int maxHp = 3;
        [Tooltip("Сколько зёрен снимает касание. 0 — безопасен")]
        [SerializeField] protected int contactDamage = 1;
        [Tooltip("Скорость отбрасывания от удара белки")]
        [SerializeField] protected float knockbackSpeed = 7f;
        [SerializeField] float flashTime = 0.1f;

        protected SquirrelHealth target;
        protected SquirrelMotor targetMotor;
        protected Vector2 knock;

        SpriteRenderer[] renderers;
        Color[] baseColors;
        float flashTimer;
        Vector3 spawnPosition;

        public int Hp { get; protected set; }
        public int MaxHp => maxHp;
        public bool IsDead => Hp <= 0;
        public static IReadOnlyList<Enemy> All => registry;

        public override bool CanBeHit => base.CanBeHit && !IsDead;

        protected virtual void Awake()
        {
            spawnPosition = transform.position;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
            Hp = maxHp;
            target = FindAnyObjectByType<SquirrelHealth>();
            if (target != null) targetMotor = target.GetComponent<SquirrelMotor>();
            registry.Add(this);
        }

        protected virtual void OnDestroy() => registry.Remove(this);

        /// <summary>Вернуть всех врагов на места (белка погибла и вернулась в гнездо).</summary>
        public static void ResetAll()
        {
            foreach (var e in registry.ToArray())
                if (e != null) e.ResetEnemy();
        }

        public virtual void ResetEnemy()
        {
            transform.position = spawnPosition;
            Hp = maxHp;
            knock = Vector2.zero;
            flashTimer = 0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = true;
                renderers[i].color = baseColors[i];
            }
            OnReset();
        }

        protected virtual void OnReset() { }

        public override bool TakeHit(in HitInfo hit)
        {
            if (IsDead) return false;
            Hp -= hit.damage;
            flashTimer = flashTime;
            knock = KnockbackFor(hit) * knockbackSpeed;
            OnHurt(hit);
            if (Hp <= 0) Die();
            return true;
        }

        /// <summary>Куда отбросить (по умолчанию — по направлению удара).</summary>
        protected virtual Vector2 KnockbackFor(in HitInfo hit) => hit.direction;

        protected virtual void OnHurt(in HitInfo hit) { }

        protected virtual void Die()
        {
            Hp = 0;
            foreach (var r in renderers) r.enabled = false;
            OnDied();
        }

        protected virtual void OnDied() { }

        protected virtual void Update()
        {
            if (IsDead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                float k = Mathf.Clamp01(flashTimer / flashTime);
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].color = Color.Lerp(baseColors[i], Color.white, k);
            }

            Tick(dt);
            if (IsDead) return;
            TouchDamage();
        }

        protected abstract void Tick(float dt);

        void TouchDamage()
        {
            if (contactDamage <= 0 || target == null || targetMotor == null || target.IsDead) return;
            if (HitRect.Overlaps(targetMotor.WorldRect))
                target.Damage(contactDamage, HitCenter);
        }

        protected Vector2 TargetCenter => targetMotor != null ? targetMotor.WorldCenter : (Vector2)transform.position;
        protected bool HasTarget => target != null && targetMotor != null && !target.IsDead;

        /// <summary>Сдвинуть тело с учётом стен. Позиция объекта — у «ног» (HitCenter - hitOffset).</summary>
        protected void MoveBody(Vector2 delta, out bool hitX, out bool hitY)
        {
            Vector2 c = KinematicBox.Move(HitCenter, hitSize, delta, out hitX, out hitY);
            transform.position = new Vector3(c.x - hitOffset.x, c.y - hitOffset.y, transform.position.z);
        }
    }
}
