using UnityEditor;

namespace PointPass.EditorTools
{
    /// <summary>
    /// Automatically sets sprite import settings for every PNG in a Resources/Art folder,
    /// so you never have to touch the Inspector for imported art.
    /// </summary>
    public class ArtImportSettings : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Art/")) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
