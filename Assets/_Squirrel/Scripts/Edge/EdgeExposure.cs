using System;
using UnityEngine;
using Squirrel.World;

namespace Squirrel.Edge
{
    /// <summary>
    /// Вешается на белку. За Краем тают «зёрна» здоровья. Когда они кончились —
    /// «провал глубже в сон»: возврат в последнее гнездо, Край откатывается на фазу, растёт глубина сна.
    /// </summary>
    public class EdgeExposure : MonoBehaviour
    {
        [SerializeField] EdgeZone zone;
        [SerializeField] SquirrelController squirrel;
        [Tooltip("Точка возврата, если гнёзд ещё не было")]
        [SerializeField] Transform fallbackSpawn;

        float regenTimer;
        float graceTimer;
        Vector3 startPosition;

        public float Health { get; private set; }
        public float MaxHealth => zone != null ? zone.Settings.maxHealth : 1f;
        public bool IsOutside { get; private set; }
        /// <summary>Скрытый счётчик «глубины сна» (в игре повлияет на сюжет).</summary>
        public int SleepDepth { get; private set; }
        /// <summary>Расстояние до Края: + внутри, − снаружи.</summary>
        public float DistanceToEdge { get; private set; }

        public event Action<int> FellDeeper;

        void Awake()
        {
            if (squirrel == null) squirrel = GetComponent<SquirrelController>();
            if (zone == null) zone = FindFirstObjectByType<EdgeZone>();
            startPosition = transform.position;
        }

        void Start() => Health = MaxHealth;

        void Update()
        {
            if (zone == null) return;
            var s = zone.Settings;
            float dt = Time.deltaTime;
            graceTimer -= dt;

            DistanceToEdge = zone.SignedDistanceInside(transform.position);
            IsOutside = zone.Stage != EdgeStage.Stabilized && DistanceToEdge < 0f;

            if (IsOutside && graceTimer <= 0f)
            {
                Health -= s.drainPerSecond * dt;
                regenTimer = s.regenDelay;
                if (Health <= 0f) FallDeeper();
            }
            else
            {
                regenTimer -= dt;
                if (regenTimer <= 0f) Health = Mathf.Min(MaxHealth, Health + s.regenPerSecond * dt);
            }
        }

        void FallDeeper()
        {
            SleepDepth++;
            zone.RollBackOnePhase();
            Vector3 target = Nest.LastVisited != null ? Nest.LastVisited.SpawnPoint
                           : fallbackSpawn != null ? fallbackSpawn.position
                           : startPosition;
            squirrel.Teleport(target);
            Health = MaxHealth;
            graceTimer = zone.Settings.respawnGrace;
            FellDeeper?.Invoke(SleepDepth);
            Debug.Log($"Белка провалилась глубже в сон. Глубина: {SleepDepth}");
        }

        public bool InGrace => graceTimer > 0f;
    }
}
