using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Squirrel;
using Squirrel.Combat;
using Squirrel.Edge;
using Squirrel.Enemies;
using Squirrel.World;

namespace SquirrelEditor
{
    /// <summary>
    /// Меню Squirrel → «Собрать серую комнату» и «Собрать арену боя».
    /// Greybox.unity — комната с Краем, Сердцем, гнёздами и воспоминаниями.
    /// Arena.unity — арена с врагами всех трёх типов и манекеном для отработки ударов.
    /// Повторный запуск пересоздаёт сцены, но не трогает настройки (ScriptableObject'ы).
    /// </summary>
    public static partial class GreyboxBuilder
    {
        const string Root = "Assets/_Squirrel";
        const string Generated = Root + "/Generated";
        const string SettingsDir = Root + "/Settings";
        const string ScenePath = Root + "/Scenes/Greybox.unity";
        const string ArenaPath = Root + "/Scenes/Arena.unity";

        static readonly Color Ground = new Color(0.36f, 0.40f, 0.38f);
        static readonly Color Platform = new Color(0.50f, 0.55f, 0.50f);
        static readonly Color Bark = new Color(0.42f, 0.34f, 0.30f);
        static readonly Color Background = new Color(0.14f, 0.17f, 0.20f);

        static readonly Color Moss = new Color(0.35f, 0.62f, 0.35f);
        static readonly Color MothColor = new Color(0.78f, 0.74f, 0.95f);
        static readonly Color SpitterColor = new Color(0.72f, 0.82f, 0.32f);
        static readonly Color MemoryColor = new Color(0.65f, 0.92f, 1f);

        static Sprite white;
        static Material unlit;
        static SquirrelSettings squirrelSettings;
        static EdgeSettings edgeSettings;
        static CombatSettings combatSettings;

        [MenuItem("Squirrel/Собрать всё (комната, арена, Мшистая колыбель)")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildGreyboxScene();
            BuildArenaScene();
            BuildMossCradleScene();
            EditorSceneManager.OpenScene(MossCradlePath);
        }

        static void Prepare()
        {
            UrpSetup.Run();
            EnsureFolder(Generated);
            EnsureFolder(SettingsDir);
            EnsureFolder(Root + "/Scenes");
            white = MakeWhiteSprite();
            unlit = MakeUnlitMaterial();
            PrepareShapes();
            squirrelSettings = LoadOrCreate<SquirrelSettings>(SettingsDir + "/SquirrelSettings.asset");
            edgeSettings = LoadOrCreate<EdgeSettings>(SettingsDir + "/EdgeSettings.asset");
            combatSettings = LoadOrCreate<CombatSettings>(SettingsDir + "/CombatSettings.asset");
        }

        [MenuItem("Squirrel/Собрать серую комнату")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildGreyboxScene();
        }

        [MenuItem("Squirrel/Собрать арену боя")]
        public static void BuildArena()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildArenaScene();
        }

