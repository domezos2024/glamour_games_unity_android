using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GlamourGames.EditorTools
{
    /// <summary>
    /// Richtet das Projekt beim ersten Oeffnen automatisch ein: URP-Pipeline (HDR, 4x MSAA), Startszene,
    /// Build-Einstellungen und Player-Einstellungen fuer Android (Paketname, IL2CPP/ARM64, Querformat, Vulkan/GLES3,
    /// adaptive Icons, Linear-Farbraum). Laeuft idempotent bei jedem Laden der Skripte; manuell ueber das Menue "Glamour Games".
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectBootstrap
    {
        const string SettingsDir = "Assets/Glamour/Settings";
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string PipelinePath = SettingsDir + "/GlamourURP.asset";
        const string RendererPath = SettingsDir + "/GlamourURP_Renderer.asset";
        const string IconPath = "Assets/Glamour/Branding/icon.png";

        static ProjectBootstrap() { EditorApplication.delayCall += () => Setup(false); }

        [MenuItem("Glamour Games/Projekt einrichten", priority = 1)]
        static void SetupMenu() => Setup(true);

        static void Setup(bool verbose, bool force = false)
        {
            if (!force && (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)) { EditorApplication.delayCall += () => Setup(verbose); return; }
            try
            {
                EnsurePipeline();
                EnsureScene();
                EnsurePlayerSettings();
                if (verbose) Debug.Log("[Glamour] Projekt eingerichtet: URP, Szene, Player-Einstellungen.");
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        static void EnsurePipeline()
        {
            Directory.CreateDirectory(SettingsDir);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(rd, RendererPath);
                asset = UniversalRenderPipelineAsset.Create(rd);
                asset.supportsHDR = true;
                asset.msaaSampleCount = 4;
                asset.renderScale = 1f;
                AssetDatabase.CreateAsset(asset, PipelinePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Glamour] URP-Pipeline angelegt: " + PipelinePath);
            }
            // 64-Bit-HDR wie in der Windows-Version (keine Farbstufen in Verlaeufen); die adaptive Qualitaet (App) reagiert bei Bedarf
            if (asset.hdrColorBufferPrecision != HDRColorBufferPrecision._64Bits || asset.colorGradingMode != ColorGradingMode.HighDynamicRange) { asset.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits; asset.colorGradingMode = ColorGradingMode.HighDynamicRange; EditorUtility.SetDirty(asset); }
            if (GraphicsSettings.defaultRenderPipeline != asset) { GraphicsSettings.defaultRenderPipeline = asset; EditorUtility.SetDirty(asset); }
            int cur = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != null && QualitySettings.renderPipeline != asset) QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(cur, false);
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var active = SceneManager.GetActiveScene();
                bool emptyUntitled = string.IsNullOrEmpty(active.path) && !active.isDirty;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, emptyUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
                var cam = new GameObject("Main Camera") { tag = "MainCamera" };
                var c = cam.AddComponent<Camera>(); c.orthographic = true; c.orthographicSize = 450; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Color.black;
                cam.transform.position = new Vector3(0, 0, -10);
                if (!emptyUntitled) SceneManager.MoveGameObjectToScene(cam, scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (!emptyUntitled) EditorSceneManager.CloseScene(scene, true);
                else EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("[Glamour] Startszene angelegt: " + ScenePath);
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !SceneManager.GetActiveScene().isDirty) EditorSceneManager.OpenScene(ScenePath);
        }

        public const string ProductName = "Glamour Games Unity Android", PackageId = "de.domezosware.glamourgames.unityandroid";

        static void EnsurePlayerSettings()
        {
            if (PlayerSettings.productName != ProductName) PlayerSettings.productName = ProductName;
            if (PlayerSettings.companyName != "DoMeZos-Ware") PlayerSettings.companyName = "DoMeZos-Ware";
            if (PlayerSettings.bundleVersion != GlamourGames.App.Version) PlayerSettings.bundleVersion = GlamourGames.App.Version;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false; // Mobil: im Hintergrund nichts berechnen (Akku, Ton)
            PlayerSettings.muteOtherAudioSources = false;

            var nbt = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(nbt, PackageId);
            PlayerSettings.SetScriptingBackend(nbt, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(nbt, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(nbt, ManagedStrippingLevel.Low);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = HighestStableSdk(36);
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;   // randlos; die Spiel-UI bleibt per Screen.safeArea im sicheren Bereich
            PlayerSettings.Android.androidIsGame = true;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true; PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnitySplashLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(6 / 255f, 1 / 255f, 15 / 255f);
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
            EnsureIcons();
        }

        /// <summary>Hoechste installierte, stabile SDK-Stufe bis max (Unity-Enum kennt nicht jede Stufe).</summary>
        static AndroidSdkVersions HighestStableSdk(int max)
        {
            AndroidSdkVersions best = AndroidSdkVersions.AndroidApiLevel35;
            foreach (AndroidSdkVersions v in System.Enum.GetValues(typeof(AndroidSdkVersions))) { int n = (int)v; if (n > (int)best && n <= max) best = v; }
            return best;
        }

        const string IconDir = "Assets/Glamour/Branding/Android/";
        static void EnsureIcons()
        {
            var nbt = NamedBuildTarget.Android;
            Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + n);
            var bg = T("adaptive_bg.png"); var fg = T("adaptive_fg.png"); var legacy = T("legacy.png") ?? AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath); var round = T("round.png") ?? legacy;
            void Fill(PlatformIconKind kind, Texture2D a, Texture2D b)
            {
                if (a == null) return;
                var icons = PlayerSettings.GetPlatformIcons(nbt, kind);
                foreach (var ic in icons) { ic.SetTexture(a, 0); if (b != null && ic.GetTextures().Length > 1) ic.SetTexture(b, 1); }
                PlayerSettings.SetPlatformIcons(nbt, kind, icons);
            }
            Fill(AndroidPlatformIconKind.Adaptive, bg, fg);
            Fill(AndroidPlatformIconKind.Round, round, null);
            Fill(AndroidPlatformIconKind.Legacy, legacy, null);
        }

        [MenuItem("Glamour Games/Android-APK erstellen", priority = 20)]
        static void BuildApkMenu() => Build("Builds/Android/GlamourGamesUnityAndroid.apk", false, false);

        static bool Build(string path, bool bundle, bool development)
        {
            Setup(false, true);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            EditorUserBuildSettings.buildAppBundle = bundle;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.development = development;
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Glamour] Build: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB -> {opts.locationPathName}");
            bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            if (ok && !Application.isBatchMode) EditorUtility.RevealInFinder(opts.locationPathName);
            return ok;
        }

        /// <summary>
        /// Kommandozeilen-Build: Unity -batchmode -quit -buildTarget Android -projectPath . -executeMethod GlamourGames.EditorTools.ProjectBootstrap.CiBuild
        ///   [-buildPath Pfad.apk] [-aab] [-development] [-versionCode N]
        /// Signierung (optional, sonst Debug-Schluessel): Umgebungsvariablen GLAMOUR_KEYSTORE, GLAMOUR_KEYSTORE_PASS, GLAMOUR_KEY_ALIAS, GLAMOUR_KEY_PASS.
        /// Beendet Unity mit Exit-Code 1, wenn der Build fehlschlaegt.
        /// </summary>
        public static void CiBuild()
        {
            string path = "Builds/Android/GlamourGamesUnityAndroid.apk"; bool aab = false, dev = false;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-buildPath" && i + 1 < args.Length) path = args[i + 1];
                else if (args[i] == "-aab") aab = true;
                else if (args[i] == "-development") dev = true;
                else if (args[i] == "-versionCode" && i + 1 < args.Length && int.TryParse(args[i + 1], out var vc)) PlayerSettings.Android.bundleVersionCode = vc;
            }
            if (aab && path.EndsWith(".apk")) path = path.Substring(0, path.Length - 4) + ".aab";
            var ks = System.Environment.GetEnvironmentVariable("GLAMOUR_KEYSTORE");
            if (!string.IsNullOrEmpty(ks) && File.Exists(ks))
            {
                PlayerSettings.Android.useCustomKeystore = true; PlayerSettings.Android.keystoreName = ks;
                PlayerSettings.Android.keystorePass = System.Environment.GetEnvironmentVariable("GLAMOUR_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = System.Environment.GetEnvironmentVariable("GLAMOUR_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = System.Environment.GetEnvironmentVariable("GLAMOUR_KEY_PASS");
            }
            else PlayerSettings.Android.useCustomKeystore = false;
            bool ok = false;
            try { ok = Build(path, aab, dev); } catch (System.Exception e) { Debug.LogException(e); }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
