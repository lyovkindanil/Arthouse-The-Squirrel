using System.IO;
using UnityEditor;
using UnityEngine;
using Squirrel.World;

namespace SquirrelEditor
{
    /// <summary>
    /// Простые формы для серого бокса, чтобы объекты читались по силуэту, а не только по цвету:
    /// круг, мягкое свечение, кольцо, треугольник (шипы) и текстура коры с бороздами.
    /// Картинки рисуются кодом в Assets/_Squirrel/Generated при сборке сцены.
    /// </summary>
    public static partial class GreyboxBuilder
    {
        static Sprite circle, glow, ring, triangle, barkSprite;

        // Язык цветов серого бокса: голубое светится — подбирай; красное — опасно; тёплое — безопасно.
        static readonly Color PickupGlow = new Color(0.55f, 0.95f, 1f, 0.45f);
        static readonly Color DangerRed = new Color(0.95f, 0.22f, 0.2f);
        static readonly Color SafeWarm = new Color(1f, 0.8f, 0.45f, 0.28f);
        static readonly Color HintCold = new Color(0.75f, 0.95f, 1f);
        static readonly Color HintWarm = new Color(1f, 0.9f, 0.7f);
        static readonly Color HintDanger = new Color(1f, 0.7f, 0.55f);

        static void PrepareShapes()
        {
            circle = ShapeSprite("Circle", 64, (x, y) => Smooth(1f - Len(x, y), 0.04f));
            glow = ShapeSprite("Glow", 64, (x, y) => { float d = Mathf.Clamp01(1f - Len(x, y)); return d * d; });
            ring = ShapeSprite("Ring", 64, (x, y) => { float d = Len(x, y); return Smooth(1f - d, 0.04f) * Smooth(d - 0.78f, 0.04f); });
            // Треугольник остриём вверх: основание внизу картинки.
            triangle = ShapeSprite("Triangle", 64, (x, y) =>
            {
                float v = (y + 1f) * 0.5f;          // 0 внизу, 1 вверху
                float half = (1f - v) * 1f;         // половина ширины на этой высоте
                return Smooth(half - Mathf.Abs(x), 0.04f) * Smooth(v, 0.02f) * Smooth(1f - v, 0.02f);
            });
            barkSprite = MakeBarkSprite();
        }

        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);
        static float Smooth(float v, float edge) => Mathf.Clamp01(v / edge);

        /// <summary>Белая картинка с альфой из функции (x, y от −1 до 1). 1 картинка = 1 единица мира.</summary>
        static Sprite ShapeSprite(string name, int size, System.Func<float, float, float> alpha)
        {
            string path = Generated + $"/{name}.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = (x + 0.5f) / size * 2f - 1f, fy = (y + 0.5f) / size * 2f - 1f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(fx, fy)));
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp.textureType != TextureImporterType.Sprite || !Mathf.Approximately(imp.spritePixelsPerUnit, size))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = size;
                imp.filterMode = FilterMode.Bilinear;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Кора: вертикальные борозды. Плитка 2×2 ед, кладётся «Tiled», поэтому не растягивается.</summary>
        static Sprite MakeBarkSprite()
        {
            string path = Generated + "/Bark.png";
            const int n = 16;
            if (!File.Exists(path))
            {
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Борозды через каждые 4 пикселя, слегка «гуляют» по высоте; поперечные трещинки реже.
                    int shifted = (x + (y / 5) % 2) % 4;
                    float v = shifted == 0 ? 0.62f : shifted == 1 ? 0.85f : 1f;
                    if ((y == 3 && x % 8 < 3) || (y == 11 && (x + 4) % 8 < 3)) v *= 0.75f;
                    tex.SetPixel(x, y, new Color(v, v, v, 1f));
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            if (imp.textureType != TextureImporterType.Sprite || ts.spriteMeshType != SpriteMeshType.FullRect || !Mathf.Approximately(imp.spritePixelsPerUnit, 8f))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.ReadTextureSettings(ts);
                ts.spriteMeshType = SpriteMeshType.FullRect; // нужно для Tiled
                imp.SetTextureSettings(ts);
                imp.spritePixelsPerUnit = 8;
                imp.filterMode = FilterMode.Point;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static SpriteRenderer MakeShape(Transform parent, string name, Sprite sprite, Vector2 localPos, Vector2 size, Color color, int order)
        {
            var sr = MakeSprite(parent, name, localPos, size, color, order);
            sr.sprite = sprite;
            return sr;
        }

        static WorldHint AddHint(GameObject go, string text, Vector2 offset, float radius, Color color)
        {
            var h = go.AddComponent<WorldHint>();
            h.Setup(text, offset, radius, color);
            EditorUtility.SetDirty(h);
            return h;
        }

        /// <summary>Красные шипы на спине — «не касаться».</summary>
        static void AddSpikes(Transform visual, float y, float width, int count, int order)
        {
            for (int i = 0; i < count; i++)
            {
                float x = count == 1 ? 0f : Mathf.Lerp(-width * 0.5f, width * 0.5f, i / (float)(count - 1));
                MakeShape(visual, $"Spike {i + 1}", triangle, new Vector2(x, y), new Vector2(0.28f, 0.32f), DangerRed, order);
            }
        }
    }
}
