using UnityEngine;

namespace Squirrel.Combat
{
    /// <summary>Стоп-кадр: на долю секунды замораживает игру, чтобы удар «чувствовался».</summary>
    public static class HitStop
    {
        public static bool Enabled = true;
        internal static float Remaining;
        static HitStopRunner runner;

        public static void Freeze(float seconds)
        {
            if (!Enabled || seconds <= 0f || !Application.isPlaying) return;
            if (runner == null)
            {
                var go = new GameObject("HitStop") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<HitStopRunner>();
            }
            Remaining = Mathf.Max(Remaining, seconds);
            Time.timeScale = 0f;
        }
    }

    /// <summary>Тряска камеры. Камера читает Offset каждый кадр.</summary>
    public static class CameraShake
    {
        static float amplitude, time, duration;

        public static void Add(float amount, float seconds = 0.2f)
        {
            if (amount <= 0f) return;
            amplitude = Mathf.Max(amplitude * (time / Mathf.Max(duration, 0.0001f)), amount);
            duration = time = seconds;
        }

        /// <summary>Вызывать раз в кадр. Работает и во время стоп-кадра.</summary>
        public static Vector2 Tick(float unscaledDt)
        {
            if (time <= 0f) return Vector2.zero;
            time -= unscaledDt;
            float k = Mathf.Clamp01(time / duration);
            return Random.insideUnitCircle * amplitude * k * k;
        }
    }
}
