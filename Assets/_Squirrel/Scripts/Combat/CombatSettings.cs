using UnityEngine;

namespace Squirrel.Combat
{
    /// <summary>
    /// Все числа боя: зёрна здоровья, сок для лечения, удар хвостом, жёлуди, «сочность».
    /// ScriptableObject: правки в Play Mode сохраняются.
    /// </summary>
    [CreateAssetMenu(menuName = "Squirrel/Combat Settings", fileName = "CombatSettings")]
    public class CombatSettings : ScriptableObject
    {
        [Header("Зёрна (здоровье)")]
        [Min(1)] public int maxSeeds = 5;
        [Tooltip("Неуязвимость после удара, с")]
        public float invulnerableTime = 1f;
        [Tooltip("Отбрасывание белки при ударе: X — от врага, Y — вверх")]
        public Vector2 hurtKnockback = new Vector2(8f, 7f);
        [Tooltip("Сколько после удара управление по X заблокировано, с")]
        public float hurtInputLock = 0.2f;

        [Header("Сок (лечение)")]
        [Tooltip("Сколько сока помещается")]
        [Min(1)] public int maxSap = 9;
        [Tooltip("Сок за каждый удар хвостом по врагу")]
        public int sapPerHit = 1;
        [Tooltip("Сколько сока стоит одно зерно")]
        public int healCost = 3;
        [Tooltip("Сколько держать кнопку лечения (стоя на земле), с")]
        public float healTime = 0.9f;

        [Header("Удар хвостом")]
        public int tailDamage = 1;
        [Tooltip("Перезарядка удара, с")]
        public float tailCooldown = 0.3f;
        [Tooltip("Сколько удар бьёт после нажатия, с")]
        public float tailActiveTime = 0.1f;
        [Tooltip("Буфер нажатия удара, с")]
        public float tailBuffer = 0.12f;
        [Tooltip("Зона удара вбок: ширина × высота")]
        public Vector2 tailSideSize = new Vector2(1.7f, 1.0f);
        [Tooltip("Насколько центр зоны удара вбок сдвинут от центра белки")]
        public float tailSideReach = 0.95f;
        [Tooltip("Зона удара вверх/вниз: ширина × высота")]
        public Vector2 tailVerticalSize = new Vector2(1.3f, 1.5f);
        public float tailVerticalReach = 1.0f;
        [Tooltip("Скорость отскока вверх после удара вниз по врагу")]
        public float pogoVelocity = 13f;
        [Tooltip("Отдача назад после удара вбок по врагу")]
        public float sideRecoil = 5f;
        public float recoilLock = 0.08f;

        [Header("Жёлудь")]
        public int acornDamage = 1;
        [Min(0)] public int maxAcorns = 3;
        [Tooltip("Один жёлудь восстанавливается за столько секунд")]
        public float acornRecharge = 2.5f;
        public float acornCooldown = 0.35f;
        public float acornSpeed = 22f;
        [Tooltip("Лёгкая дуга полёта")]
        public float acornGravity = 4f;
        public float acornLifetime = 1.2f;

        [Header("Сочность")]
        [Tooltip("Стоп-кадр, когда белка попала по врагу, с")]
        public float hitStopOnHit = 0.04f;
        [Tooltip("Стоп-кадр, когда ударили белку, с")]
        public float hitStopOnHurt = 0.1f;
        public float shakeOnHit = 0.12f;
        public float shakeOnHurt = 0.35f;
    }
}
