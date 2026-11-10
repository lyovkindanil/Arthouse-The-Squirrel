using UnityEngine;
using Squirrel.Edge;

namespace Squirrel.World
{
    /// <summary>Сердце локации. Белка дотронулась — Край замирает, локация стабилизируется.</summary>
    public class Heart : MonoBehaviour
    {
        [SerializeField] float radius = 1.2f;
        [SerializeField] Transform squirrel;
        [SerializeField] EdgeZone zone;
        [SerializeField] Transform visual;

        public bool Restored { get; private set; }

        void Awake()
        {
            if (squirrel == null)
            {
                var c = FindAnyObjectByType<SquirrelController>();
                if (c != null) squirrel = c.transform;
            }
            if (zone == null) zone = FindAnyObjectByType<EdgeZone>();
        }

        void Update()
        {
            if (visual != null)
            {
                float speed = Restored ? 1.2f : 3f + (zone != null ? zone.Phase : 0) * 1.5f; // бьётся чаще к концу
                float beat = 1f + 0.12f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * speed)), 6f);
                visual.localScale = Vector3.one * beat;
                visual.Rotate(0f, 0f, (Restored ? 20f : 60f) * Time.deltaTime);
            }

            if (Restored || squirrel == null || zone == null) return;
            if (Vector2.Distance(squirrel.position, transform.position) <= radius)
            {
                Restored = true;
                zone.Stabilize();
                Debug.Log("Сердце восстановлено: Край замер.");
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.5f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
