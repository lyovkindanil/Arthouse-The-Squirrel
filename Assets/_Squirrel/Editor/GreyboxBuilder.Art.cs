using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Squirrel;
using Squirrel.Combat;

namespace SquirrelEditor
{
    /// <summary>
    /// Арт поверх серых коробок. Пока здесь только белка: её облик лежит в
    /// Assets/_Squirrel/Art/Squirrel/&lt;облик&gt;/ (позы idle, run, jump, cling и form.json с масштабом и пивотами;
    /// их готовит Tools/SquirrelSprites/prepare.py из концепт-картинок).
    /// Если рядом лежит папка &lt;облик&gt;3d с clips.json (покадровые анимации из 3D-модели,
    /// их готовит Tools/SquirrelBlender), белка собирается из неё: это основной вид.
    /// Если нет ни того, ни другого, сборщик рисует прежнюю белку из прямоугольников.
    /// </summary>
    public static partial class GreyboxBuilder
    {
        const string SquirrelArtDir = Root + "/Art/Squirrel";
        const string DefaultSquirrelForm = "spirit";

        [Serializable] class FormManifest { public string form; public PoseEntry[] poses; }
        [Serializable] class PoseEntry { public string name; public float ppu, pivotX, pivotY, anchorX, anchorY, tailX, tailY; }

        /// <summary>Облик белки из папки Art/Squirrel/&lt;id&gt;: импортирует позы и собирает ассет SquirrelForm.</summary>
        static SquirrelForm LoadSquirrelForm(string id)
        {
            string dir = SquirrelArtDir + "/" + id;
            string manifestPath = dir + "/form.json";
            if (!File.Exists(manifestPath)) return null;
            var manifest = JsonUtility.FromJson<FormManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.poses == null) return null;

            string assetPath = SettingsDir + "/SquirrelForm_" + id + ".asset";
            var form = LoadOrCreate<SquirrelForm>(assetPath, f => f.id = id);
            foreach (var e in manifest.poses)
            {
                var sprite = ImportPose(dir + "/" + e.name + ".png", e.ppu, new Vector2(e.pivotX, e.pivotY));
                if (sprite == null) { Debug.LogError($"Облик «{id}»: нет позы {e.name}.png"); return null; }
                var pose = new SquirrelForm.Pose { sprite = sprite, anchor = new Vector2(e.anchorX, e.anchorY), tailBase = new Vector2(e.tailX, e.tailY) };
                switch (e.name)
                {
                    case "idle": form.idle = pose; break;
                    case "run": form.run = pose; break;
                    case "jump": form.jump = pose; break;
                    case "cling": form.cling = pose; break;
                }
            }
            if (form.idle.sprite == null) { Debug.LogError($"Облик «{id}»: в form.json нет позы idle"); return null; }
            // Каких поз не хватает — подменяем стоячей, чтобы игра не ломалась на неполном наборе.
            if (form.run.sprite == null) form.run = form.idle;
            if (form.jump.sprite == null) form.jump = form.run;
            if (form.cling.sprite == null) form.cling = form.idle;
            EditorUtility.SetDirty(form);
            AssetDatabase.SaveAssets();
            return form;
        }

