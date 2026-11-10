using UnityEngine;

namespace Squirrel.World
{
    /// <summary>Короткое сообщение по центру экрана (HUD рисует его сам). В срезе заменим на UI Toolkit.</summary>
    public static class Toast
    {
        public static string Text { get; private set; }
        static float until;

        public static bool Visible => !string.IsNullOrEmpty(Text) && Time.unscaledTime < until;
        /// <summary>0..1, для плавного исчезновения.</summary>
        public static float Alpha => Visible ? Mathf.Clamp01((until - Time.unscaledTime) / 0.5f) : 0f;

        public static void Show(string text, float seconds = 3f)
        {
            Text = text;
            until = Time.unscaledTime + seconds;
        }
    }
}
