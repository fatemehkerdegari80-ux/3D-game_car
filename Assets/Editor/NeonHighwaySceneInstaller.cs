#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using NeonHighway;

// Runs once when the project is first opened in Unity, creating an actual editable .unity scene.
// Manual backup: Tools > Neon Highway > Build / Repair Game Scene.
// (Re-run it after updating the project so the new post-processing look is applied.)
[InitializeOnLoad]
public static class NeonHighwaySceneInstaller
{
    const string ScenePath = "Assets/Scenes/NeonHighway.unity";
    static NeonHighwaySceneInstaller() { EditorApplication.delayCall += AutoBuild; }

    static void AutoBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoBuild;
            return;
        }
        if(!System.IO.File.Exists(ScenePath)) BuildScene();
        else EnsureBuildSetting();
    }

    [MenuItem("Tools/Neon Highway/Build or Repair Game Scene")]
    public static void BuildScene()
    {
        if(EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode before rebuilding."); return; }
        if(!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets","Scenes");
        if(!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets","Settings");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cameraGo=new GameObject("Main Camera | Third person",typeof(Camera),typeof(AudioListener));
        cameraGo.tag="MainCamera";
        cameraGo.transform.position=new Vector3(0,7.0f,-12.8f);
        cameraGo.transform.LookAt(new Vector3(0,1.1f,21.5f));
        var camera=cameraGo.GetComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.04f,.025f,.08f);
        camera.fieldOfView=64f;
        camera.allowHDR=true;
        AddBloom(cameraGo);
        var root=new GameObject("GameManager | Neon Highway");
        root.AddComponent<GameManager>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureBuildSetting();
        AssetDatabase.SaveAssets();
        Debug.Log("[Neon Highway] Scene created. Press Play. Scene: " + ScenePath);
    }

    // Post-processing uses the URP Volume system included by this project's package manifest.
    static void AddBloom(GameObject cameraObject)
    {
        if (GraphicsSettings.currentRenderPipeline == null)
        {
            Debug.LogWarning("[Neon Highway] No URP asset is active, so Bloom was not configured.");
            return;
        }

        const string profilePath = "Assets/Settings/NeonURPBloom.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        var bloom = EnsureOverride<Bloom>(profile);
        bloom.intensity.Override(1.25f);
        bloom.threshold.Override(1.0f);
        bloom.scatter.Override(.7f);

        var grading = EnsureOverride<ColorAdjustments>(profile);
        grading.saturation.Override(10f);
        grading.contrast.Override(12f);
        grading.postExposure.Override(.2f);

        var vignette = EnsureOverride<Vignette>(profile);
        vignette.intensity.Override(.30f);
        vignette.smoothness.Override(.55f);

        var tonemapping = EnsureOverride<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);

        var volumeObject = new GameObject("Night Bloom | URP Global Volume");
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;

        var cameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
        if (!cameraData) cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("[Neon Highway] URP Bloom Volume configured.");
    }

    static T EnsureOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component;
        if (profile.TryGet<T>(out component)) return component;
        component = profile.Add<T>(true);
        if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    static void EnsureBuildSetting()
    {
        if(System.IO.File.Exists(ScenePath))
            EditorBuildSettings.scenes=new[] { new EditorBuildSettingsScene(ScenePath,true) };
    }
}
#endif