        static void BuildGreyboxScene()
        {
            Prepare();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Комната ---
            var level = new GameObject("Level").transform;
            Block(level, "Floor", 0, -1, 68, 2, Ground);
            Block(level, "Ceiling", 0, 39, 68, 2, Ground);
            Block(level, "Wall Left", -33, 19, 2, 40, Bark);
            Block(level, "Wall Right", 33, 19, 2, 40, Bark);

            // Нижний маршрут: бег и короткие прыжки.
            Block(level, "Stump 1", -14, 0.75f, 2, 1.5f, Bark);
            Block(level, "Stump 2", 10, 1.25f, 3, 2.5f, Bark);
            Block(level, "Stump 3", 18, 1, 2, 2, Bark);

            // Ступеньки слева и «потолок» для проверки сглаживания углов.
            Block(level, "Platform A", -22, 2, 6, 1, Platform);
            Block(level, "Platform B", -13, 4.5f, 5, 1, Platform);
            Block(level, "Corner Test Overhang", -13.5f, 8.3f, 2.5f, 0.5f, Platform);

            // Ствол для лазания и «дымоход» для отталкиваний.
            Block(level, "Trunk", -5, 8, 2, 16, Bark);
            Block(level, "Chimney Column", -1, 11, 1.5f, 16, Bark);
            Block(level, "Platform C (top of trunk)", 5, 17.5f, 8, 1, Platform);

            // Верхний маршрут к Сердцу.
            Block(level, "Platform D", 14, 20, 4, 1, Platform);
            Block(level, "Platform E", 21, 22.5f, 4, 1, Platform);
            Block(level, "Platform F", 28.5f, 25, 5, 1, Platform);
            Block(level, "Heart Ledge", 21, 27.8f, 6, 1, Platform);

            // Альтернативный путь справа: стена для лазания и полки.
            Block(level, "Climb Wall", 26, 6, 2, 12, Bark);
            Block(level, "Platform H", 29.5f, 15.5f, 3, 1, Platform);
            Block(level, "Platform I", 27, 19.5f, 3, 1, Platform);

            // --- Сердце и Край ---
            var heartGo = new GameObject("Heart");
            heartGo.transform.position = new Vector3(20, 29.3f);
            var heartVisual = MakeSprite(heartGo.transform, "Visual", Vector2.zero, new Vector2(0.9f, 0.9f), new Color(1f, 0.35f, 0.55f), 20);
            heartVisual.transform.localRotation = Quaternion.Euler(0, 0, 45);
            var heart = heartGo.AddComponent<Heart>();

            var zoneGo = new GameObject("Edge (Край)");
            var zone = zoneGo.AddComponent<EdgeZone>();
            Set(zone, "settings", edgeSettings);
            Set(zone, "center", heartGo.transform);
            var visual = zoneGo.AddComponent<EdgeVisual>();
            Set(visual, "material", unlit);
            Set(heart, "zone", zone);
            Set(heart, "visual", heartVisual.transform);

            // --- Белка, гнёзда, воспоминания ---
            var rig = BuildSquirrel(new Vector3(-28, 0.05f), zone);
            Set(heart, "squirrel", rig.root);

            MakeNest("Nest 1 (start)", new Vector3(-28, 0), rig.root, zone);
            MakeNest("Nest 2 (top of trunk)", new Vector3(6, 18), rig.root, zone);

            // Воспоминания — в стороне от основного пути, награда за исследование.
            MakeMemory("Memory 1 (above Platform A)", new Vector3(-22, 3.6f), rig.root, zone);
            MakeMemory("Memory 2 (right shelf)", new Vector3(29.5f, 17f), rig.root, zone);
            MakeMemory("Memory 3 (corner overhang)", new Vector3(-13.5f, 9.5f), rig.root, zone);

            BuildCameraAndHud(new Vector3(-26, 2, -10), rig, zone);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath, true);
            Selection.activeObject = squirrelSettings;
            Debug.Log("Серая комната собрана: " + ScenePath + ". Жми Play. Числа белки — в Settings/SquirrelSettings, Края — в Settings/EdgeSettings, боя — в Settings/CombatSettings.");
        }

        static void BuildArenaScene()
        {
            Prepare();
            var arenaEdge = LoadOrCreate<EdgeSettings>(SettingsDir + "/EdgeSettings_Arena.asset", e =>
            {
                // На арене Край есть, но далеко и медленно: тут учимся драться.
                e.startRadius = 44f;
                e.finalRadius = 14f;
                e.phaseCount = 3;
                e.firstHoldTime = 180f;
                e.firstShrinkTime = 15f;
            });

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Арена 64 × 24 ---
            var level = new GameObject("Level").transform;
            Block(level, "Floor", 0, -1, 68, 2, Ground);
            Block(level, "Ceiling", 0, 23, 68, 2, Ground);
            Block(level, "Wall Left", -33, 11, 2, 24, Bark);
            Block(level, "Wall Right", 33, 11, 2, 24, Bark);
            Block(level, "Platform P1 (crawler)", -2, 4, 10, 1, Platform);
            Block(level, "Platform P2", 10, 8, 6, 1, Platform);
            Block(level, "Platform P3 (spitter)", 20, 4, 6, 1, Platform);
            Block(level, "Platform P4 (memory)", -14, 9, 6, 1, Platform);
            Block(level, "Step", -8, 0.5f, 2, 1, Bark);
            Block(level, "Log (keeps crawler away from nest)", -17, 0.5f, 1, 1, Bark);

            var zoneGo = new GameObject("Edge (Край)");
            zoneGo.transform.position = new Vector3(0, 10);
            var zone = zoneGo.AddComponent<EdgeZone>();
            Set(zone, "settings", arenaEdge);
            Set(zoneGo.AddComponent<EdgeVisual>(), "material", unlit);

            var rig = BuildSquirrel(new Vector3(-28, 0.05f), zone);
            MakeNest("Nest (arena start)", new Vector3(-28, 0), rig.root, zone);

            // --- Враги ---
            MakeDummy("Training Dummy", new Vector3(-20, 0));
            MakeCrawler("Crawler A (floor)", new Vector3(-12, 0), -1);
            MakeCrawler("Crawler B (platform P1)", new Vector3(-2, 4.5f), 1);
            MakeCrawler("Crawler C (floor right)", new Vector3(14, 0), 1);
            MakeMoth("Moth (above P2)", new Vector3(6, 13));
            MakeSpitter("Spitter (on P3)", new Vector3(22, 4.5f));

            MakeMemory("Memory A (on P4)", new Vector3(-14, 10.4f), rig.root, zone);
            MakeMemory("Memory B (far right)", new Vector3(30, 1.2f), rig.root, zone);

            BuildCameraAndHud(new Vector3(-26, 2, -10), rig, zone);

            EditorSceneManager.SaveScene(scene, ArenaPath);
            AddSceneToBuild(ArenaPath, false);
            Selection.activeObject = combatSettings;
            Debug.Log("Арена собрана: " + ArenaPath + ". Числа боя — в Settings/CombatSettings.");
        }

        struct SquirrelRig
        {
            public Transform root;
            public SquirrelController controller;
            public EdgeExposure exposure;
            public SquirrelHealth health;
            public AcornThrower thrower;
            public Respawner respawner;
        }

        static SquirrelRig BuildSquirrel(Vector3 position, EdgeZone zone, bool allAbilities = true)
        {
            var sq = new GameObject("Squirrel");
            sq.transform.position = position;
            var box = sq.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 0.7f);
            box.offset = new Vector2(0, 0.35f);
            box.isTrigger = true;
            sq.AddComponent<SquirrelInput>();
            sq.AddComponent<SquirrelMotor>();
            var abilities = sq.AddComponent<SquirrelAbilities>();
            SetBool(abilities, "startWithWallJump", allAbilities);
            sq.AddComponent<RoomTracker>();
            var controller = sq.AddComponent<SquirrelController>();
            var body = new GameObject("Visual").transform;
            body.SetParent(sq.transform, false);
            // Визуал с пивотом у ног, чтобы сжатие при приземлении шло от земли.
            // Белка из концепт-арта (облик + аниматор); без арта — прежние прямоугольники.
            bool hasArt = BuildSquirrelArt(body, controller);
            if (!hasArt)
            {
                MakeSprite(body, "Body", new Vector2(0, 0.35f), new Vector2(0.9f, 0.7f), new Color(0.92f, 0.52f, 0.22f), 110);
                var tail = MakeSprite(body, "Tail", new Vector2(-0.55f, 0.75f), new Vector2(0.45f, 0.9f), new Color(0.75f, 0.38f, 0.16f), 109);
                tail.transform.localRotation = Quaternion.Euler(0, 0, -15);
                MakeSprite(body, "Ear", new Vector2(0.28f, 0.78f), new Vector2(0.14f, 0.2f), new Color(0.75f, 0.38f, 0.16f), 111);
                MakeSprite(body, "Eye", new Vector2(0.3f, 0.5f), new Vector2(0.12f, 0.12f), new Color(0.08f, 0.06f, 0.05f), 112);
            }
            Set(controller, "settings", squirrelSettings);
            Set(controller, "visual", body);

            var health = sq.AddComponent<SquirrelHealth>();
            Set(health, "settings", combatSettings);
            Set(health, "visual", body);

            var attack = sq.AddComponent<TailAttack>();
            var slash = MakeSprite(sq.transform, "Tail Slash", Vector2.zero, Vector2.one, new Color(1f, 0.95f, 0.7f, 0.45f), 113);
            Set(attack, "slash", slash);
            var spiritAnim = body.GetComponentInChildren<SquirrelSpriteAnimator>();
            if (spiritAnim != null) Set(spiritAnim, "attack", attack);
            var clipAnim = body.GetComponentInChildren<SquirrelClipAnimator>();
            if (clipAnim != null) { Set(clipAnim, "attack", attack); Set(clipAnim, "health", health); }

            var thrower = sq.AddComponent<AcornThrower>();
            Set(thrower, "sprite", white);
            Set(thrower, "material", unlit);

            var exposure = sq.AddComponent<EdgeExposure>();
            Set(exposure, "zone", zone);
            Set(exposure, "health", health);

            var respawner = sq.AddComponent<Respawner>();
            Set(respawner, "zone", zone);

            return new SquirrelRig
            {
                root = sq.transform, controller = controller, exposure = exposure,
                health = health, thrower = thrower, respawner = respawner
            };
        }

        static void BuildCameraAndHud(Vector3 cameraPos, SquirrelRig rig, EdgeZone zone)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = cameraPos;
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            var follow = camGo.AddComponent<CameraFollow>();
            Set(follow, "target", rig.controller);

            var hud = new GameObject("Prototype HUD").AddComponent<PrototypeHUD>();
            Set(hud, "zone", zone);
            Set(hud, "exposure", rig.exposure);
            Set(hud, "squirrel", rig.controller);
            Set(hud, "health", rig.health);
            Set(hud, "thrower", rig.thrower);
            Set(hud, "respawner", rig.respawner);
        }

        static Memory MakeMemory(string name, Vector3 pos, Transform squirrel, EdgeZone zone)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var visual = MakeSprite(go.transform, "Visual", Vector2.zero, new Vector2(0.45f, 0.45f), MemoryColor, 30);
            visual.transform.localRotation = Quaternion.Euler(0, 0, 45);
            // Голубое свечение = «подбери». Свечение — дочерний объект, исчезает вместе с кристаллом.
            MakeShape(visual.transform, "Glow", glow, Vector2.zero, new Vector2(3.6f, 3.6f), PickupGlow, 29);
            AddHint(visual.gameObject, "Воспоминание: подбери — Край отступит", new Vector2(0f, 0.9f), 4f, HintCold);
            var m = go.AddComponent<Memory>();
            Set(m, "squirrel", squirrel);
            Set(m, "zone", zone);
            Set(m, "visual", visual.transform);
            return m;
        }

        static Transform EnemyRoot(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            return go.transform;
        }

        static void SetVector(Object target, string field, Vector2 value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform MakeCrawler(string name, Vector3 pos, int dir)
        {
            var root = EnemyRoot(name, pos);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            MakeSprite(visual, "Body", new Vector2(0, 0.4f), new Vector2(1.2f, 0.8f), Moss, 60);
            MakeShape(visual, "Eye", circle, new Vector2(0.4f, 0.55f), new Vector2(0.2f, 0.2f), DangerRed, 61);
            AddSpikes(visual, 0.92f, 0.8f, 3, 59);
            var c = root.gameObject.AddComponent<Crawler>();
            SetVector(c, "hitSize", new Vector2(1.2f, 0.8f));
            SetVector(c, "hitOffset", new Vector2(0, 0.4f));
            SetInt(c, "startDirection", dir);
            Set(c, "visual", visual);
            return root;
        }

        static Transform MakeMoth(string name, Vector3 pos)
        {
            var root = EnemyRoot(name, pos);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            MakeSprite(visual, "Body", Vector2.zero, new Vector2(0.5f, 0.6f), MothColor, 60);
            var wl = MakeSprite(visual, "Wing L", new Vector2(-0.4f, 0.05f), new Vector2(0.5f, 0.4f), MothColor * 0.85f, 59);
            wl.transform.localRotation = Quaternion.Euler(0, 0, 20);
            var wr = MakeSprite(visual, "Wing R", new Vector2(0.4f, 0.05f), new Vector2(0.5f, 0.4f), MothColor * 0.85f, 59);
            wr.transform.localRotation = Quaternion.Euler(0, 0, -20);
            MakeShape(visual, "Eye L", circle, new Vector2(-0.1f, 0.12f), new Vector2(0.14f, 0.14f), DangerRed, 61);
            MakeShape(visual, "Eye R", circle, new Vector2(0.1f, 0.12f), new Vector2(0.14f, 0.14f), DangerRed, 61);
            var m = root.gameObject.AddComponent<Moth>();
            SetVector(m, "hitSize", new Vector2(0.9f, 0.6f));
            SetVector(m, "hitOffset", Vector2.zero);
            SetInt(m, "maxHp", 2);
            Set(m, "visual", visual);
            return root;
        }

        static Transform MakeSpitter(string name, Vector3 pos)
        {
            var root = EnemyRoot(name, pos);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            MakeSprite(visual, "Stem", new Vector2(0, 0.35f), new Vector2(0.45f, 0.7f), new Color(0.9f, 0.88f, 0.75f), 60);
            MakeShape(visual, "Cap", circle, new Vector2(0, 0.85f), new Vector2(1.3f, 0.6f), SpitterColor, 61);
            // Красные споры на шляпке — «стреляет».
            MakeShape(visual, "Spot 1", circle, new Vector2(-0.3f, 0.95f), new Vector2(0.18f, 0.14f), DangerRed, 62);
            MakeShape(visual, "Spot 2", circle, new Vector2(0.25f, 1.0f), new Vector2(0.14f, 0.12f), DangerRed, 62);
            MakeShape(visual, "Spot 3", circle, new Vector2(0.02f, 0.78f), new Vector2(0.12f, 0.1f), DangerRed, 62);
            var sp = root.gameObject.AddComponent<Spitter>();
            SetVector(sp, "hitSize", new Vector2(1.1f, 1.1f));
            SetVector(sp, "hitOffset", new Vector2(0, 0.55f));
            Set(sp, "visual", visual);
            Set(sp, "shotSprite", white);
            Set(sp, "shotMaterial", unlit);
            return root;
        }

        static Transform MakeDummy(string name, Vector3 pos)
        {
            var root = EnemyRoot(name, pos);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            MakeSprite(visual, "Stump", new Vector2(0, 0.8f), new Vector2(1f, 1.6f), new Color(0.6f, 0.45f, 0.32f), 60);
            var d = root.gameObject.AddComponent<TrainingDummy>();
            SetVector(d, "hitSize", new Vector2(1f, 1.6f));
            SetVector(d, "hitOffset", new Vector2(0, 0.8f));
            SetInt(d, "maxHp", 99);
            SetInt(d, "contactDamage", 0);
            Set(d, "visual", visual);
            return root;
        }

        static Nest MakeNest(string name, Vector3 pos, Transform squirrel, EdgeZone zone)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            // Гнездо — чаша из веток с тёплым светом над ней: безопасное место.
            MakeShape(go.transform, "Warm Light", glow, new Vector2(0, 0.6f), new Vector2(3.4f, 2.4f), SafeWarm, 3);
            var marker = MakeShape(go.transform, "Marker", circle, new Vector2(0, 0.18f), new Vector2(1.8f, 0.7f), new Color(0.55f, 0.42f, 0.3f), 5);
            MakeShape(go.transform, "Hollow", circle, new Vector2(0, 0.3f), new Vector2(1.3f, 0.35f), new Color(0.22f, 0.16f, 0.12f), 6);
            for (int i = 0; i < 3; i++)
            {
                var twig = MakeSprite(go.transform, $"Twig {i + 1}", new Vector2(-0.6f + i * 0.6f, 0.42f), new Vector2(0.5f, 0.07f), new Color(0.42f, 0.3f, 0.2f), 7);
                twig.transform.localRotation = Quaternion.Euler(0, 0, i == 1 ? -8f : (i == 0 ? 25f : -25f));
            }
            AddHint(go, "Гнездо: отдых и сохранение. Пока ты здесь, Край стоит", new Vector2(0f, 1.6f), 3.5f, HintWarm);
            var nest = go.AddComponent<Nest>();
            Set(nest, "squirrel", squirrel);
            Set(nest, "zone", zone);
            Set(nest, "marker", marker);
            return nest;
        }

        static void Block(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = color;
            sr.sharedMaterial = unlit;
            if (color == Bark && barkSprite != null)
            {
                // Кора — с бороздами, плиткой: сразу видно, что по ней можно лезть.
                sr.sprite = barkSprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(w, h);
                sr.color = Color.Lerp(Bark, Color.white, 0.12f);
                go.AddComponent<BoxCollider2D>().size = new Vector2(w, h);
            }
            else
            {
                go.transform.localScale = new Vector3(w, h, 1);
                sr.sprite = white;
                go.AddComponent<BoxCollider2D>();
            }
            if (color == Bark) go.AddComponent<BarkSurface>(); // по коре можно лазать
            go.isStatic = true;
        }

        static SpriteRenderer MakeSprite(Transform parent, string name, Vector2 localPos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = color;
            sr.sharedMaterial = unlit;
            sr.sortingOrder = order;
            return sr;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"Нет поля {field} в {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Sprite MakeWhiteSprite()
        {
            string path = Generated + "/White.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(4, 4);
                tex.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray());
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp.textureType != TextureImporterType.Sprite || !Mathf.Approximately(imp.spritePixelsPerUnit, 4f))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = 4;
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Material MakeUnlitMaterial()
        {
            string path = Generated + "/GreyboxUnlit.mat";
            // URP-шейдер работает только при назначенном URP-ассете, иначе спрайты будут розовыми.
            Shader shader = null;
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                if (mat.shader != shader) { mat.shader = shader; EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets(); }
                return mat;
            }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static T LoadOrCreate<T>(string path, System.Action<T> init = null) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            init?.Invoke(a);
            AssetDatabase.CreateAsset(a, path);
            AssetDatabase.SaveAssets();
            return a;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddSceneToBuild(string path, bool first)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            if (first) scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            else scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
