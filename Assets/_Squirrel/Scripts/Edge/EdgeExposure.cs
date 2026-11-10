using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.Edge
{
    /// <summary>
    /// Вешается на белку. За Краем тают зёрна здоровья (общие с боем, см. SquirrelHealth).
    /// Внутри зоны подтаявшее зерно восстанавливается, целые — нет.
    /// Когда зёрна кончились за Краем — «провал глубже в сон» (обрабатывает Respawner).
    /// </summary>
    public class EdgeExposure : MonoBehaviour
    {
        [SerializeField] EdgeZone zone;
        [SerializeField] SquirrelHealth health;

        float regenTimer;
        float graceTimer;

        public bool IsOutside { get; private set; }
        /// <summary>Расстояние до Края: + внутри, − снаружи.</summary>
        public float DistanceToEdge { get; private set; }
        public bool InGrace => graceTimer > 0f;
        public EdgeZone Zone => zone;

        void Awake()
        {
            if (health == null) health = GetComponent<SquirrelHealth>();
            if (zone == null) zone = FindAnyObjectByType<EdgeZone>();
        }

        void Update()
        {
            if (zone == null || health == null) return;
            var s = zone.Settings;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            graceTimer -= dt;

            DistanceToEdge = zone.SignedDistanceInside(transform.position);
            IsOutside = zone.Stage != EdgeStage.Stabilized && DistanceToEdge < 0f;

            if (IsOutside && graceTimer <= 0f)
            {
                health.Drain(s.drainPerSecond * dt);
                regenTimer = s.regenDelay;
            }
            else
            {
                regenTimer -= dt;
                if (regenTimer <= 0f) health.RegenPartial(s.regenPerSecond * dt);
            }
        }

        /// <summary>Неуязвимость к Краю (после возврата в гнездо).</summary>
        public void GiveGrace(float seconds)
        {
            graceTimer = Mathf.Max(graceTimer, seconds);
            IsOutside = false;
        }
    }
}
