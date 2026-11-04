using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameHeart;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace GameHeartEditor
{
    /// <summary>
    /// One-stop project setup and WebGL build for Game Heart: Rhythm of Life.
    /// Batchmode entry points:
    ///   -executeMethod GameHeartEditor.GameHeartBuilder.Setup
    ///   -executeMethod GameHeartEditor.GameHeartBuilder.BuildWebGL         (release)
    ///   -executeMethod GameHeartEditor.GameHeartBuilder.BuildWebGLDev      (development build)
    /// </summary>
    public static class GameHeartBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string FontSrc = "Tools/fonts";
        const string FontDir = "Assets/Fonts";
        const string FontOut = "Assets/Resources/Fonts";

        static readonly (string file, string asset, int size, string fallback)[] Fonts =
        {
            ("Inter-SemiBold.ttf", "Body", 72, null),
            ("Inter-Regular.ttf", "BodyLight", 72, null),
            ("Inter-Bold.ttf", "BodyBold", 72, null),
            ("Rajdhani-Bold.ttf", "Label", 72, "BodyBold"),
            ("Orbitron-Black.ttf", "Display", 90, "BodyBold"),
            ("Orbitron-Bold.ttf", "DisplayBold", 90, "BodyBold"),
        };

        const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "…—–‘’“”°·•×÷±²₂▲▼◀▶★☆♥✓✕←→↑↓é";

        [MenuItem("Game Heart/Setup Project")]
        public static void Setup()
        {
            ImportTmpEssentials();
            BuildFonts();
            ConfigurePlayer();
            ConfigureGraphics();
            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[GameHeartBuilder] Setup complete");
        }

        [MenuItem("Game Heart/Build WebGL")]
        public static void BuildWebGL() => Build(false);

        [MenuItem("Game Heart/Build WebGL (Development)")]
        public static void BuildWebGLDev() => Build(true);

        static void Build(bool dev)
        {
            Setup();
            string outDir = Environment.GetEnvironmentVariable("GH_BUILD_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = dev ? "Builds/WebGLDev" : "Builds/WebGL";
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = dev ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[GameHeartBuilder] Build {report.summary.result} size={report.summary.totalSize / (1024 * 1024)}MB " +
                      $"errors={report.summary.totalErrors} time={report.summary.totalTime}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        static void ImportTmpEssentials()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) return;
            string pkg = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(pkg))
            {
                string app = EditorApplication.applicationContentsPath;
                pkg = Path.Combine(app, "Resources/PackageManager/BuiltInPackages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            }
            Debug.Log("[GameHeartBuilder] Importing TMP essentials from " + pkg);
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void BuildFonts()
        {
            Directory.CreateDirectory(FontDir);
            Directory.CreateDirectory(FontOut);
            bool copied = false;
            foreach (var f in Fonts)
            {
                string dst = Path.Combine(FontDir, f.file);
                if (!File.Exists(dst)) { File.Copy(Path.Combine(FontSrc, f.file), dst); copied = true; }
            }
            if (copied) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var made = new Dictionary<string, TMP_FontAsset>();
            foreach (var f in Fonts)
            {
                string path = $"{FontOut}/{f.asset}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (existing != null) { made[f.asset] = existing; continue; }
                var font = AssetDatabase.LoadAssetAtPath<Font>(Path.Combine(FontDir, f.file));
                var fa = TMP_FontAsset.CreateFontAsset(font, f.size, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                fa.name = f.asset;
                AssetDatabase.CreateAsset(fa, path);
                fa.atlasTextures[0].name = f.asset + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = f.asset + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                fa.TryAddCharacters(Charset, out _);
                EditorUtility.SetDirty(fa);
                made[f.asset] = fa;
            }
            foreach (var f in Fonts)
            {
                if (f.fallback == null) continue;
                var fa = made[f.asset];
                if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!fa.fallbackFontAssetTable.Contains(made[f.fallback])) fa.fallbackFontAssetTable.Add(made[f.fallback]);
                EditorUtility.SetDirty(fa);
            }
            foreach (var f in Fonts)
            {
                string mp = $"{FontOut}/{f.asset} Glow.mat";
                if (File.Exists(mp)) continue;
                var fa = made[f.asset];
                var m = new Material(fa.material) { name = f.asset + " Glow" };
                m.EnableKeyword("GLOW_ON");
                m.SetColor(ShaderUtilities.ID_GlowColor, new Color(1f, 0.25f, 0.4f, 0.8f));
                m.SetFloat(ShaderUtilities.ID_GlowPower, 0.35f);
                m.SetFloat(ShaderUtilities.ID_GlowOuter, 0.6f);
                m.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
                m.SetFloat(ShaderUtilities.ID_GlowInner, 0.05f);
                AssetDatabase.CreateAsset(m, mp);
            }
            AssetDatabase.SaveAssets();
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "SPARC";
            PlayerSettings.productName = "Game Heart - Rhythm of Life";
            PlayerSettings.bundleVersion = "2.0." + DateTime.UtcNow.ToString("yyMMddHHmm");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.template = "PROJECT:GameHeart";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithoutStacktrace;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = true;
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (ps != null)
            {
                var so = new SerializedObject(ps);
                var input = so.FindProperty("activeInputHandler");
                if (input != null) { input.intValue = 0; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
        }

        static void ConfigureGraphics()
        {
            var pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            foreach (var asset in new[] { pc, mobile })
            {
                if (asset == null) continue;
                asset.supportsHDR = true;
                asset.msaaSampleCount = asset == pc ? 4 : 1;
                asset.renderScale = 1f;
                asset.shadowDistance = 0;
                asset.supportsCameraDepthTexture = false;
                asset.supportsCameraOpaqueTexture = false;
                EditorUtility.SetDirty(asset);
            }
            GraphicsSettings.defaultRenderPipeline = pc;
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            if (qs != null)
            {
                var so = new SerializedObject(qs);
                var levels = so.FindProperty("m_QualitySettings");
                for (int i = 0; i < levels.arraySize; i++)
                {
                    var lvl = levels.GetArrayElementAtIndex(i);
                    string name = lvl.FindPropertyRelative("name").stringValue;
                    var rp = lvl.FindPropertyRelative("customRenderPipeline");
                    if (rp != null) rp.objectReferenceValue = name == "Mobile" ? mobile : pc;
                    var vsync = lvl.FindPropertyRelative("vSyncCount");
                    if (vsync != null) vsync.intValue = 1;
                }
                var per = so.FindProperty("m_PerPlatformDefaultQuality");
                for (int i = 0; i < per.arraySize; i++)
                {
                    var e = per.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("first").stringValue == "WebGL")
                        e.FindPropertyRelative("second").intValue = 1;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
        }

        static void BuildScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("GameRoot");
                root.AddComponent<GameRoot>();
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.25f, 0.2f, 0.22f);
                RenderSettings.skybox = null;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }

    /// <summary>Import rules keyed by folder, so dropped-in art and audio get the right settings.</summary>
    public class GameHeartImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            if (assetPath.Contains("/Resources/Portraits/") || assetPath.Contains("/Resources/UI/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.Compressed;
            }
            else if (assetPath.Contains("/Resources/Models/"))
            {
                ti.maxTextureSize = 1024;
            }
        }

        void OnPreprocessAudio()
        {
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = assetPath.Contains("/Voice/") ? 0.8f : 0.7f;
            ai.defaultSampleSettings = s;
            ai.forceToMono = assetPath.Contains("/Voice/");
        }

        void OnPreprocessModel()
        {
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.isReadable = true;
        }
    }
}