        static Sprite ImportPose(string path, float ppu, Vector2 pivot)
        {
            if (!File.Exists(path)) return null;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
            {
                AssetDatabase.ImportAsset(path);
                imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) return null;
            }
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            bool dirty = imp.textureType != TextureImporterType.Sprite
                || !Mathf.Approximately(imp.spritePixelsPerUnit, ppu)
                || ts.spriteAlignment != (int)SpriteAlignment.Custom
                || (ts.spritePivot - pivot).sqrMagnitude > 1e-7f
                || ts.spriteMeshType != SpriteMeshType.FullRect
                || !imp.mipmapEnabled;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.ReadTextureSettings(ts);
                ts.spriteAlignment = (int)SpriteAlignment.Custom;
                ts.spritePivot = pivot;
                ts.spriteMeshType = SpriteMeshType.FullRect; // аниматор кладёт весь прямоугольник на сетку
                imp.SetTextureSettings(ts);
                imp.spritePixelsPerUnit = ppu;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.mipmapEnabled = true;                    // на экране белка в разы меньше картинки — без мипов рябит
                imp.filterMode = FilterMode.Trilinear;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Material MakeSpiritMaterial()
        {
            var shader = Shader.Find("Squirrel/SpiritSprite");
            if (shader == null) { Debug.LogError("Не найден шейдер Squirrel/SpiritSprite"); return null; }
            string path = Generated + "/SpiritSprite.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
                AssetDatabase.SaveAssets();
            }
            else if (mat.shader != shader) { mat.shader = shader; EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets(); }
            return mat;
        }

        [Serializable] class ClipManifest { public string form; public ClipEntry[] clips; }
        [Serializable] class ClipEntry { public string name; public int frames, cols, size; public float fps, ppu, pivotX, pivotY, offsetX, offsetY; public bool loop; }

        /// <summary>Покадровые анимации белки из папки Art/Squirrel/&lt;id&gt;: режет листы кадров на спрайты и собирает ассет SquirrelClips.</summary>
        static SquirrelClips LoadSquirrelClips(string id)
        {
            string dir = SquirrelArtDir + "/" + id;
            string manifestPath = dir + "/clips.json";
            if (!File.Exists(manifestPath)) return null;
            string json = File.ReadAllText(manifestPath);
            var manifest = JsonUtility.FromJson<ClipManifest>(json);
            if (manifest == null || manifest.clips == null || manifest.clips.Length == 0) return null;

            string assetPath = SettingsDir + "/SquirrelClips_" + id + ".asset";
            string stamp = Hash128.Compute(json).ToString();
            var set = AssetDatabase.LoadAssetAtPath<SquirrelClips>(assetPath);
            if (set != null && set.source == stamp && ClipsIntact(set)) return set;

            // Кадры хранятся внутри ассета; при изменении clips.json собираем его заново.
            if (set != null) AssetDatabase.DeleteAsset(assetPath);
            set = ScriptableObject.CreateInstance<SquirrelClips>();
            set.id = id;
            set.source = stamp;
            AssetDatabase.CreateAsset(set, assetPath);

            var list = new System.Collections.Generic.List<SquirrelClips.Clip>();
            foreach (var e in manifest.clips)
            {
                var tex = ImportClipSheet(dir + "/" + e.name + ".png");
                if (tex == null) { Debug.LogError($"Анимации «{id}»: нет листа {e.name}.png"); continue; }
                var clip = new SquirrelClips.Clip
                {
                    name = e.name, fps = e.fps, loop = e.loop,
                    offset = new Vector2(e.offsetX, e.offsetY),
                    frames = new Sprite[e.frames],
                };
                int cols = Mathf.Max(1, e.cols);
                for (int i = 0; i < e.frames; i++)
                {
                    // Кадры идут слева направо, сверху вниз; у текстуры ноль снизу.
                    var rect = new Rect((i % cols) * e.size, tex.height - (i / cols + 1) * e.size, e.size, e.size);
                    var sprite = Sprite.Create(tex, rect, new Vector2(e.pivotX, e.pivotY), e.ppu, 0, SpriteMeshType.FullRect);
                    sprite.name = $"{e.name}_{i:000}";
                    AssetDatabase.AddObjectToAsset(sprite, set);
                    clip.frames[i] = sprite;
                }
                list.Add(clip);
            }
            set.clips = list.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            if (set.Find("idle_test") == null) { Debug.LogError($"Анимации «{id}»: в clips.json нет клипа idle_test"); return null; }
            return set;
        }

        static bool ClipsIntact(SquirrelClips set)
        {
            if (set.clips == null || set.clips.Length == 0) return false;
            foreach (var c in set.clips)
            {
                if (c == null || c.frames == null || c.frames.Length == 0) return false;
                foreach (var f in c.frames) if (f == null) return false;
            }
            return true;
        }

        static Texture2D ImportClipSheet(string path)
        {
            if (!File.Exists(path)) return null;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
            {
                AssetDatabase.ImportAsset(path);
                imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) return null;
            }
            bool dirty = imp.textureType != TextureImporterType.Default
                || imp.npotScale != TextureImporterNPOTScale.None
                || !imp.mipmapEnabled
                || !imp.alphaIsTransparency
                || imp.maxTextureSize != 4096
                || imp.wrapMode != TextureWrapMode.Clamp;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Default; // спрайты режем сами, см. LoadSquirrelClips
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.mipmapEnabled = true;
                imp.filterMode = FilterMode.Trilinear;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaIsTransparency = true;
                imp.sRGBTexture = true;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.maxTextureSize = 4096;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Визуал белки из покадровых анимаций: свечение + спрайт + аниматор. false — клипов нет.</summary>
        static bool BuildSquirrelClipArt(Transform visual, SquirrelController controller)
        {
            var set = LoadSquirrelClips(DefaultSquirrelForm + "3d");
            if (set == null) return false;

            var glowSr = MakeShape(visual, "Spirit Glow", glow, new Vector2(-0.1f, 0.55f), set.glowSize, set.glowColor, 108);

            var spirit = new GameObject("Spirit").transform;
            spirit.SetParent(visual, false);
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(spirit, false);
            var sr = bodyGo.AddComponent<SpriteRenderer>();
            sr.sprite = set.Find("idle_test").frames[0];
            sr.sortingOrder = 110;
            if (unlit != null) sr.sharedMaterial = unlit;

            var anim = spirit.gameObject.AddComponent<SquirrelClipAnimator>();
            Set(anim, "controller", controller);
            Set(anim, "clips", set);
            Set(anim, "body", sr);
            Set(anim, "glow", glowSr);
            return true;
        }

        /// <summary>Визуал белки из облика: свечение + два меш-слоя + аниматор. false — облика нет, рисуем коробки.</summary>
        static bool BuildSquirrelArt(Transform visual, SquirrelController controller)
        {
            if (BuildSquirrelClipArt(visual, controller)) return true;

            var form = LoadSquirrelForm(DefaultSquirrelForm);
            var material = form != null ? MakeSpiritMaterial() : null;
            if (form == null || material == null) return false;

            var glowSr = MakeShape(visual, "Spirit Glow", glow, new Vector2(-0.15f, 0.7f), form.glowSize, form.glowColor, 108);

            var spirit = new GameObject("Spirit").transform;
            spirit.SetParent(visual, false);
            var a = MakeMeshLayer(spirit, "Pose A", material, 111);
            var b = MakeMeshLayer(spirit, "Pose B", material, 110);

            var anim = spirit.gameObject.AddComponent<SquirrelSpriteAnimator>();
            Set(anim, "controller", controller);
            Set(anim, "form", form);
            Set(anim, "layerA", a);
            Set(anim, "layerB", b);
            Set(anim, "glow", glowSr);
            return true;
        }

        static MeshFilter MakeMeshLayer(Transform parent, string name, Material material, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mf;
        }
    }
}
