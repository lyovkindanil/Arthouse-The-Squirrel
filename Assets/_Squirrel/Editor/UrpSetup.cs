using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SquirrelEditor
{
    /// <summary>
    /// Переводит проект на URP с 2D-рендерером: создаёт Assets/Settings/URP-2D.asset
    /// и назначает его в Graphics и во все уровни Quality. Повторный вызов ничего не ломает.
    /// </summary>
    public static class UrpSetup
    {
        const string Dir = "Assets/Settings";
        const string AssetPath = Dir + "/URP-2D.asset";
        const string RendererPath = Dir + "/URP-2D-Renderer.asset";

        [MenuItem("Squirrel/Настроить URP 2D")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Settings");

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if (asset == null)
            {
                var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                    AssetDatabase.CreateAsset(renderer, RendererPath);
                }
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();
            Debug.Log("URP 2D назначен: " + AssetPath);
        }
    }
}
