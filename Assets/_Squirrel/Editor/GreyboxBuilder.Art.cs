using System.IO;
using UnityEditor;
using UnityEngine;

namespace SquirrelEditor
{
    /// <summary>
    /// Арт поверх серых коробок. Пока здесь только белка из концепт-арта:
    /// Assets/_Squirrel/Art/Squirrel/squirrel_concept.png (фон вырезан, свечение — в альфе).
    /// Если файла нет, сборщик рисует прежнюю белку из прямоугольников.
    /// </summary>
    public static partial class GreyboxBuilder
    {
        const string SquirrelConceptPath = Root + "/Art/Squirrel/squirrel_concept.png";

        // Тело белки на картинке (без хвоста) — около 610 px в длину; в игре это 0.9 ед., чуть шире хитбокса (0.8).
        const float SquirrelConceptPpu = 680f;
        // Пивот: по x — середина тела (хвост не в счёт), по y — подушечки лап.
        static readonly Vector2 SquirrelConceptPivot = new Vector2(0.63f, 0.035f);

        static Sprite LoadSquirrelConcept()
        {
            if (!File.Exists(SquirrelConceptPath)) return null;
            var imp = (TextureImporter)AssetImporter.GetAtPath(SquirrelConceptPath);
            if (imp == null)
            {
                AssetDatabase.ImportAsset(SquirrelConceptPath);
                imp = (TextureImporter)AssetImporter.GetAtPath(SquirrelConceptPath);
                if (imp == null) return null;
            }
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            bool dirty = imp.textureType != TextureImporterType.Sprite
                || !Mathf.Approximately(imp.spritePixelsPerUnit, SquirrelConceptPpu)
                || ts.spriteAlignment != (int)SpriteAlignment.Custom
                || (ts.spritePivot - SquirrelConceptPivot).sqrMagnitude > 1e-6f
                || !imp.mipmapEnabled;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.ReadTextureSettings(ts);
                ts.spriteAlignment = (int)SpriteAlignment.Custom;
                ts.spritePivot = SquirrelConceptPivot;
                ts.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(ts);
                imp.spritePixelsPerUnit = SquirrelConceptPpu;
                imp.mipmapEnabled = true;           // на экране она в 8–9 раз меньше картинки — без мипов рябит
                imp.filterMode = FilterMode.Trilinear;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.maxTextureSize = 1024;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SquirrelConceptPath);
        }
    }
}
