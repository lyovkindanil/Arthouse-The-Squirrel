using System;
using UnityEngine;

namespace Squirrel.Combat
{
    public enum DeathCause { Combat, Edge }

    /// <summary>
    /// Зёрна здоровья белки — общие для боя и Края. Удары врагов снимают целые зёрна,
    /// Край «растапливает» их понемногу. Внутри зоны подтаявшее зерно восстанавливается, но целые — нет:
    /// их возвращает только лечение соком или отдых в гнезде.
    /// Сок копится от ударов хвостом по врагам; лечение — держать кнопку, стоя на земле.
    /// </summary>
    [RequireComponent(typeof(SquirrelController), typeof(SquirrelInput))]
    public class SquirrelHealth : MonoBehaviour
    {
        [SerializeField] CombatSettings settings;
        [Tooltip("Спрайты белки: мигают во время неуязвимости")]
        [SerializeField] Transform visual;

        SquirrelController controller;
        SquirrelInput input;
        SpriteRenderer[] renderers;
        float invulnTimer, healTimer;

        public CombatSettings Settings => settings;
        public float Current { get; private set; }
        public int Max => settings.maxSeeds + BonusSeeds;
        /// <summary>Лишние зёрна сверх базовых (например, взятый запас гусеницы).</summary>
        public int BonusSeeds { get; private set; }
        public int Sap { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => invulnTimer > 0f;
        public bool IsHealing { get; private set; }
        public float HealProgress => IsHealing ? Mathf.Clamp01(healTimer / settings.healTime) : 0f;

        /// <summary>Белку ударили (сколько зёрен сняли).</summary>
        public event Action<int> Damaged;
        public event Action Healed;
        public event Action<DeathCause> Died;

        void Awake()
        {
            controller = GetComponent<SquirrelController>();
            input = GetComponent<SquirrelInput>();
            if (settings == null)
            {
                Debug.LogWarning("SquirrelHealth: не назначены CombatSettings, беру значения по умолчанию.");
                settings = ScriptableObject.CreateInstance<CombatSettings>();
            }
            if (visual != null) renderers = visual.GetComponentsInChildren<SpriteRenderer>();
            Current = Max;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            invulnTimer -= dt;
            UpdateHeal(dt);
            UpdateBlink();
        }

        void UpdateHeal(float dt)
        {
            bool want = input.HealHeld && !IsDead
                        && Sap >= settings.healCost
                        && Current < Max - 0.001f
                        && controller.State == SquirrelState.Grounded;
            if (!want) { CancelHeal(); return; }

            if (!IsHealing)
            {
                IsHealing = true;
                healTimer = 0f;
                controller.Rooted = true;
            }
            healTimer += dt;
            if (healTimer >= settings.healTime)
            {
                Sap -= settings.healCost;
                Current = Mathf.Min(Max, Mathf.Floor(Current + 0.001f) + 1f);
                healTimer = 0f;
                Healed?.Invoke();
            }
        }

        void UpdateBlink()
        {
            if (renderers == null) return;
            bool show = invulnTimer <= 0f || Mathf.Repeat(Time.time, 0.12f) < 0.07f;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = show;
        }

        /// <summary>Прервать лечение (удар, бросок, урон).</summary>
        public void CancelHeal()
        {
            if (!IsHealing) return;
            IsHealing = false;
            healTimer = 0f;
            controller.Rooted = false;
        }

        /// <summary>Удар врага: снимает целые зёрна, отбрасывает, даёт неуязвимость.</summary>
        public bool Damage(int amount, Vector2 from)
        {
            if (IsDead || invulnTimer > 0f || amount <= 0) return false;
            Current = Mathf.Max(0f, Current - amount);
            CancelHeal();
            invulnTimer = settings.invulnerableTime;

            float dir = transform.position.x >= from.x ? 1f : -1f;
            controller.Knockback(new Vector2(dir * settings.hurtKnockback.x, settings.hurtKnockback.y), settings.hurtInputLock);
            HitStop.Freeze(settings.hitStopOnHurt);
            CameraShake.Add(settings.shakeOnHurt, 0.3f);
            Damaged?.Invoke(amount);

            if (Current <= 0.001f) Die(DeathCause.Combat);
            return true;
        }

        /// <summary>Край растапливает зёрна. Неуязвимость от ударов тут не спасает.</summary>
        public void Drain(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current -= amount;
            if (Current <= 0.001f) Die(DeathCause.Edge);
        }

        /// <summary>Восстановить подтаявшее зерно, но не больше ближайшего целого.</summary>
        public void RegenPartial(float amount)
        {
            if (IsDead || amount <= 0f) return;
            float cap = Mathf.Min(Max, Mathf.Ceil(Current - 0.001f));
            if (Current < cap) Current = Mathf.Min(cap, Current + amount);
        }

        public void AddMaxSeeds(int amount)
        {
            BonusSeeds = Mathf.Max(0, BonusSeeds + amount);
            Current = Mathf.Clamp(Current + amount, 0.01f, Max);
        }

        public void SetBonusSeeds(int amount)
        {
            BonusSeeds = Mathf.Max(0, amount);
            Current = Mathf.Min(Current, Max);
        }

        public void AddSap(int amount) => Sap = Mathf.Clamp(Sap + amount, 0, settings.maxSap);

        /// <summary>Полное здоровье (гнездо, возврат после смерти).</summary>
        public void Restore()
        {
            IsDead = false;
            Current = Max;
            CancelHeal();
        }

        public void GrantInvulnerability(float seconds) => invulnTimer = Mathf.Max(invulnTimer, seconds);

        /// <summary>Отладка и тесты.</summary>
        public void DebugSet(float current, int sap)
        {
            IsDead = false;
            Current = Mathf.Clamp(current, 0.01f, Max);
            Sap = Mathf.Clamp(sap, 0, settings.maxSap);
        }

        void Die(DeathCause cause)
        {
            if (IsDead) return;
            IsDead = true;
            Current = 0f;
            CancelHeal();
            invulnTimer = 0f;
            Died?.Invoke(cause);
        }
    }
}
