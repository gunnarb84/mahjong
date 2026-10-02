using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mahjong.Game.Editor
{
    /// <summary>
    /// WebGL-Build ueber Menue "Mahjong/WebGL bauen":
    /// - schaltet die Build-Plattform auf WebGL (falls noetig)
    /// - setzt Kompression (Gzip) + Decompression-Fallback (laeuft auf jedem Host,
    ///   z. B. GitHub Pages, ohne Server-Konfiguration)
    /// - nutzt das Mahjong-Template (Ladebalken, dunkler Hintergrund, touch-action:none)
    /// - erzeugt bei Bedarf eine leere Szene (das Spiel bootet zur Laufzeit)
    /// Ausgabe: build/WebGL/ (neben unity/).
    /// </summary>
    public static class WebGlBuild
    {
        const string OutputDir = "../build/WebGL";
        const string EmptyScenePath = "Assets/Scenes/Empty.unity";

        [MenuItem("Mahjong/WebGL bauen")]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            PlayerSettings.productName = "Mahjong Solitaire";
            PlayerSettings.companyName = "Mahjong";

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true; // laeuft auf jedem Hoster
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.template = "PROJECT:Mahjong";

            // Android-Chrome kann den WASM-Heap nicht ueber ~256 MB hinaus WACHSEN
            // lassen (bekanntes Chromium-Problem: "memory access out of bounds"
            // beim Heap-Growth) — der Heap darf nur VORAB gross angelegt werden.
            // Deshalb: Initial-Heap ueber der Kante anlegen, Growth-Cap fuer Desktop.
            PlayerSettings.WebGL.initialMemorySize = 320;
            PlayerSettings.WebGL.maximumMemorySize = 768;

            // WebGL: immer IL2CPP/WASM (Debug-Code strippt der Pipeline-Standard).

            EnsureShaderIncluded("Standard"); // TileView erzeugt Materialien per Shader.Find
            EnsureResourcesMaterial("Mahjong/Outline", "Assets/Resources/Art/OutlineMat.mat");
            EnsureResourcesMaterial("Mahjong/Face", "Assets/Resources/Art/FaceMat.mat");
            EnsureMusicImportSettings();
            EnsureEmptyScene();

            var result = BuildPipeline.BuildPlayer(
                new[] { EmptyScenePath }, OutputDir, BuildTarget.WebGL,
                BuildOptions.None);

            Debug.Log("[Mahjong] WebGL-Build: " + OutputDir + " — "
                + (result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded
                    ? "erfolgreich"
                    : result.summary.result.ToString()));
        }

        /// <summary>
        /// Shader.Find liefert im Build null, wenn der Shader nirgends referenziert ist
        /// (im Editor funktioniert es immer) — deshalb hier in "Always Included
        /// Shaders" aufnehmen. Wirkt nur zur Buildzeit (Graphics-Settings frieren
        /// beim Build ein). Die API-Property existiert in Unity 6 nicht mehr, also
        /// direkt ueber das GraphicsSettings-Asset.
        /// </summary>
        static void EnsureShaderIncluded(string shaderName)
        {
            var shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogError("[Mahjong] Shader nicht gefunden: " + shaderName);
                return;
            }

            var graphicsSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "ProjectSettings/GraphicsSettings.asset");

            if (graphicsSettings == null)
            {
                Debug.LogError("[Mahjong] GraphicsSettings.asset nicht gefunden.");
                return;
            }

            var serialized = new SerializedObject(graphicsSettings);
            var array = serialized.FindProperty("m_AlwaysIncludedShaders");

            for (var i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    return; // schon enthalten
                }
            }

            array.InsertArrayElementAtIndex(array.arraySize);
            array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = shader;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[Mahjong] Shader 'Always Included' aufgenommen: " + shaderName);
        }

        /// <summary>
        /// Der Outline-Shader wird zur Laufzeit via Resources.Load bezogen (statt
        /// Shader.Find): Ein Material-Asset im Resources-Ordner referenziert den
        /// Shader, wodurch er garantiert in jedem Build enthalten ist. Der Weg
        /// ueber "Always Included Shaders" (GraphicsSettings per SerializedObject)
        /// war unzuverlaessig — die Aenderung persistierte nicht, Shader.Find
        /// lieferte im Build null und new Material(null) crashte WASM hart
        /// ("memory access out of bounds", schwarzes Brett).
        /// </summary>
        static void EnsureResourcesMaterial(string shaderName, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                return; // schon vorhanden
            }

            var shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogError("[Mahjong] Shader nicht gefunden: " + shaderName);
                return;
            }

            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(new Material(shader), path);
            AssetDatabase.SaveAssets();
            Debug.Log("[Mahjong] Material erzeugt: " + path);
        }

        /// <summary>Das Spiel baut alles zur Laufzeit — es braucht nur eine leere Szene.</summary>
        static void EnsureEmptyScene()
        {
            if (File.Exists(EmptyScenePath))
            {
                return;
            }

            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.SaveScene(scene, EmptyScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(EmptyScenePath, true),
            };
        }

        /// <summary>
        /// Musik-Import-Einstellungen fuer WebGL: WebGL kann kein Streaming-Audio
        /// (LoadType Streaming faellt dort auf Decompress zurueck), und Vorbis in
        /// gemaeessigter Qualitaet haelt die Build-Groesse klein — Ambient/loops
        /// hoert man da keinen Unterschied. Wird bei JEDEM Build erzwungen, damit
        /// neu hinzugekommene Tracks nicht mit Fallback-Einstellungen landen.
        /// </summary>
        static void EnsureMusicImportSettings()
        {
            const string musicDir = "Assets/Resources/Music";

            if (!Directory.Exists(musicDir))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(musicDir, "*.mp3"))
            {
                ApplyClipSettings(file);
            }

            foreach (var file in Directory.GetFiles(musicDir, "*.ogg"))
            {
                ApplyClipSettings(file);
            }
        }

        static void ApplyClipSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Mahjong] Kein Audio-Importer: " + path);
                return;
            }

            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 55f;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}