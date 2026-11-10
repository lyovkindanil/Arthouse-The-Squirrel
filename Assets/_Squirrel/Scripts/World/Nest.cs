using UnityEngine;
using Squirrel.Combat;
using Squirrel.Edge;

namespace Squirrel.World
{
    /// <summary>
    /// Гнездо — чекпоинт. Пока белка в гнезде, Край стоит. Последнее посещённое гнездо — точка возврата.
    /// Отдых в гнезде возвращает все зёрна и жёлуди.
    /// Проверка по расстоянию, без физики, чтобы не зависеть от Rigidbody.
    /// </summary>
    public class Nest : MonoBehaviour
    {
        [SerializeField] float radius = 1.4f;
        [SerializeField] Vector2 spawnOffset = new Vector2(0f, 0.1f);
        [SerializeField] Transform squirrel;
        [SerializeField] EdgeZone zone;
        [SerializeField] SpriteRenderer marker;
        [SerializeField] Color idleColor = new Color(0.55f, 0.42f, 0.3f);
        [SerializeField] Color activeColor = new Color(0.95f, 0.8f, 0.45f);

        public static Nest LastVisited { get; private set; }
        public Vector3 SpawnPoint => transform.position + (Vector3)spawnOffset;
        public bool Occupied { get; private set; }

        void Awake()
        {
            if (squirrel == null)
            {
                var c = FindAnyObjectByType<SquirrelController>();
                if (c != null) squirrel = c.transform;
            }
            if (zone == null) zone = FindAnyObjectByType<EdgeZone>();
        }

        void OnDisable()
        {
            if (zone != null) zone.SetPaused(this, false);
            if (LastVisited == this) LastVisited = null;
        }

        void Update()
        {
            if (squirrel == null) return;
            bool inside = Vector2.Distance(squirrel.position, transform.position) <= radius;
            if (inside != Occupied)
            {
                Occupied = inside;
                if (zone != null) zone.SetPaused(this, inside);
                if (inside)
                {
                    LastVisited = this;
                    var health = squirrel.GetComponent<SquirrelHealth>();
                    if (health != null && !health.IsDead) health.Restore();
                    var thrower = squirrel.GetComponent<AcornThrower>();
                    if (thrower != null) thrower.Refill();
                }
            }
            if (marker != null)
                marker.color = LastVisited == this ? activeColor : idleColor;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
