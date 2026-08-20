#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorfulSort.EditorTools
{
    /// <summary>
    /// Makes the popup prefabs editable by double-clicking them.
    /// <para>
    /// `Popup_Pause`, `Popup_Win` and `Popup_Fail` exist only as prefabs — `PopupHost`
    /// instantiates them, and nothing keeps a disabled copy sitting in a scene, because one
    /// place deciding what is on screen is the whole point of having a host. The cost is that
    /// Prefab Mode opens them with no canvas around them, so an 880×900 panel floats at a size
    /// that means nothing and cannot be laid out against anything.
    /// </para>
    /// <para>
    /// Unity's own answer is a <em>prefab editing environment</em>: a scene it opens UI prefabs
    /// inside. This builds one and assigns it. Afterwards a double-click shows the popup at the
    /// proportions it will actually have, and the drag-into-a-scene / Apply / delete dance is
    /// no longer needed.
    /// </para>
    /// <para>
    /// The scene lives under <c>Assets/Editor/</c>, so it is in no build, and it is deliberately
    /// absent from the blueprint's scene inventory: that inventory describes what the game
    /// loads, and this is a tool.
    /// </para>
    /// </summary>
    public static class UiPrefabEnvironmentSetup
    {
        private const string EnvironmentFolder = "Assets/Editor/PrefabEnvironments";
        private const string EnvironmentScene = EnvironmentFolder + "/UI.unity";
        private const string BootScene = "Assets/Scenes/Boot.unity";

        /// <summary>Any sorting order will do — nothing else is in this scene.</summary>
        private const int EnvironmentSortingOrder = 0;

        [MenuItem("Tools/Colorful Sort/Set Up UI Prefab Editing")]
        public static void SetUp()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[Colorful Sort] Cancelled: there are unsaved scene changes.");
                return;
            }

            EnsureFolder("Assets/Editor", "PrefabEnvironments");

            bool created = false;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentScene) == null)
            {
                CreateEnvironmentScene();
                created = true;
            }

            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentScene);

            if (asset == null)
            {
                Debug.LogError("[Colorful Sort] " + EnvironmentScene + " could not be created, so Prefab Mode was left as it was.");
                return;
            }

            if (EditorSettings.prefabUIEnvironment == asset)
            {
                Debug.Log("[Colorful Sort] UI prefab editing is already set up" + (created ? " (scene rebuilt)." : "."));
                return;
            }

            // Unity's own project setting, written through Unity's own API. It is per-project,
            // so it follows this checkout rather than one machine.
            EditorSettings.prefabUIEnvironment = asset;
            AssetDatabase.SaveAssets();

            Debug.Log("[Colorful Sort] UI prefab editing is set up: double-clicking a UI prefab now opens it inside " +
                      EnvironmentScene + ". Try Assets/Prefabs/UI/Popup_Pause.prefab.", asset);
        }

        /// <summary>
        /// One canvas and nothing else. Its settings come from <see cref="UiFactory.EnsureCanvas"/>
        /// rather than from numbers typed here, so this scene shows a popup at the size the game
        /// will give it — and keeps doing so if those settings ever change.
        /// </summary>
        private static void CreateEnvironmentScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvas = new GameObject("--UI--");
            UiFactory.EnsureCanvas(canvas, EnvironmentSortingOrder);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, EnvironmentScene);
            AssetDatabase.Refresh();

            // Building it left it open, and pressing Play in a scene with nothing but a canvas
            // is the same dead end that leaving a screen scene open creates. Boot is the only
            // scene worth being in.
            EditorSceneManager.OpenScene(BootScene, OpenSceneMode.Single);
        }

        private static void EnsureFolder(string parent, string folder)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + folder))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif
