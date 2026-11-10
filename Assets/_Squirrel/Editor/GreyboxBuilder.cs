using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Squirrel;
using Squirrel.Edge;
using Squirrel.World;

namespace SquirrelEditor
{
    /// <summary>
    /// Меню Squirrel → «Собрать серую комнату». Создаёт сцену Assets/_Squirrel/Scenes/Greybox.unity
    /// со всем нужным: белка, Край, Сердце, два гнезда, платформы, камера, HUD.
    /// Повторный запуск пересоздаёт сцену, но не трогает настройки (ScriptableObject'ы).
    /// </summary>
    public static class GreyboxBuilder
    {
        const string Root = "Assets/_Squirrel";
        const string Generated = Root + "/Generated";
        const string SettingsDir = Root + "/Settings";
        const string ScenePath = Root + "/Scenes/Greybox.unity";

        static readonly Color Ground = new Color(0.36f, 0.40f, 0.38f);
        static readonly Color Platform = new Color(0.50f, 0.55f, 0.50f);
        static readonly Color Bark = new Color(0.42f, 0.34f, 0.30f);
        static readonly Color Background = new Color(0.14f, 0.17f, 0.20f);

        static Sprite white;
        static Material unlit;

        [MenuItem("Squirrel/Собрать серую комнату")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Generated);
            EnsureFolder(SettingsDir);
            EnsureFolder(Root + "/Scenes");
            white = MakeWhiteSprite();
            unlit = MakeUnlitMaterial();
            var squirrelSettings = LoadOrCreate<SquirrelSettings>(SettingsDir + "/SquirrelSettings.asset");
            var edgeSettings = LoadOrCreate<EdgeSettings>(SettingsDir + "/EdgeSettings.asset");

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

            // --- Белка ---
            var sq = new GameObject("Squirrel");
            sq.transform.position = new Vector3(-28, 0.05f);
            var box = sq.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 0.7f);
            box.offset = new Vector2(0, 0.35f);
            box.isTrigger = true;
            sq.AddComponent<SquirrelInput>();
            sq.AddComponent<SquirrelMotor>();
            var controller = sq.AddComponent<SquirrelController>();
            var body = new GameObject("Visual").transform;
            body.SetParent(sq.transform, false);
            // Визуал с пивотом у ног, чтобы сжатие при приземлении шло от земли.
            MakeSprite(body, "Body", new Vector2(0, 0.35f), new Vector2(0.9f, 0.7f), new Color(0.92f, 0.52f, 0.22f), 110);
            var tail = MakeSprite(body, "Tail", new Vector2(-0.55f, 0.75f), new Vector2(0.45f, 0.9f), new Color(0.75f, 0.38f, 0.16f), 109);
            tail.transform.localRotation = Quaternion.Euler(0, 0, -15);
            MakeSprite(body, "Ear", new Vector2(0.28f, 0.78f), new Vector2(0.14f, 0.2f), new Color(0.75f, 0.38f, 0.16f), 111);
            MakeSprite(body, "Eye", new Vector2(0.3f, 0.5f), new Vector2(0.12f, 0.12f), new Color(0.08f, 0.06f, 0.05f), 112);
            Set(controller, "settings", squirrelSettings);
            Set(controller, "visual", body);

            var exposure = sq.AddComponent<EdgeExposure>();
            Set(exposure, "zone", zone);
            Set(exposure, "squirrel", controller);
            Set(heart, "squirrel", sq.transform);

            // --- Гнёзда ---
            MakeNest("Nest 1 (start)", new Vector3(-28, 0), sq.transform, zone);
            MakeNest("Nest 2 (top of trunk)", new Vector3(6, 18), sq.transform, zone);

            // --- Камера и HUD ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(-26, 2, -10);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            var follow = camGo.AddComponent<CameraFollow>();
            Set(follow, "target", controller);

            var hud = new GameObject("Prototype HUD").AddComponent<PrototypeHUD>();
            Set(hud, "zone", zone);
            Set(hud, "exposure", exposure);
            Set(hud, "squirrel", controller);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            Selection.activeObject = squirrelSettings;
            Debug.Log("Серая комната собрана: " + ScenePath + ". Жми Play. Числа белки — в Settings/SquirrelSettings, Края — в Settings/EdgeSettings.");
        }

        static void MakeNest(string name, Vector3 pos, Transform squirrel, EdgeZone zone)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var marker = MakeSprite(go.transform, "Marker", new Vector2(0, 0.15f), new Vector2(1.6f, 0.3f), new Color(0.55f, 0.42f, 0.3f), 5);
            var nest = go.AddComponent<Nest>();
            Set(nest, "squirrel", squirrel);
            Set(nest, "zone", zone);
            Set(nest, "marker", marker);
        }

        static void Block(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y);
            go.transform.localScale = new Vector3(w, h, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = color;
            sr.sharedMaterial = unlit;
            go.AddComponent<BoxCollider2D>();
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
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
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

        static void AddSceneToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
