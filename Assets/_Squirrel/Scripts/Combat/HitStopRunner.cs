using UnityEngine;

namespace Squirrel.Combat
{
    /// <summary>Считает стоп-кадр по реальному времени и возвращает timeScale.</summary>
    public class HitStopRunner : MonoBehaviour
    {
        void Update()
        {
            if (HitStop.Remaining <= 0f) return;
            HitStop.Remaining -= Time.unscaledDeltaTime;
            if (HitStop.Remaining <= 0f)
            {
                HitStop.Remaining = 0f;
                Time.timeScale = 1f;
            }
        }

        void OnDestroy()
        {
            if (HitStop.Remaining > 0f) { HitStop.Remaining = 0f; Time.timeScale = 1f; }
        }
    }
}
