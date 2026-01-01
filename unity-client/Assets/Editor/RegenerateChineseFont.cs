using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;

public class RegenerateChineseFont : EditorWindow
{
    [MenuItem("Tools/Regenerate Chinese Font Asset")]
    public static void Regenerate()
    {
        string sourceFontPath = "Assets/Fonts/SourceHanSansSC-Regular.otf";
        string outputPath = "Assets/Fonts/SourceHanSansSC-Regular SDF.asset";

        // Load source font
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[RegenerateFont] Source font not found at: {sourceFontPath}");
            return;
        }

        Debug.Log($"[RegenerateFont] Loaded source font: {sourceFont.name}");

        // Create font asset using TMP's API
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
        
        if (fontAsset == null)
        {
            Debug.LogError("[RegenerateFont] Failed to create font asset!");
            return;
        }

        // Configure the font asset for Chinese characters
        fontAsset.name = "SourceHanSansSC-Regular SDF";
        
        // Save the asset
        if (File.Exists(outputPath))
        {
            AssetDatabase.DeleteAsset(outputPath);
        }
        
        AssetDatabase.CreateAsset(fontAsset, outputPath);
        
        // Save the atlas texture as a sub-asset
        if (fontAsset.atlasTexture != null)
        {
            fontAsset.atlasTexture.name = "SourceHanSansSC-Regular SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        }
        
        // Save the material as a sub-asset
        if (fontAsset.material != null)
        {
            fontAsset.material.name = "SourceHanSansSC-Regular SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[RegenerateFont] Font asset created successfully at: {outputPath}");
        
        // Setup as fallback for TMP Settings
        SetupFallbackFont(fontAsset);
        
        EditorUtility.DisplayDialog("Regenerate Font", 
            "Chinese font asset regenerated successfully!\n\nNote: For full Chinese character support, use Window > TextMeshPro > Font Asset Creator to generate with a specific character set.", 
            "OK");
    }

    private static void SetupFallbackFont(TMP_FontAsset chineseFont)
    {
        // Get TMP Settings
        string settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath);
        
        if (settings == null)
        {
            Debug.LogWarning("[RegenerateFont] TMP Settings not found. Skipping fallback setup.");
            return;
        }

        // Add Chinese font as fallback to default font
        string defaultFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        TMP_FontAsset defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(defaultFontPath);
        
        if (defaultFont != null && defaultFont.fallbackFontAssetTable != null)
        {
            if (!defaultFont.fallbackFontAssetTable.Contains(chineseFont))
            {
                defaultFont.fallbackFontAssetTable.Add(chineseFont);
                EditorUtility.SetDirty(defaultFont);
                AssetDatabase.SaveAssets();
                Debug.Log("[RegenerateFont] Added Chinese font as fallback to default font.");
            }
        }
    }
}
