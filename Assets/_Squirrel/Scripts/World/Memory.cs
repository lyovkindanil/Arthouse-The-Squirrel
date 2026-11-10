using System;
using UnityEngine;
using Squirrel.Edge;

namespace Squirrel.World
{
    /// <summary>
    /// Воспоминание о реальном мире. Белка подбирает его — Край отступает на pushBack единиц.
    /// Награда за исследование: чем больше собрано, тем больше времени и мира остаётся.
    /// </summary>
    public class Memory : MonoBehaviour
    {
        [SerializeField] float radius = 0.9f;
        [Tooltip("На сколько единиц отступает Край")]
        [SerializeField] float pushBack = 5f;
        [SerializeField] Transform squirrel;
        [SerializeField] EdgeZone zone;
        [SerializeField] Transform visual;

        Vector3 visualBase;

        public bool Collected { get; private set; }
        public float PushBackAmount => pushBack;
        public static event Action<Memory> AnyCollected;

        void Awake()
        {
            if (squirrel == null)
            {
                var c = FindAnyObjectByType<SquirrelController>();
                if (c != null) squirrel = c.transform;
            }
            if (zone == null) zone = FindAnyObjectByType<EdgeZone>();
            if (visual != null) visualBase = visual.localPosition;
        }

        void Update()
        {
            if (Collected) return;
            if (visual != null)
            {
                visual.localPosition = visualBase + Vector3.up * Mathf.Sin(Time.time * 2.2f + transform.position.x) * 0.12f;
                visual.Rotate(0f, 0f, 45f * Time.deltaTime);
            }
            if (squirrel == null) return;
            // Белка — от ног до головы: считаем от середины тела.
            Vector2 body = (Vector2)squirrel.position + Vector2.up * 0.35f;
            if (Vector2.Distance(body, transform.position) <= radius) Collect();
        }

        void Collect()
        {
            Collected = true;
            if (zone != null) zone.PushBack(pushBack);
            if (visual != null) visual.gameObject.SetActive(false);
            AnyCollected?.Invoke(this);
            Debug.Log($"Воспоминание: Край отступил на {pushBack}.");
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
