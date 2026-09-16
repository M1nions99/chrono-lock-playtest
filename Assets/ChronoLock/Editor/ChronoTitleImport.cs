using UnityEditor;
using UnityEngine;

public sealed class ChronoTitleImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.EndsWith("/Resources/TitleBackground.png"))return;
        var texture=(TextureImporter)assetImporter;
        texture.textureType=TextureImporterType.Default;
        texture.mipmapEnabled=false;
        texture.sRGBTexture=true;
        texture.maxTextureSize=2048;
        texture.textureCompression=TextureImporterCompression.Uncompressed;
        texture.wrapMode=TextureWrapMode.Clamp;
        texture.filterMode=FilterMode.Bilinear;
    }
}
