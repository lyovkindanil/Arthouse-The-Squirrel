using UnityEngine;

namespace Squirrel.World
{
    /// <summary>Простая камера для серой комнаты. В срезе заменим на Cinemachine.</summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] SquirrelController target;
        [SerializeField] Vector2 offset = new Vector2(0f, 1.5f);
        [SerializeField] float lookAhead = 2f;
        [SerializeField] float smoothTime = 0.18f;
        [SerializeField] float lookAheadSmoothTime = 0.5f;

        Vector3 vel;
        float currentLook, lookVel;

        void Awake()
        {
            if (target == null) target = FindAnyObjectByType<SquirrelController>();
        }

        void LateUpdate()
        {
            if (target == null) return;
            currentLook = Mathf.SmoothDamp(currentLook, target.Facing * lookAhead, ref lookVel, lookAheadSmoothTime);
            Vector3 goal = target.transform.position + (Vector3)offset + Vector3.right * currentLook;
            goal.z = transform.position.z;
            // Если белку перенесло далеко (возврат в гнездо) — прыгаем сразу.
            if (Vector2.Distance(goal, transform.position) > 25f) { transform.position = goal; vel = Vector3.zero; return; }
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref vel, smoothTime);
        }
    }
}
