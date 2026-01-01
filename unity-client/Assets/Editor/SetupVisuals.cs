using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.PostProcessing;

public class SetupVisuals
{
    [MenuItem("TheIsland/Setup PostProcessing")]
    public static void Setup()
    {
        // 1. Setup Camera
        var camera = Camera.main;
        if (camera == null) { Debug.LogError("No Main Camera!"); return; }
        
        var layer = camera.gameObject.GetComponent<PostProcessLayer>();
        if (layer == null) layer = camera.gameObject.AddComponent<PostProcessLayer>();
        
        // Use Default layer
        layer.volumeLayer = LayerMask.GetMask("Default"); 
        // Simple AA
        layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;

        // 2. Create Global Volume
        var volumeGo = GameObject.Find("GlobalVolume");
        if (volumeGo == null)
        {
            volumeGo = new GameObject("GlobalVolume");
            volumeGo.layer = LayerMask.NameToLayer("Default");
        }
        
        var volume = volumeGo.GetComponent<PostProcessVolume>();
        if (volume == null) volume = volumeGo.AddComponent<PostProcessVolume>();
        
        volume.isGlobal = true;
        
        // 3. Create Profile if not exists
        var profilePath = "Assets/GlobalProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        volume.profile = profile;

        // 4. Clean existing settings
        profile.settings.Clear();

        // 5. Add Effects
        // Bloom - Glow effect
        var bloom = profile.AddSettings<Bloom>();
        bloom.enabled.value = true;
        bloom.intensity.value = 3.0f;
        bloom.threshold.value = 1.0f;
        bloom.softKnee.value = 0.5f;

        // Color Grading - Better colors
        var colorGrading = profile.AddSettings<ColorGrading>();
        colorGrading.enabled.value = true;
        colorGrading.tonemapper.value = Tonemapper.ACES;
        colorGrading.postExposure.value = 0.5f; // Slightly brighter
        colorGrading.saturation.value = 20f; // More vibrant
        colorGrading.contrast.value = 15f; // More pop

        // Vignette - Focus center
        var vignette = profile.AddSettings<Vignette>();
        vignette.enabled.value = true;
        vignette.intensity.value = 0.35f;
        vignette.smoothness.value = 0.4f;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("Visuals Setup Complete! Bloom, ColorGrading, and Vignette configured.");
    }
}
