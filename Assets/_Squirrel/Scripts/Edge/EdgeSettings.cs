using UnityEngine;

namespace Squirrel.Edge
{
    /// <summary>
    /// Настройки Края для одной локации. Время в прототипе — в секундах, чтобы проверять быстро.
    /// В настоящей локации паузы будут минутами (план: 6 → 4 → 2,5 → 1,5 мин).
    /// </summary>
    [CreateAssetMenu(menuName = "Squirrel/Edge Settings", fileName = "EdgeSettings")]
    public class EdgeSettings : ScriptableObject
    {
        [Header("Размер")]
        [Tooltip("Радиус безопасной зоны в начале, ед")]
        public float startRadius = 62f;
        [Tooltip("Радиус после последней фазы, ед")]
        public float finalRadius = 4f;

        [Header("Фазы")]
        [Range(1, 8)] public int phaseCount = 4;
        [Tooltip("Пауза первой фазы (Край стоит), с")]
        public float firstHoldTime = 40f;
        [Tooltip("Длительность первого сдвига (Край ползёт), с")]
        public float firstShrinkTime = 12f;
        [Tooltip("Каждая следующая фаза короче в столько раз: 0.65 → 40, 26, 17, 11 с")]
        [Range(0.2f, 1f)] public float escalation = 0.65f;
        [Tooltip("Насколько быстрее ползёт Край в каждой следующей фазе (1 = так же)")]
        [Range(0.2f, 1f)] public float shrinkEscalation = 0.65f;
        [Tooltip("За сколько секунд до сдвига начинается предупреждение")]
        public float warningTime = 5f;

        [Header("Финал")]
        [Tooltip("Если игрок не дошёл до Сердца после последней фазы, Край схлопывается до нуля")]
        public bool collapseAfterLastPhase = true;
        public float collapseHoldTime = 8f;
        public float collapseTime = 10f;

        [Header("Урон за Краем (число зёрен — в CombatSettings)")]
        [Tooltip("Сколько зёрен в секунду тает за Краем")]
        public float drainPerSecond = 1.5f;
        [Tooltip("Восстановление подтаявшего зерна внутри зоны, зёрен в секунду (целые зёрна так не вернуть)")]
        public float regenPerSecond = 0.5f;
        [Tooltip("Через сколько секунд внутри зоны начинается восстановление")]
        public float regenDelay = 2f;
        [Tooltip("Неуязвимость к Краю после возврата в гнездо, с")]
        public float respawnGrace = 2f;

        public float HoldTime(int phase) => firstHoldTime * Mathf.Pow(escalation, phase);
        public float ShrinkTime(int phase) => firstShrinkTime * Mathf.Pow(shrinkEscalation, phase);

        /// <summary>Радиус, на котором Край стоит в паузе фазы phase (0..phaseCount).</summary>
        public float RadiusAt(int phase)
        {
            float t = phaseCount <= 0 ? 1f : Mathf.Clamp01((float)phase / phaseCount);
            return Mathf.Lerp(startRadius, finalRadius, t);
        }
    }
}
