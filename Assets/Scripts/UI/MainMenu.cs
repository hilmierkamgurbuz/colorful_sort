using ColorfulSort.Content;
using ColorfulSort.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorfulSort.UI
{
    /// <summary>
    /// The menu screen. Today it is one button, and that is the whole point of it: without a
    /// way into the Game scene, everything Boot carries — the popup host and the project's
    /// single <c>EventSystem</c> — is unreachable, so the HUD's gear does nothing and no popup
    /// can open. This is the route.
    /// <para>
    /// It shows no coins, no hearts and no progress bar. Those read <c>Meta</c>'s economy,
    /// which does not exist yet; a counter that reads zero would be decoration standing in for
    /// a system, and the rest of the menu arrives with 4C after the progression slice.
    /// </para>
    /// <para>
    /// The button now says which level it opens — <em>LEVEL 1</em> — which it works out from the
    /// save's ordinal and the database, because <c>AttemptStarter</c> lives in the Game scene and
    /// does not exist while this screen is up. It still decides nothing: it reads the number and
    /// asks <c>Core</c> for a screen (rules/ui.md: a UI script does not load a scene itself).
    /// </para>
    /// <para>
    /// With no levels authored it says so and goes disabled, rather than opening a Game scene with
    /// nothing to build a board from. That is a designed state, not a fallback: it is exactly where
    /// a project sits before its first level, and where a mis-typed level file puts it later (D-086).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField]
        private Button playButton;

        [Tooltip("The number on the button. Its wording comes from the style config, never from here.")]
        [SerializeField]
        private TextMeshProUGUI playLabel;

        [Tooltip("Where the levels are. The menu reads it to name the next one; it never plays it.")]
        [SerializeField]
        private LevelDatabase database;

        [Tooltip("The one place the reference's wording lives (D-020).")]
        [SerializeField]
        private UiStyleConfig style;

        private void OnEnable()
        {
            if (playButton == null)
            {
                Debug.LogError("[UI] " + name + " has no play button; run Tools > Colorful Sort > Build UI.", this);
                return;
            }

            playButton.onClick.AddListener(Play);
            ShowNextLevel();
        }

        /// <summary>
        /// Puts the level the player would open next on the button.
        /// <para>
        /// Read once, when the screen comes up: the ordinal only moves when a level is finished, and
        /// finishing one means leaving this screen and coming back to it (recompute-timing).
        /// </para>
        /// <para>
        /// The ordinal is progression's, and the number on the button is the level's own plaque index
        /// — the database orders levels and never renumbers them (D-085), so the two are different
        /// things and this is the one place they meet.
        /// </para>
        /// </summary>
        private void ShowNextLevel()
        {
            if (playLabel == null || style == null)
            {
                return;
            }

            LevelDefinition next = NextLevel();

            if (next == null)
            {
                playLabel.text = style.MenuNoLevel;
                playButton.interactable = false;
                return;
            }

            playLabel.text = style.MenuLevelFor(next.LevelIndex);
            playButton.interactable = true;
        }

        private LevelDefinition NextLevel()
        {
            if (database == null)
            {
                return null;
            }

            GameRoot root = GameRoot.Instance;
            int ordinal = root == null || root.Save == null || root.Save.Data == null
                ? 0
                : root.Save.Data.currentLevelOrdinal;

            return database.ByOrdinal(ordinal);
        }

        private void OnDisable()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(Play);
            }
        }

        private void Play()
        {
            GameRoot root = GameRoot.Instance;

            if (root == null)
            {
                Debug.LogWarning("[UI] Play needs the scene flow, and there is no GameRoot: " +
                                 "this scene was entered without Boot. Press Play from Boot.unity.", this);
                return;
            }

            // No guard against a second press: SceneFlowService already refuses a request that
            // arrives mid-swap and says so. Re-checking here would be a second opinion about a
            // state Core owns.
            root.Scenes.ShowGame();
        }
    }
}
