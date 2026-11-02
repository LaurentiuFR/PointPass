using UnityEditor;

namespace PointPass.EditorTools
{
    /// <summary>
    /// Automatically sets sprite import settings for every PNG in a Resources/Art folder,
    /// so you never have to touch the Inspector for imported art.
    /// </summary>
    public class ArtImportSettings : AssetPostprocessor
    {
        [InitializeOnLoadMethod]
        private static void ScheduleQualityRefresh()
        {
            EditorApplication.delayCall += RefreshPresentationQuality;
        }
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Art/")) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = true;
            importer.filterMode = UnityEngine.FilterMode.Trilinear;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Keep detail for desktop/close views; mipmaps smoothly downsample it at match scale.
            if (assetPath.EndsWith("_polished.png") || assetPath.EndsWith("_rigged.png") || assetPath.EndsWith("arm_runner.png"))
                importer.maxTextureSize = 1024;
        }

        [MenuItem("PointPass/Refresh presentation quality")]
        public static void RefreshPresentationQuality()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/Art")) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Resources/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                bool detailed = path.EndsWith("_polished.png") || path.EndsWith("_rigged.png") || path.EndsWith("arm_runner.png");
                if (importer != null && (!importer.mipmapEnabled || importer.filterMode != UnityEngine.FilterMode.Trilinear
                    || (detailed && importer.maxTextureSize != 1024)))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            UnityEngine.Object pipeline = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/UniversalRP.asset");
            if (pipeline != null)
            {
                SerializedObject settings = new SerializedObject(pipeline);
                settings.FindProperty("m_MSAA").intValue = 4;
                settings.FindProperty("m_RenderScale").floatValue = 1f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            UnityEngine.QualitySettings.antiAliasing = 4;
        }
    }
}
