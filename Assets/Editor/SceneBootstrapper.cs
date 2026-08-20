#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using ColorfulSort.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ColorfulSort.EditorTools
{
    /// <summary>
    /// Builds the three scenes the blueprint asks for, in the shape it asks for, and
    /// retires the Unity 3D-template content that <c>SampleScene</c> belongs to.
    /// <para>
    /// A script rather than a click-through, because a scene skeleton is a decision
    /// (which roots exist, which object is persistent, what order Build Settings has)
    /// and a decision should be re-runnable and reviewable. It is idempotent: a scene
    /// that already exists is left exactly as it is, so running this again after task 2
    /// has furnished the Game scene destroys nothing.
    /// </para>
    /// </summary>
    public static class SceneBootstrapper
    {
        private const string ScenesFolder = "Assets/Scenes";

        /// <summary>
        /// What a freshly created <c>DisplayConfig</c> starts at. A seed and not the authority: the
        /// asset is the authority the moment it exists, and this is only what a brand-new one is
        /// born holding. 120 is a ceiling — a 60 Hz screen still gives 60.
        /// </summary>
        private const int SeedTargetFrameRate = 120;

        /// <summary>
        /// What the Unity 3D template shipped and the blueprint's <c>TemplateLeftovers</c>
        /// system covers. Deleted through <c>AssetDatabase</c> so the <c>.meta</c> files go
        /// with them and the asset database does not keep a dangling entry.
        /// </summary>
        private static readonly string[] TemplateLeftovers =
        {
            "Assets/Scenes/SampleScene.unity",
            "Assets/TutorialInfo",
            "Assets/Readme.asset",
        };

        [MenuItem("Tools/Colorful Sort/Bootstrap Scenes")]
        public static void BootstrapScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[Colorful Sort] Bootstrap cancelled: there are unsaved scene changes.");
                return;
            }

            EnsureScenesFolder();

            List<string> created = new List<string>();

            foreach (string sceneName in SceneNames.BuildOrder)
            {
                string path = ScenePath(sceneName);

                if (File.Exists(path))
                {
                    continue;
                }

                CreateScene(sceneName, path);
                created.Add(sceneName);
            }

            SetBuildSettings();

            // Boot is opened before anything is deleted: SampleScene must not be the open
            // scene when it goes, and Boot is where the user presses Play afterwards.
            EditorSceneManager.OpenScene(ScenePath(SceneNames.Boot), OpenSceneMode.Single);

            List<string> removed = DeleteTemplateLeftovers();

            string displayReport = EnsureDisplayConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Colorful Sort] Bootstrap: created " + Describe(created) +
                      "; Build Settings = Boot, Menu, Game (Boot first); removed " + Describe(removed) +
                      "; " + displayReport + ".");
        }

        private static void CreateScene(string sceneName, string path)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Root objects in the blueprint's order: --Systems--, --Camera--, --Board--, --UI--.
            switch (sceneName)
            {
                case SceneNames.Boot:
                    BuildBootRoots();
                    break;
                case SceneNames.Menu:
                    CreateScreenCamera();
                    new GameObject("--UI--");
                    break;
                case SceneNames.Game:
                    CreateScreenCamera();
                    new GameObject("--Board--");
                    new GameObject("--UI--");
                    break;
            }

            EditorSceneManager.SaveScene(scene, path);
        }

        /// <summary>
        /// Boot holds one root and it is the persistent one. <c>GameRoot</c> sits on the
        /// root itself because <c>DontDestroyOnLoad</c> only keeps root objects, and the
        /// single <c>AudioListener</c> lives here too — one listener that outlives every
        /// screen swap beats one per screen camera.
        /// </summary>
        private static void BuildBootRoots()
        {
            GameObject systems = new GameObject("--Systems--");
            systems.AddComponent<GameRoot>();
            systems.AddComponent<AudioListener>();
        }

        /// <summary>
        /// A screen's camera: orthographic, because 1 logical cell = 1 unit and the board
        /// is drawn flat (fingerprint.md → Space model). The framing itself —
        /// <c>orthographicSize</c> — is deliberately left at Unity's default: it is a
        /// tuning number, so it belongs in <c>Data/Config/</c> and is BoardView's to drive
        /// (.claude/rules/data.md). Lighting is BoardView's too, once the brick materials
        /// arrive.
        /// </summary>
        private static void CreateScreenCamera()
        {
            GameObject cameraRoot = new GameObject("--Camera--");
            cameraRoot.tag = "MainCamera";

            // The conventional 2D stand-off, not a framing value: it decides what is in
            // front of the camera, not how much of it is visible.
            cameraRoot.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraRoot.AddComponent<Camera>();
            camera.orthographic = true;

            // URP adds this at runtime if it is missing; adding it here keeps the scene
            // honest about what the camera really carries.
            cameraRoot.AddComponent<UniversalAdditionalCameraData>();
        }

        private static void SetBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();

            foreach (string sceneName in SceneNames.BuildOrder)
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath(sceneName), true));
            }

            // Assigning the whole list is what removes SampleScene from a build.
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static List<string> DeleteTemplateLeftovers()
        {
            List<string> removed = new List<string>();

            foreach (string path in TemplateLeftovers)
            {
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                {
                    continue;
                }

                if (AssetDatabase.DeleteAsset(path))
                {
                    removed.Add(path);
                }
                else
                {
                    Debug.LogWarning("[Colorful Sort] '" + path + "' could not be deleted; delete it in the Project window.");
                }
            }

            return removed;
        }

        /// <summary>
        /// Makes sure the display config exists and that Boot's <c>GameRoot</c> is holding it.
        /// <para>
        /// The asset is <em>seeded</em>, not owned: a new one starts at
        /// <see cref="SeedTargetFrameRate"/> and an existing one is never touched, exactly as the
        /// block-skin factory seeds a colour map and then leaves the designer's colours alone (D-020).
        /// That is what keeps the number data rather than code — the runtime reads only the asset, and
        /// this constant is a starting value somebody is free to overwrite in the Inspector (D-100).
        /// </para>
        /// <para>
        /// The reference is written through <c>SerializedProperty</c> and only when it is empty, for
        /// the two reasons this whole tool exists on: a private field needs no public setter to be
        /// wired (D-032), and a bootstrapper that overwrites a furnished scene is a bootstrapper
        /// nobody dares run twice.
        /// </para>
        /// </summary>
        private static string EnsureDisplayConfig()
        {
            const string folder = "Assets/Data/Config";
            const string path = folder + "/DisplayConfig.asset";

            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("[Colorful Sort] " + folder + " is missing, so the display config was not created.");
                return "display config skipped";
            }

            DisplayConfig config = AssetDatabase.LoadAssetAtPath<DisplayConfig>(path);
            bool created = false;

            if (config == null)
            {
                config = ScriptableObject.CreateInstance<DisplayConfig>();

                SerializedObject asset = new SerializedObject(config);
                asset.FindProperty("targetFrameRate").intValue = SeedTargetFrameRate;
                asset.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.CreateAsset(config, path);
                created = true;
            }

            GameRoot root = Object.FindAnyObjectByType<GameRoot>();

            if (root == null)
            {
                return (created ? "display config created at " + SeedTargetFrameRate + " fps" : "display config already there") +
                       " but Boot has no GameRoot to hold it";
            }

            SerializedObject holder = new SerializedObject(root);
            SerializedProperty reference = holder.FindProperty("display");

            if (reference.objectReferenceValue != null)
            {
                return created ? "display config created at " + SeedTargetFrameRate + " fps" : "display config left as authored";
            }

            reference.objectReferenceValue = config;
            holder.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);

            return (created ? "display config created at " + SeedTargetFrameRate + " fps" : "display config found") +
                   " and wired to GameRoot";
        }

        private static void EnsureScenesFolder()
        {
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
        }

        private static string ScenePath(string sceneName)
        {
            return ScenesFolder + "/" + sceneName + ".unity";
        }

        private static string Describe(List<string> items)
        {
            return items.Count == 0 ? "nothing" : string.Join(", ", items.ToArray());
        }
    }
}
#endif
