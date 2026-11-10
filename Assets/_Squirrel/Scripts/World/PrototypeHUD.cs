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
        GUIStyle big, small, hintStyle, arrowStyle;
        float outsideFlash, hurtFlash;
        // Очаг Края, который сейчас подступает к белке.
        float frontNear;          // 0..1, насколько близко
        Vector2 frontDir;         // куда смотреть (в мире)
        static Texture2D vignette;
        global::Squirrel.Enemies.RootWarden boss;

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
            boss = FindAnyObjectByType<global::Squirrel.Enemies.RootWarden>();
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
            UpdateFrontWarning();
        }

        void UpdateFrontWarning()
        {
            float target = 0f;
            if (zone != null && squirrel != null && zone.HasFronts && zone.Stage != EdgeStage.Stabilized)
            {
                Vector2 p = (Vector2)squirrel.transform.position + Vector2.up * 0.35f;
                int i = zone.NearestFront(p, out float margin);
                if (i >= 0 && zone.IsFrontPressing(i))
                {
                    target = 1f - Mathf.Clamp01(margin / zone.Settings.frontAwareness);
                    // Направление на очаг — против роста расстояния до него (по проходам).
                    var f = zone.Field;
                    const float e = 0.75f;
                    Vector2 g = new Vector2(
                        f.FrontDistance(i, p + Vector2.right * e) - f.FrontDistance(i, p - Vector2.right * e),
                        f.FrontDistance(i, p + Vector2.up * e) - f.FrontDistance(i, p - Vector2.up * e));
                    if (g.sqrMagnitude > 1e-4f) frontDir = Vector2.Lerp(frontDir, -g.normalized, Time.unscaledDeltaTime * 6f);
                }
            }
            frontNear = Mathf.MoveTowards(frontNear, target, Time.unscaledDeltaTime * 1.5f);
        }

        static Texture2D Vignette()
        {
            if (vignette != null) return vignette;
            const int n = 64;
            vignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.75f);
                vignette.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
            }
            vignette.Apply();
            return vignette;
        }

        void DrawHints()
        {
            var cam = Camera.main;
            if (cam == null || squirrel == null) return;
            Vector2 from = squirrel.transform.position;
            foreach (var h in WorldHint.All)
            {
                float a = h.Visibility(from);
                if (a <= 0f || string.IsNullOrEmpty(h.Text)) continue;
                Vector3 sp = cam.WorldToScreenPoint(h.WorldPoint);
                if (sp.z < 0f) continue;
                var rect = new Rect(sp.x - 170, Screen.height - sp.y - 46, 340, 46);
                GUI.color = new Color(0f, 0f, 0f, 0.55f * a);
                var size = hintStyle.CalcHeight(new GUIContent(h.Text), 340);
                GUI.DrawTexture(new Rect(rect.x + 10, rect.yMax - size - 4, 320, size + 6), Texture2D.whiteTexture);
                var c = h.Color; c.a *= a;
                GUI.color = c;
                GUI.Label(new Rect(rect.x, rect.yMax - size - 1, 340, size), h.Text, hintStyle);
            }
            GUI.color = Color.white;
        }

        void DrawBossBar()
        {
            if (boss == null || !boss.IsFighting) return;
            float w = Screen.width * 0.5f, x = (Screen.width - w) * 0.5f, y = Screen.height - 58;
            GUI.Label(new Rect(x, y - 30, w, 28), boss.Phase == 1 ? "Корневой сторож" : "Корневой сторож — ярость", hintStyle);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, w, 14), Texture2D.whiteTexture);
            float k = Mathf.Clamp01((float)boss.Hp / boss.MaxHp);
            GUI.color = boss.IsExposed ? new Color(1f, 0.75f, 0.3f) : new Color(0.75f, 0.25f, 0.2f);
            GUI.DrawTexture(new Rect(x + 2, y + 2, (w - 4) * k, 10), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawFrontWarning()
        {
            if (frontNear <= 0.01f) return;
            // Тёплая виньетка: чем ближе очаг, тем гуще.
            GUI.color = new Color(1f, 0.45f, 0.15f, frontNear * 0.45f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Vignette());
            var cam = Camera.main;
            if (cam != null && squirrel != null && frontDir.sqrMagnitude > 0.01f)
            {
                Vector3 sp = cam.WorldToScreenPoint(squirrel.transform.position + Vector3.up * 0.5f);
                Vector2 c = new Vector2(sp.x, Screen.height - sp.y);
                Vector2 d = new Vector2(frontDir.x, -frontDir.y).normalized;
                Vector2 at = c + d * 95f;
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f);
                GUI.color = new Color(1f, 0.6f, 0.25f, Mathf.Clamp01(frontNear * 1.6f) * pulse);
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(angle, at);
                GUI.Label(new Rect(at.x - 30, at.y - 30, 60, 60), "►", arrowStyle);
                GUI.matrix = m;
                GUI.color = new Color(1f, 0.7f, 0.4f, Mathf.Clamp01(frontNear * 2f));
                GUI.Label(new Rect(c.x - 150, c.y + 40, 300, 30), "Край подступает", hintStyle);
            }
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
                small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
                center = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                corner = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperRight };
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                arrowStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            }

            DrawHints();
            DrawFrontWarning();
            DrawBossBar();

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
