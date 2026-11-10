using System;
using UnityEngine;
using Squirrel.Combat;
using Squirrel.Edge;
using Squirrel.Enemies;

namespace Squirrel.World
{
    /// <summary>
    /// Что происходит, когда зёрна кончились.
    /// В бою — возврат в последнее гнездо, враги возвращаются на места.
    /// За Краем — то же самое плюс «провал глубже в сон»: Край откатывается на фазу, растёт скрытая глубина сна.
    /// </summary>
    [RequireComponent(typeof(SquirrelHealth), typeof(SquirrelController))]
    public class Respawner : MonoBehaviour
    {
        [SerializeField] EdgeZone zone;
        [Tooltip("Точка возврата, если гнёзд ещё не было")]
        [SerializeField] Transform fallbackSpawn;
        [Tooltip("Неуязвимость после возврата, если в сцене нет Края, с")]
        [SerializeField] float defaultGrace = 2f;

        SquirrelHealth health;
        SquirrelController controller;
        EdgeExposure exposure;
        AcornThrower thrower;
        Vector3 startPosition;

        /// <summary>Скрытый счётчик «глубины сна» (в игре повлияет на сюжет).</summary>
        public int SleepDepth { get; private set; }
        public int Deaths { get; private set; }

        public event Action<int> FellDeeper;
        public event Action<DeathCause> Respawned;

        void Awake()
        {
            health = GetComponent<SquirrelHealth>();
            controller = GetComponent<SquirrelController>();
            exposure = GetComponent<EdgeExposure>();
            thrower = GetComponent<AcornThrower>();
            if (zone == null) zone = FindAnyObjectByType<EdgeZone>();
            startPosition = transform.position;
            health.Died += OnDied;
        }

        /// <summary>Загрузка сохранения.</summary>
        public void RestoreCounters(int sleepDepth, int deaths)
        {
            SleepDepth = Mathf.Max(0, sleepDepth);
            Deaths = Mathf.Max(0, deaths);
        }

        void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        void OnDied(DeathCause cause)
        {
            Deaths++;
            if (cause == DeathCause.Edge && zone != null)
            {
                SleepDepth++;
                zone.RollBackOnePhase();
                FellDeeper?.Invoke(SleepDepth);
                Debug.Log($"Белка провалилась глубже в сон. Глубина: {SleepDepth}");
            }

            Vector3 target = Nest.LastVisited != null ? Nest.LastVisited.SpawnPoint
                           : fallbackSpawn != null ? fallbackSpawn.position
                           : startPosition;
            controller.Teleport(target);
            health.Restore();
            float grace = zone != null ? zone.Settings.respawnGrace : defaultGrace;
            health.GrantInvulnerability(grace);
            if (exposure != null) exposure.GiveGrace(grace);
            if (thrower != null) thrower.Refill();
            Enemy.ResetAll();
            SporeShot.ClearAll();
            Respawned?.Invoke(cause);
        }
    }
}
