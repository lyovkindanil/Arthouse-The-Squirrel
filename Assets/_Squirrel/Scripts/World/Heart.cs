using System;
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
        [Tooltip("Страж Сердца: пока он жив, Сердце не восстановить")]
        [SerializeField] global::Squirrel.Enemies.Enemy guard;
        [SerializeField] string lockedHint = "Сердце колыбели. Его стережёт Корневой сторож";
        [SerializeField] string openHint = "Сердце колыбели: коснись — Край остановится";

        WorldHint hint;
        public bool Locked => guard != null && !guard.IsDead;

        public const string RestoredFlag = "heart.restored";

        public bool Restored { get; private set; }
        public static event Action<Heart> AnyRestored;

        void Awake()
        {
            if (squirrel == null)
            {
                var c = FindAnyObjectByType<SquirrelController>();
                if (c != null) squirrel = c.transform;
            }
            if (zone == null) zone = FindAnyObjectByType<EdgeZone>();
            hint = GetComponent<WorldHint>();
        }

        void Start()
        {
            // Сердце уже восстановлено в сохранении: Край восстановлен вместе с ним.
            if (WorldState.HasFlag(RestoredFlag)) Restored = true;
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

            if (hint != null) hint.Text = Restored ? "" : Locked ? lockedHint : openHint;
            if (Restored || Locked || squirrel == null || zone == null) return;
            if (Vector2.Distance(squirrel.position, transform.position) <= radius)
            {
                Restored = true;
                zone.Stabilize();
                WorldState.SetFlag(RestoredFlag);
                Toast.Show("Сердце восстановлено. Край замер.");
                AnyRestored?.Invoke(this);
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
