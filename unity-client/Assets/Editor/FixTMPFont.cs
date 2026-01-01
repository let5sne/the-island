using UnityEngine;
using UnityEditor;
using TMPro;

public class FixTMPFont : EditorWindow
{
    [MenuItem("Tools/Fix TMP Font Assets")]
    public static void FixFonts()
    {
        // Find all TMP font assets
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (font == null) continue;

            bool needsSave = false;

            // Check if material is missing
            if (font.material == null)
            {
                Debug.LogWarning($"[FixTMPFont] Font '{font.name}' has missing material. Attempting to fix...");
                
                // Try to find a sub-asset material
                Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                foreach (Object subAsset in subAssets)
                {
                    if (subAsset is Material mat)
                    {
                        Debug.Log($"[FixTMPFont] Found sub-asset material: {mat.name}");
                        font.material = mat;
                        needsSave = true;
                        break;
                    }
                }

                // If still no material, create one
                if (font.material == null && font.atlasTexture != null)
                {
                    Debug.Log($"[FixTMPFont] Creating new material for '{font.name}'");
                    Shader shader = Shader.Find("TextMeshPro/Distance Field");
                    if (shader != null)
                    {
                        Material newMat = new Material(shader);
                        newMat.name = font.name + " Atlas Material";
                        newMat.SetTexture("_MainTex", font.atlasTexture);
                        AssetDatabase.AddObjectToAsset(newMat, font);
                        font.material = newMat;
                        needsSave = true;
                        Debug.Log($"[FixTMPFont] Created and assigned new material for '{font.name}'");
                    }
                    else
                    {
                        Debug.LogError("[FixTMPFont] Could not find TextMeshPro/Distance Field shader!");
                    }
                }
            }

            // Verify atlas texture
            if (font.atlasTexture == null)
            {
                Debug.LogError($"[FixTMPFont] Font '{font.name}' has no atlas texture - needs to be regenerated!");
            }

            if (needsSave)
            {
                EditorUtility.SetDirty(font);
                fixedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FixTMPFont] Done! Fixed {fixedCount} font asset(s).");
        EditorUtility.DisplayDialog("Fix TMP Fonts", $"Fixed {fixedCount} font asset(s). Check console for details.", "OK");
    }

    [MenuItem("Tools/Regenerate Chinese Font")]
    public static void RegenerateChinese()
    {
        string fontPath = "Assets/Fonts/SourceHanSansSC-Regular.otf";
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        
        if (sourceFont == null)
        {
            Debug.LogError($"[FixTMPFont] Could not load font at '{fontPath}'");
            return;
        }

        Debug.Log($"[FixTMPFont] Source font loaded: {sourceFont.name}");
        Debug.Log("[FixTMPFont] Please use Window > TextMeshPro > Font Asset Creator to regenerate the font.");

        // Open the Font Asset Creator
        EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Font Asset Creator");
    }
}
