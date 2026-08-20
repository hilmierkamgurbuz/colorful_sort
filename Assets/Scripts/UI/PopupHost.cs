using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ColorfulSort.UI
{
    /// <summary>
    /// The one thing that puts a popup on screen and takes it off again. Popups are
    /// instantiated from prefabs and <em>stacked</em>; nothing is hand-toggled, and no popup
    /// decides what sits above it (rules/ui.md).
    /// <para>
    /// It is persistent, on Boot's <c>--UI--</c> root, for the same reason <c>GameRoot</c> is:
    /// <c>DontDestroyOnLoad</c> keeps root objects only. Living in Boot is also what lets a
    /// popup survive the additive Menu/Game swap — its canvas is <c>Screen Space - Overlay</c>,
    /// which needs no camera, so it does not care that the screen scene underneath it was
    /// unloaded and replaced.
    /// </para>
    /// <para>
    /// Its canvas sorts above the screen HUD (200 against 100), which is what makes "on top"
    /// a fact about the canvas rather than about the order somebody happened to add objects
    /// in. Within the canvas, order is sibling order and the scrim rides one place under the
    /// top popup — so the popup below is dimmed and, more importantly, untappable.
    /// </para>
    /// <para>
    /// The scrim is also what stops a tap on a popup from moving a brick underneath it: it is
    /// a raycast target covering the screen, and <c>BoardInput</c> refuses a press that lands
    /// on any UI graphic (D-037).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PopupHost : MonoBehaviour
    {
        [Tooltip("Where popups are parented. The scrim lives here too, one sibling under the top popup.")]
        [SerializeField]
        private RectTransform stack;

        [Tooltip("Full-screen raycast blocker. Active only while something is open.")]
        [SerializeField]
        private Image scrim;

        private readonly List<Popup> open = new List<Popup>();

        /// <summary>The live host. Null only before Boot's first frame.</summary>
        public static PopupHost Instance { get; private set; }

        /// <summary>Whether anything is on screen. The screen behind is untappable while this is true.</summary>
        public bool AnyOpen => open.Count > 0;

        /// <summary>
        /// Puts a popup on screen and returns the instance, so the caller can hand it
        /// whatever it needs before the player sees it. The caller is the screen that owns
        /// the button — which is how a popup living in Boot reaches an object in the Game
        /// scene without ever looking for one (reference-binding.md: inject, never find).
        /// </summary>
        public T Open<T>(T prefab) where T : Popup
        {
            if (prefab == null)
            {
                Debug.LogError("[UI] Something asked for a popup but handed over no prefab.", this);
                return null;
            }

            T popup = Instantiate(prefab, stack, false);
            popup.name = prefab.name;
            popup.CloseRequested += Close;
            open.Add(popup);

            Restack();
            popup.OnOpened();
            return popup;
        }

        /// <summary>Takes one popup down, wherever it sits in the stack.</summary>
        public void Close(Popup popup)
        {
            if (popup == null || !open.Remove(popup))
            {
                return;
            }

            popup.CloseRequested -= Close;
            popup.OnClosing();
            Destroy(popup.gameObject);

            Restack();
        }

        /// <summary>Takes down whatever is on top. Does nothing when nothing is open.</summary>
        public void CloseTop()
        {
            if (open.Count > 0)
            {
                Close(open[open.Count - 1]);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A second Boot was loaded. The first host keeps the stack; this one leaves,
                // exactly as GameRoot does with the save file.
                Destroy(gameObject);
                return;
            }

            if (transform.parent != null)
            {
                Debug.LogError("[UI] " + name + " is not a root object, so DontDestroyOnLoad cannot keep it: " +
                               "popups would die with the first scene swap. Move it to the top of the Boot hierarchy.", this);
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (stack == null || scrim == null)
            {
                Debug.LogError("[UI] " + name + " has no stack root or no scrim; run Tools > Colorful Sort > Build UI.", this);
                return;
            }

            scrim.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Sibling order is what "on top" means inside one canvas. The scrim goes up first
        /// and the top popup straight after it, so the scrim always separates the popup the
        /// player is using from everything it covers — including a popup underneath it.
        /// </summary>
        private void Restack()
        {
            if (open.Count == 0)
            {
                scrim.gameObject.SetActive(false);
                return;
            }

            scrim.gameObject.SetActive(true);
            scrim.rectTransform.SetAsLastSibling();
            open[open.Count - 1].transform.SetAsLastSibling();
        }
    }
}
