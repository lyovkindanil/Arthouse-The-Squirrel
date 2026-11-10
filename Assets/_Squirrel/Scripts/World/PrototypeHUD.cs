using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Squirrel.Combat;
using Squirrel.Edge;

namespace Squirrel.World
{
    /// <summary>
    /// Отладочный HUD на OnGUI (без настройки Canvas). В срезе заменим на UI Toolkit.
    /// Клавиши: R — перезапуск, F1 — перемотать стадию Края, F2 — вкл/выкл планирование,
    /// F3 — вкл/выкл отталкивание от стен, F4 — полный сок, F8 — стереть сохранение и начать заново, H — скрыть подсказки.
    /// </summary>
    public class PrototypeHUD : MonoBehaviour
    {
        [SerializeField] EdgeZone zone;
        [SerializeField] EdgeExposure exposure;
        [SerializeField] SquirrelController squirrel;
        [SerializeField] SquirrelHealth health;
        [SerializeField] AcornThrower thrower;
        [SerializeField] Respawner respawner;
        SquirrelAbilities abilities;
        RoomTracker rooms;
        GameSession session;
        GUIStyle center, corner;
        float roomFlash;

        bool showHelp = true;
        GUIStyle big, small;
        float outsideFlash, hurtFlash;

        void Awake()
        {
            if (zone == null) zone = FindAnyObjectByType<EdgeZone>();
            if (exposure == null) exposure = FindAnyObjectByType<EdgeExposure>();
            if (squirrel == null) squirrel = FindAnyObjectByType<SquirrelController>();
            if (health == null && squirrel != null) health = squirrel.GetComponent<SquirrelHealth>();
            if (thrower == null && squirrel != null) thrower = squirrel.GetComponent<AcornThrower>();
            if (respawner == null && squirrel != null) respawner = squirrel.GetComponent<Respawner>();
            if (squirrel != null) abilities = squirrel.GetComponent<SquirrelAbilities>();
            if (squirrel != null) rooms = squirrel.GetComponent<RoomTracker>();
            if (rooms != null) rooms.Entered += _ => roomFlash = 2.5f;
            session = FindAnyObjectByType<GameSession>();
            if (health != null) health.Damaged += _ => hurtFlash = 1f;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
                ? SceneManager.GetActiveScene().buildIndex : 0);
            if (kb.f1Key.wasPressedThisFrame && zone != null) zone.DebugSkipStage();
            if (kb.f2Key.wasPressedThisFrame && abilities != null) abilities.Set(Ability.Glide, !abilities.Glide);
            if (kb.f3Key.wasPressedThisFrame && abilities != null) abilities.Set(Ability.WallJump, !abilities.WallJump);
            if (kb.f4Key.wasPressedThisFrame && health != null) health.AddSap(health.Settings.maxSap);
            if (kb.f8Key.wasPressedThisFrame && session != null) session.WipeAndRestart();
            if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
            roomFlash -= Time.unscaledDeltaTime;

            bool outside = exposure != null && exposure.IsOutside;
            outsideFlash = Mathf.MoveTowards(outsideFlash, outside ? 1f : 0f, Time.deltaTime * 3f);
            hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, Time.unscaledDeltaTime * 2.5f);
        }

        void OnGUI()
        {
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
                small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
                center = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                corner = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperRight };
            }

            if (Toast.Visible)
            {
                GUI.color = new Color(1f, 0.95f, 0.8f, Toast.Alpha);
                GUI.Label(new Rect(Screen.width * 0.2f, Screen.height * 0.62f, Screen.width * 0.6f, 80), Toast.Text, center);
                GUI.color = Color.white;
            }

            if (rooms != null && rooms.Current != null)
            {
                float a = roomFlash > 0f ? 1f : 0.55f;
                GUI.color = new Color(1f, 1f, 1f, a);
                string axes = "";
                var w = WorldState.Current;
                if (w != null && showHelp) axes = $"\nСон {w.Dream:+0;-0;0} · Сострадание {w.Compassion:+0;-0;0}";
                GUI.Label(new Rect(Screen.width - 520, 12, 504, 80), rooms.Current.Title + axes, corner);
                GUI.color = Color.white;
            }

            // Красная виньетка, когда белка за Краем или её ударили.
            float red = Mathf.Max(outsideFlash * 0.35f, hurtFlash * 0.3f);
            if (red > 0f)
            {
                GUI.color = new Color(0.6f, 0f, 0.1f, red);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            GUILayout.BeginArea(new Rect(16, 12, 900, 400));
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

            if (health != null)
            {
                int whole = Mathf.FloorToInt(health.Current + 0.001f);
                bool partial = health.Current - whole > 0.01f;
                string seeds = new string('●', whole) + (partial ? "◐" : "")
                             + new string('○', Mathf.Max(0, health.Max - whole - (partial ? 1 : 0)));
                string sap = new string('▮', health.Sap) + new string('▯', Mathf.Max(0, health.Settings.maxSap - health.Sap));
                string acorns = thrower != null ? new string('◆', thrower.Ammo) + new string('◇', Mathf.Max(0, thrower.MaxAmmo - thrower.Ammo)) : "";
                GUI.color = health.IsInvulnerable ? new Color(1f, 0.7f, 0.7f) : Color.white;
                GUILayout.Label($"Зёрна: {seeds}   Сок: {sap}   Жёлуди: {acorns}", big);
                GUI.color = Color.white;
                string extra = health.IsHealing ? $"   Лечение… {health.HealProgress * 100f:0}%" : "";
                string edge = exposure != null ? $"До Края: {exposure.DistanceToEdge:0.0}   " : "";
                string depth = respawner != null ? $"Глубина сна: {respawner.SleepDepth}   Смертей: {respawner.Deaths}" : "";
                string pushed = zone != null && zone.PushedBack > 0f ? $"   Край оттолкнут на {zone.PushedBack:0}" : "";
                GUILayout.Label(edge + depth + pushed + extra, small);
            }

            if (squirrel != null && showHelp)
            {
                bool glide = abilities != null && abilities.Glide, wj = abilities != null && abilities.WallJump;
                GUILayout.Space(6);
                GUILayout.Label($"Состояние: {squirrel.State}{(squirrel.IsGliding ? " (планирует)" : "")}   v = {squirrel.Velocity.x:0.0}, {squirrel.Velocity.y:0.0}", small);
                GUILayout.Label("A/D, стрелки, стик — бег · Пробел/Z/A — прыжок (держать — выше)", small);
                GUILayout.Label("К стене в воздухе — зацепиться · W/S на стене — лезть/скользить · прыжок на стене — оттолкнуться", small);
                GUILayout.Label("X/J/□ — удар хвостом (+W — вверх, в воздухе +S — вниз с отскоком) · C/K/R1 — жёлудь · держать V/L/△ — лечение (3 сока)", small);
                GUILayout.Label($"R — заново · F1 — перемотать стадию Края · F2 — планирование [{(glide ? "вкл" : "выкл")}] · F3 — отталкивание [{(wj ? "вкл" : "выкл")}] · F4 — полный сок{(session != null ? " · F8 — стереть сохранение" : "")} · H — скрыть", small);
            }
            GUILayout.EndArea();
        }
    }
}
