using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Squirrel.Edge;

namespace Squirrel.World
{
    /// <summary>
    /// Отладочный HUD на OnGUI (без настройки Canvas). В срезе заменим на UI Toolkit.
    /// Клавиши: R — перезапуск, F1 — перемотать стадию Края, F2 — вкл/выкл планирование,
    /// F3 — вкл/выкл отталкивание от стен, H — скрыть подсказки.
    /// </summary>
    public class PrototypeHUD : MonoBehaviour
    {
        [SerializeField] EdgeZone zone;
        [SerializeField] EdgeExposure exposure;
        [SerializeField] SquirrelController squirrel;

        bool showHelp = true;
        GUIStyle big, small;
        float outsideFlash;

        void Awake()
        {
            if (zone == null) zone = FindAnyObjectByType<EdgeZone>();
            if (exposure == null) exposure = FindAnyObjectByType<EdgeExposure>();
            if (squirrel == null) squirrel = FindAnyObjectByType<SquirrelController>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
                ? SceneManager.GetActiveScene().buildIndex : 0);
            if (kb.f1Key.wasPressedThisFrame && zone != null) zone.DebugSkipStage();
            if (kb.f2Key.wasPressedThisFrame && squirrel != null) squirrel.Settings.glideUnlocked = !squirrel.Settings.glideUnlocked;
            if (kb.f3Key.wasPressedThisFrame && squirrel != null) squirrel.Settings.wallJumpUnlocked = !squirrel.Settings.wallJumpUnlocked;
            if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;

            bool outside = exposure != null && exposure.IsOutside;
            outsideFlash = Mathf.MoveTowards(outsideFlash, outside ? 1f : 0f, Time.deltaTime * 3f);
        }

        void OnGUI()
        {
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
                small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            }

            // Красная виньетка, когда белка за Краем.
            if (outsideFlash > 0f)
            {
                GUI.color = new Color(0.6f, 0f, 0.1f, 0.35f * outsideFlash);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            GUILayout.BeginArea(new Rect(16, 12, 520, 400));
            if (zone != null)
            {
                var s = zone.Settings;
                string stage = zone.Stage switch
                {
                    EdgeStage.Hold when zone.IsPaused => "Край стоит (гнездо)",
                    EdgeStage.Hold when zone.IsWarning => "Край сейчас сдвинется!",
                    EdgeStage.Hold => "Край стоит",
                    EdgeStage.Shrink => "Край ползёт",
                    EdgeStage.Collapse => "Мир схлопывается",
                    EdgeStage.Stabilized => "Сердце восстановлено — Край замер",
                    _ => ""
                };
                string phase = zone.Stage == EdgeStage.Stabilized ? ""
                    : $"Фаза {Mathf.Min(zone.Phase + 1, s.phaseCount)}/{s.phaseCount}   ";
                string timer = zone.Stage == EdgeStage.Stabilized || float.IsInfinity(zone.StageTimeLeft) ? ""
                    : $"   {Mathf.Max(0f, zone.StageTimeLeft):0.0} с";
                GUI.color = zone.IsWarning || zone.Stage == EdgeStage.Collapse ? new Color(1f, 0.45f, 0.45f) : Color.white;
                GUILayout.Label(phase + stage + timer, big);
                GUI.color = Color.white;
            }

            if (exposure != null)
            {
                int full = Mathf.CeilToInt(exposure.Health - 0.001f);
                string seeds = new string('●', Mathf.Max(0, full)) + new string('○', Mathf.Max(0, Mathf.RoundToInt(exposure.MaxHealth) - full));
                GUILayout.Label($"Зёрна: {seeds}   До Края: {exposure.DistanceToEdge:0.0}   Глубина сна: {exposure.SleepDepth}", small);
            }

            if (squirrel != null && showHelp)
            {
                var st = squirrel.Settings;
                GUILayout.Space(6);
                GUILayout.Label($"Состояние: {squirrel.State}{(squirrel.IsGliding ? " (планирует)" : "")}   v = {squirrel.Velocity.x:0.0}, {squirrel.Velocity.y:0.0}", small);
                GUILayout.Label("A/D, стрелки, стик — бег · Пробел/Z/A — прыжок (держать — выше)", small);
                GUILayout.Label("К стене в воздухе — зацепиться · W/S на стене — лезть/скользить · прыжок на стене — оттолкнуться", small);
                GUILayout.Label($"R — заново · F1 — перемотать стадию Края · F2 — планирование [{(st.glideUnlocked ? "вкл" : "выкл")}] · F3 — отталкивание [{(st.wallJumpUnlocked ? "вкл" : "выкл")}] · H — скрыть", small);
            }
            GUILayout.EndArea();
        }
    }
}
