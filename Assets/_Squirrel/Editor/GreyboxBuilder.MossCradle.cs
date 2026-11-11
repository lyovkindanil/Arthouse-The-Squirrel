using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Squirrel;
using Squirrel.Edge;
using Squirrel.World;

namespace SquirrelEditor
{
    /// <summary>
    /// Мшистая колыбель: сцена собирается из текстовой карты Assets/_Squirrel/Levels/MossCradle.txt.
    /// Правишь символы в файле — жмёшь «Squirrel → Собрать Мшистую колыбель» — получаешь новую сцену.
    /// Соседние клетки одного типа сливаются в один блок, поэтому объектов немного.
    /// </summary>
    public static partial class GreyboxBuilder
    {
        const string MossCradleMap = Root + "/Levels/MossCradle.txt";
        const string MossCradlePath = Root + "/Scenes/MossCradle.unity";

        static readonly Color Stone = new Color(0.33f, 0.36f, 0.38f);
        static readonly Color Silk = new Color(0.93f, 0.93f, 0.86f);
        static readonly Color RoomBackground = new Color(0.10f, 0.15f, 0.13f);

        [MenuItem("Squirrel/Собрать Мшистую колыбель")]
        public static void BuildMossCradle()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildMossCradleScene();
        }

        struct RoomDef { public string id, title; public int x, y, w, h; }

