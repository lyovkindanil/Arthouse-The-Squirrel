using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.World
{
    /// <summary>Простая камера для серых комнат, с тряской от ударов. В срезе заменим на Cinemachine.</summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] SquirrelController target;
        [SerializeField] Vector2 offset = new Vector2(0f, 1.5f);
        [SerializeField] float lookAhead = 2f;
        [SerializeField] float smoothTime = 0.18f;
        [SerializeField] float lookAheadSmoothTime = 0.5f;

        Vector3 vel;
        Vector3 smooth;
        float currentLook, lookVel;

        void Awake()
        {
            if (target == null) target = FindAnyObjectByType<SquirrelController>();
            smooth = transform.position;
        }

        void LateUpdate()
        {
            if (target == null) return;
            currentLook = Mathf.SmoothDamp(currentLook, target.Facing * lookAhead, ref lookVel, lookAheadSmoothTime);
            Vector3 goal = target.transform.position + (Vector3)offset + Vector3.right * currentLook;
            goal.z = smooth.z;
            // Если белку перенесло далеко (возврат в гнездо) — прыгаем сразу.
            if (Vector2.Distance(goal, smooth) > 25f) { smooth = goal; vel = Vector3.zero; }
            else smooth = Vector3.SmoothDamp(smooth, goal, ref vel, smoothTime);
            transform.position = smooth + (Vector3)CameraShake.Tick(Time.unscaledDeltaTime);
        }
    }
}
