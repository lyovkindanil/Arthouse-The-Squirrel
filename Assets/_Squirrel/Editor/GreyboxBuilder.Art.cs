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
    /// Если папки облика нет, сборщик рисует прежнюю белку из прямоугольников.
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

        /// <summary>Визуал белки из облика: свечение + два меш-слоя + аниматор. false — облика нет, рисуем коробки.</summary>
        static bool BuildSquirrelArt(Transform visual, SquirrelController controller)
        {
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