        static void BuildMossCradleScene()
        {
            Prepare();
            var edgeSettings = LoadOrCreate<EdgeSettings>(SettingsDir + "/EdgeSettings_MossCradle.asset", e =>
            {
                // План: паузы 6 → 4 → 2,5 → 1,5 мин. Для первых проб — 7 → 4,5 → 3 → 2 мин, крути здесь.
                e.startRadius = 105f;
                e.finalRadius = 8f;
                e.phaseCount = 4;
                e.firstHoldTime = 420f;
                e.firstShrinkTime = 25f;
                e.escalation = 0.65f;
                e.shrinkEscalation = 0.75f;
                e.warningTime = 8f;
                e.collapseHoldTime = 20f;
                e.collapseTime = 30f;
            });

            ParseMap(File.ReadAllLines(MossCradleMap), out var rooms, out var cells, out int width, out int height);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene выгружает неиспользуемые ассеты — берём настройки заново, уже с диска.
            edgeSettings = AssetDatabase.LoadAssetAtPath<EdgeSettings>(SettingsDir + "/EdgeSettings_MossCradle.asset");

            // --- Геометрия ---
            var level = new GameObject("Level").transform;
            foreach (var r in Merge(cells, width, height, '#')) Block(level, $"Stone {r.x},{r.y}", r.x + r.width * 0.5f, r.y + r.height * 0.5f, r.width, r.height, Stone);
            foreach (var r in Merge(cells, width, height, 'B')) Block(level, $"Bark {r.x},{r.y}", r.x + r.width * 0.5f, r.y + r.height * 0.5f, r.width, r.height, Bark);

            // --- Комнаты ---
            var roomRoot = new GameObject("Rooms").transform;
            foreach (var rd in rooms)
            {
                var go = new GameObject($"{rd.id} {rd.title}");
                go.transform.SetParent(roomRoot, false);
                go.transform.position = new Vector3(rd.x, rd.y);
                var room = go.AddComponent<Room>();
                room.Setup(rd.id, rd.title, new Vector2(rd.w, rd.h));
                // Фон комнаты, чтобы глаз отличал пустоту от камня.
                var bg = MakeSprite(go.transform, "Background", new Vector2(rd.w * 0.5f, rd.h * 0.5f), new Vector2(rd.w, rd.h), RoomBackground, -50);
                bg.name = "Background";
            }

            // --- Сердце и Край ---
            Vector2Int heartCell = Find(cells, width, height, 'H');
            var heartGo = new GameObject("Heart");
            heartGo.transform.position = new Vector3(heartCell.x + 0.5f, heartCell.y + 0.5f);
            var heartVisual = MakeSprite(heartGo.transform, "Visual", Vector2.zero, new Vector2(0.9f, 0.9f), new Color(1f, 0.35f, 0.55f), 20);
            heartVisual.transform.localRotation = Quaternion.Euler(0, 0, 45);
            // Кольцо и свечение вокруг Сердца бьются вместе с ним — его видно издалека.
            MakeShape(heartVisual.transform, "Glow", glow, Vector2.zero, new Vector2(4.5f, 4.5f), new Color(1f, 0.4f, 0.6f, 0.4f), 18);
            MakeShape(heartVisual.transform, "Ring", ring, Vector2.zero, new Vector2(2.2f, 2.2f), new Color(1f, 0.6f, 0.75f, 0.8f), 19);
            var heart = heartGo.AddComponent<Heart>();
            AddHint(heartGo, "Сердце колыбели: коснись — Край остановится", new Vector2(0f, 1.6f), 6f, new Color(1f, 0.75f, 0.85f));

            var zoneGo = new GameObject("Edge (Край)");
            var zone = zoneGo.AddComponent<EdgeZone>();
            Set(zone, "settings", edgeSettings);
            Set(zone, "center", heartGo.transform);
            // Край меряет расстояние по проходам карты и растёт ещё и из очагов (символ * на карте).
            var field = zoneGo.AddComponent<EdgeField>();
            field.Setup(AssetDatabase.LoadAssetAtPath<TextAsset>(MossCradleMap), heartGo.transform);
            EditorUtility.SetDirty(field);
            field.Build();
            Set(zone, "field", field);
            Set(zoneGo.AddComponent<EdgeFieldVisual>(), "material", unlit);
            // Общее кольцо стартует чуть дальше самой дальней точки (по пути), чтобы в начале весь мир был внутри.
            float startRadius = Mathf.Ceil(field.MaxHeartDistance + 6f);
            if (!Mathf.Approximately(edgeSettings.startRadius, startRadius))
            {
                edgeSettings.startRadius = startRadius;
                EditorUtility.SetDirty(edgeSettings);
                AssetDatabase.SaveAssets();
            }
            for (int i = 0; i < field.FrontCount; i++) MakeFrontMarker(i, field.Sources[i]);
            Set(heart, "zone", zone);
            Set(heart, "visual", heartVisual.transform);

            // --- Белка ---
            Vector2Int start = Find(cells, width, height, 'S');
            var rig = BuildSquirrel(new Vector3(start.x + 0.5f, start.y + 0.05f), zone, allAbilities: false);
            Set(heart, "squirrel", rig.root);

            // --- Состояние мира и сохранение ---
            var game = new GameObject("Game (WorldState + Save)");
            game.AddComponent<WorldState>();
            var session = game.AddComponent<GameSession>();
            Set(session, "zone", zone);

            // --- Объекты по символам ---
            var things = new GameObject("Things").transform;
            int nests = 0, memories = 0, enemies = 0;
            Caterpillar caterpillar = null;
            BreakableRock rock = null;
            StashPile stash = null;
            Transform enemiesRoot = new GameObject("Enemies").transform;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                char c = cells[x, y];
                Vector3 feet = new Vector3(x + 0.5f, y);
                Vector3 mid = new Vector3(x + 0.5f, y + 0.5f);
                string where = RoomIdAt(rooms, x, y);
                switch (c)
                {
                    case 'N':
                        MakeNest($"Nest {++nests} ({where})", feet, rig.root, zone).transform.SetParent(things, true);
                        break;
                    case 'M':
                        MakeMemory($"Memory {++memories} ({where})", mid, rig.root, zone).transform.SetParent(things, true);
                        break;
                    case 'c': MakeCrawler($"Crawler {++enemies} ({where})", feet, enemies % 2 == 0 ? 1 : -1).SetParent(enemiesRoot, true); break;
                    case 'm': MakeMoth($"Moth {++enemies} ({where})", mid).SetParent(enemiesRoot, true); break;
                    case 'p': MakeSpitter($"Spitter {++enemies} ({where})", feet).SetParent(enemiesRoot, true); break;
                    case 'A': MakeAbilityShrine(mid, things); break;
                    case 'C': caterpillar = MakeCaterpillar(feet, things); break;
                    case 'K': stash = MakeStash(feet, things); break;
                    case 'x': MakeFlagged("Dried Cocoon (if robbed)", Caterpillar.RobbedFlag, things, root =>
                    {
                        var s = MakeSprite(root, "Cocoon", new Vector2(0, 0.35f), new Vector2(0.5f, 0.7f), new Color(0.5f, 0.48f, 0.42f), 25);
                        s.transform.localRotation = Quaternion.Euler(0, 0, 70);
                    }, feet); break;
                }
            }

