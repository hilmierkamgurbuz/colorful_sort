using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorfulSort.Core
{
    /// <summary>
    /// Swaps the one additive screen scene that sits over <c>Boot</c>: Menu or Game,
    /// never both. <c>Boot</c> itself is never unloaded — the services live there.
    /// <para>
    /// Load frequency is once per screen change, which the cost model prices at ×0.01,
    /// so there is nothing to optimise here and no need for a preload path. What does
    /// matter is that a second request cannot arrive mid-swap: a double-tapped level
    /// button would otherwise unload a scene that is still loading.
    /// </para>
    /// </summary>
    public sealed class SceneFlowService
    {
        private string pending;

        /// <summary>Raised once the requested scene is loaded and active.</summary>
        public event Action<string> ScreenChanged;

        /// <summary>The loaded screen scene, or null before the first one arrives.</summary>
        public string ActiveScreen { get; private set; }

        /// <summary>True while a swap is in flight. UI disables its buttons on this.</summary>
        public bool IsBusy { get; private set; }

        public void ShowMenu()
        {
            Show(SceneNames.Menu);
        }

        public void ShowGame()
        {
            Show(SceneNames.Game);
        }

        private void Show(string sceneName)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[Core] '" + sceneName + "' was requested while '" + pending +
                                 "' is still loading; the request was ignored.");
                return;
            }

            if (ActiveScreen == sceneName)
            {
                return;
            }

            pending = sceneName;
            IsBusy = true;

            if (ActiveScreen == null)
            {
                BeginLoad();
            }
            else
            {
                BeginUnload();
            }
        }

        private void BeginUnload()
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(ActiveScreen);

            if (unload == null)
            {
                // The scene is already gone (a manual unload, or a scene that never
                // finished loading). Nothing to wait for.
                ActiveScreen = null;
                BeginLoad();
                return;
            }

            unload.completed += _ =>
            {
                ActiveScreen = null;
                BeginLoad();
            };
        }

        private void BeginLoad()
        {
            string target = pending;
            AsyncOperation load = SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive);

            if (load == null)
            {
                Debug.LogError("[Core] Scene '" + target +
                               "' could not be loaded. It is missing from Build Settings — run Tools > Colorful Sort > Bootstrap Scenes.");
                pending = null;
                IsBusy = false;
                return;
            }

            load.completed += _ =>
            {
                ActiveScreen = target;
                pending = null;

                // Whatever the screen instantiates belongs to the screen, not to Boot,
                // so it unloads with it.
                Scene loaded = SceneManager.GetSceneByName(target);
                if (loaded.IsValid())
                {
                    SceneManager.SetActiveScene(loaded);
                }

                IsBusy = false;

                if (ScreenChanged != null)
                {
                    ScreenChanged(target);
                }
            };
        }
    }
}