            // Камень гусеницы — один объект на все клетки 'R'.
            foreach (var r in Merge(cells, width, height, 'R'))
                rock = MakeRock(r, things);

            // Шёлковый мост — только если гусеницу освободили. На нём сидит она сама.
            var bridgeRects = Merge(cells, width, height, '=');
            if (bridgeRects.Count > 0)
            {
                MakeFlagged("Silk Bridge (if helped)", Caterpillar.HelpedFlag, things, root =>
                {
                    foreach (var r in bridgeRects)
                        Block(root, $"Silk {r.x},{r.y}", r.x + r.width * 0.5f, r.y + r.height * 0.5f, r.width, r.height, Silk);
                    var first = bridgeRects[0];
                    var cat = MakeSprite(root, "Caterpillar (weaving)", Vector2.zero, new Vector2(0.9f, 0.4f), new Color(0.55f, 0.8f, 0.3f), 26);
                    cat.transform.position = new Vector3(first.x + first.width + 0.5f, first.y + first.height + 0.2f);
                }, Vector3.zero);
            }

            // --- Босс: Корневой сторож в зале Сердца (R12) ---
            var warden = MakeRootWarden(rooms, enemiesRoot);
            if (warden != null) Set(heart, "guard", warden);

            if (caterpillar != null)
            {
                Set(caterpillar, "rock", rock);
                Set(caterpillar, "stash", stash);
            }

            BuildCameraAndHud(new Vector3(start.x, start.y + 2, -10), rig, zone);
            var cam = Object.FindAnyObjectByType<Camera>();
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.09f);

            EditorSceneManager.SaveScene(scene, MossCradlePath);
            AddSceneToBuild(MossCradlePath, false);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(MossCradleMap);
            Debug.Log($"Мшистая колыбель собрана: {MossCradlePath}. Комнат {rooms.Count}, гнёзд {nests}, воспоминаний {memories}, врагов {enemies}.");
        }

        static void MakeAbilityShrine(Vector3 pos, Transform parent)
        {
            var go = new GameObject("Ability Shrine (wall jump)");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            // Каменный постамент + светящийся зелёный знак над ним.
            MakeSprite(go.transform, "Pedestal", new Vector2(0, -0.35f), new Vector2(1f, 0.3f), new Color(0.5f, 0.55f, 0.52f), 28);
            var v = MakeSprite(go.transform, "Visual", Vector2.zero, new Vector2(0.7f, 0.7f), new Color(0.5f, 1f, 0.6f), 30);
            v.transform.localRotation = Quaternion.Euler(0, 0, 45);
            MakeShape(v.transform, "Glow", glow, Vector2.zero, new Vector2(4f, 4f), new Color(0.5f, 1f, 0.6f, 0.4f), 29);
            var a = go.AddComponent<AbilityPickup>();
            Set(a, "visual", v.transform);
            AddHint(v.gameObject, "Святилище: научишься отталкиваться от каменных стен", new Vector2(0f, 1.1f), 4.5f, new Color(0.75f, 1f, 0.8f));
        }

        /// <summary>
        /// Корневой сторож просыпается у левой стены зала Сердца и бродит по полу. Арена — весь зал, ворота — в проходе справа.
        /// </summary>
        static global::Squirrel.Enemies.RootWarden MakeRootWarden(List<RoomDef> rooms, Transform parent)
        {
            var r12 = rooms.Find(r => r.id == "R12");
            if (r12.id == null) return null;
            float floor = r12.y + 2;                     // пол зала (ноги белки)
            var arena = new Rect(r12.x + 1, floor, r12.w - 4, r12.h - 4); // правый край на шаг от ворот

            var root = new GameObject("Boss: Root Warden (Корневой сторож)");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(r12.x + 3f, floor);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var trunk = new Color(0.32f, 0.22f, 0.16f);
            MakeSprite(visual, "Trunk", new Vector2(0, 3.5f), new Vector2(4f, 7f), trunk, 55);
            MakeShape(visual, "Crown", circle, new Vector2(0.3f, 7.2f), new Vector2(5f, 2.6f), new Color(0.25f, 0.4f, 0.22f), 54);
            for (int i = 0; i < 4; i++)
            {
                var rootSpr = MakeSprite(visual, $"Root {i + 1}", new Vector2(1.6f + i * 0.5f, 0.3f + i * 0.9f), new Vector2(1.6f, 0.35f), trunk * 0.85f, 56);
                rootSpr.transform.localRotation = Quaternion.Euler(0, 0, -25f + i * 18f);
            }
            MakeShape(visual, "Eye L", circle, new Vector2(-0.7f, 5.4f), new Vector2(0.45f, 0.3f), DangerRed, 58);
            MakeShape(visual, "Eye R", circle, new Vector2(0.9f, 5.4f), new Vector2(0.45f, 0.3f), DangerRed, 58);
            MakeShape(visual, "Core Glow", glow, new Vector2(0.2f, 3.2f), new Vector2(3.2f, 3.2f), new Color(1f, 0.7f, 0.3f, 0.25f), 56);
            var core = MakeShape(visual, "Core", circle, new Vector2(0.2f, 3.2f), new Vector2(1.4f, 1.4f), new Color(0.3f, 0.2f, 0.15f), 57);

            // Ворота: корни перекрывают выход, пока идёт бой.
            var gate = new GameObject("Boss Gate");
            gate.transform.SetParent(parent, false); // ворота стоят на месте, сторож ходит
            gate.transform.position = new Vector3(r12.x + r12.w - 1f, floor + 1.5f);
            var gsr = gate.AddComponent<SpriteRenderer>();
            gsr.sprite = white; gsr.sharedMaterial = unlit; gsr.sortingOrder = 40;
            gsr.color = new Color(0.4f, 0.27f, 0.18f);
            gate.transform.localScale = new Vector3(2f, 3f, 1f);
            gate.AddComponent<BoxCollider2D>();
            for (int i = 0; i < 3; i++)
            {
                var bar = MakeShape(gate.transform, $"Thorn {i + 1}", triangle, new Vector2(-0.5f, -0.3f + i * 0.3f), new Vector2(0.2f, 0.1f), DangerRed, 41);
                bar.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }

            var w = root.AddComponent<global::Squirrel.Enemies.RootWarden>();
            SetVector(w, "hitSize", new Vector2(4f, 7f));
            SetVector(w, "hitOffset", new Vector2(0f, 3.5f));
            SetInt(w, "maxHp", 24);
            SetInt(w, "contactDamage", 1);
            SetFloat(w, "floorY", floor);
            var so = new SerializedObject(w);
            so.FindProperty("arena").rectValue = arena;
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(w, "gate", gate);
            Set(w, "core", core);
            Set(w, "sprite", white);
            Set(w, "material", unlit);
            AddHint(root, "Корневой сторож. Бей, когда сердцевина светится", new Vector2(2f, 8.6f), 14f, HintDanger);
            return w;
        }

        /// <summary>Очаг Края: тёмно-оранжевое кольцо, чтобы было видно, откуда ползёт.</summary>
        static void MakeFrontMarker(int index, Vector2 pos)
        {
            var go = new GameObject($"Edge Front {index + 1}");
            go.transform.position = pos;
            MakeShape(go.transform, "Glow", glow, Vector2.zero, new Vector2(5f, 5f), new Color(1f, 0.45f, 0.15f, 0.35f), 105);
            MakeShape(go.transform, "Ring", ring, Vector2.zero, new Vector2(1.6f, 1.6f), new Color(1f, 0.5f, 0.2f, 0.9f), 106);
            MakeShape(go.transform, "Core", circle, Vector2.zero, new Vector2(0.7f, 0.7f), new Color(0.15f, 0.05f, 0.05f, 1f), 107);
            AddHint(go, "Очаг Края: пока ты рядом, Край ползёт отсюда", new Vector2(0f, 1.6f), 9f, HintDanger);
        }

        static Caterpillar MakeCaterpillar(Vector3 feet, Transform parent)
        {
            var go = new GameObject("Caterpillar");
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            var body = MakeSprite(go.transform, "Body", new Vector2(0, 0.25f), new Vector2(1.1f, 0.5f), new Color(0.55f, 0.8f, 0.3f), 26);
            MakeSprite(go.transform, "Eye", new Vector2(0.4f, 0.35f), new Vector2(0.1f, 0.1f), Color.black, 27);
            var c = go.AddComponent<Caterpillar>();
            Set(c, "body", body);
            AddHint(go, "Гусеница замурована камнем", new Vector2(0f, 1.1f), 4f, HintWarm);
            return c;
        }

        static StashPile MakeStash(Vector3 feet, Transform parent)
        {
            var go = new GameObject("Caterpillar Stash");
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            var v = new GameObject("Visual").transform;
            v.SetParent(go.transform, false);
            MakeSprite(v, "Leaves", new Vector2(0, 0.2f), new Vector2(0.9f, 0.4f), new Color(0.4f, 0.65f, 0.25f), 24);
            MakeShape(v, "Berry", circle, new Vector2(0.15f, 0.45f), new Vector2(0.28f, 0.28f), new Color(0.8f, 0.2f, 0.35f), 25);
            MakeShape(v, "Berry 2", circle, new Vector2(-0.15f, 0.42f), new Vector2(0.22f, 0.22f), new Color(0.7f, 0.18f, 0.3f), 25);
            AddHint(v.gameObject, "Запас гусеницы. Ударить — забрать себе (+1 зерно)", new Vector2(0f, 1.1f), 3.5f, HintWarm);
            var s = go.AddComponent<StashPile>();
            SetVector(s, "hitSize", new Vector2(0.9f, 0.7f));
            SetVector(s, "hitOffset", new Vector2(0, 0.35f));
            Set(s, "visual", v.gameObject);
            return s;
        }

        static BreakableRock MakeRock(RectInt r, Transform parent)
        {
            var go = new GameObject("Rock (walls in the caterpillar)");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(r.width, r.height);
            // Светлее обычного камня и в трещинах: видно, что его можно разбить.
            var sr = MakeSprite(go.transform, "Visual", Vector2.zero, new Vector2(r.width, r.height), new Color(0.62f, 0.56f, 0.48f), 10);
            var crackColor = new Color(0.2f, 0.17f, 0.15f);
            var c1 = MakeSprite(go.transform, "Crack 1", new Vector2(-0.1f, 0.15f), new Vector2(0.08f, r.height * 0.7f), crackColor, 11);
            c1.transform.localRotation = Quaternion.Euler(0, 0, 18);
            var c2 = MakeSprite(go.transform, "Crack 2", new Vector2(0.15f, -0.3f), new Vector2(0.07f, r.height * 0.45f), crackColor, 11);
            c2.transform.localRotation = Quaternion.Euler(0, 0, -30);
            var c3 = MakeSprite(go.transform, "Crack 3", new Vector2(-0.2f, -0.45f), new Vector2(0.06f, r.height * 0.3f), crackColor, 11);
            c3.transform.localRotation = Quaternion.Euler(0, 0, 55);
            var rock = go.AddComponent<BreakableRock>();
            AddHint(go, "Трещины: бей хвостом (X), чтобы разбить", new Vector2(0f, r.height * 0.5f + 0.9f), 4f, HintCold);
            SetVector(rock, "hitSize", new Vector2(r.width + 0.1f, r.height));
            SetVector(rock, "hitOffset", Vector2.zero);
            Set(rock, "sprite", sr);
            return rock;
        }

        static void MakeFlagged(string name, string flag, Transform parent, System.Action<Transform> fill, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var content = new GameObject("Content").transform;
            content.SetParent(go.transform, false);
            content.position = pos;
            fill(content);
            var sw = go.AddComponent<FlagSwitch>();
            sw.Setup(flag, true, content.gameObject);
            EditorUtility.SetDirty(sw);
        }

        // ---------- Разбор карты ----------

        static void ParseMap(string[] lines, out List<RoomDef> rooms, out char[,] cells, out int width, out int height)
        {
            rooms = new List<RoomDef>();
            var mapLines = new List<string>();
            bool inMap = false;
            foreach (var raw in lines)
            {
                string line = raw.TrimEnd('\r');
                if (inMap) { if (line.Length > 0) mapLines.Add(line); continue; }
                if (line.StartsWith("#") || line.Trim().Length == 0) continue;
                if (line.Trim() == "map") { inMap = true; continue; }
                if (line.StartsWith("room "))
                {
                    var p = line.Split(new[] { ' ' }, 7);
                    rooms.Add(new RoomDef
                    {
                        id = p[1], x = int.Parse(p[2]), y = int.Parse(p[3]), w = int.Parse(p[4]), h = int.Parse(p[5]),
                        title = p.Length > 6 ? p[6] : p[1]
                    });
                }
            }
            height = mapLines.Count;
            width = 0;
            foreach (var l in mapLines) width = Mathf.Max(width, l.Length);
            cells = new char[width, height];
            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row; // верхняя строка файла — верх уровня
                for (int x = 0; x < width; x++)
                    cells[x, y] = x < mapLines[row].Length ? mapLines[row][x] : '#';
            }
        }

        static Vector2Int Find(char[,] cells, int width, int height, char c)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (cells[x, y] == c) return new Vector2Int(x, y);
            throw new System.Exception($"На карте нет символа '{c}'");
        }

        static string RoomIdAt(List<RoomDef> rooms, int x, int y)
        {
            foreach (var r in rooms)
                if (x >= r.x && x < r.x + r.w && y >= r.y && y < r.y + r.h) return r.id;
            return "?";
        }

        /// <summary>Жадное слияние клеток одного символа в прямоугольники.</summary>
        static List<RectInt> Merge(char[,] cells, int width, int height, char c)
        {
            var used = new bool[width, height];
            var result = new List<RectInt>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (used[x, y] || cells[x, y] != c) continue;
                    int w = 1;
                    while (x + w < width && cells[x + w, y] == c && !used[x + w, y]) w++;
                    int h = 1;
                    while (y + h < height && RowMatches(cells, used, x, y + h, w, c)) h++;
                    for (int yy = y; yy < y + h; yy++)
                        for (int xx = x; xx < x + w; xx++)
                            used[xx, yy] = true;
                    result.Add(new RectInt(x, y, w, h));
                }
            }
            return result;
        }

        static bool RowMatches(char[,] cells, bool[,] used, int x, int y, int w, char c)
        {
            for (int i = 0; i < w; i++)
                if (cells[x + i, y] != c || used[x + i, y]) return false;
            return true;
        }
    }
}
