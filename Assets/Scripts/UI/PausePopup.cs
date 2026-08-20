using ColorfulSort.Core;
using ColorfulSort.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorfulSort.UI
{
    /// <summary>
    /// The gear's popup: restart, the two toggles, continue, quit, and the player id the
    /// reference shows at the bottom (reference §4).
    /// <para>
    /// It performs none of those things. Restart is a command to <c>Meta</c>, quit is a
    /// command to <c>Core</c>, and the toggles are <see cref="SettingToggleButton"/>s that
    /// write through <c>GameRoot</c>. This class holds no game state and decides no policy —
    /// which is the whole content of "a UI script does not load a scene, spend a currency or
    /// mutate a board itself" (rules/ui.md).
    /// </para>
    /// <para>
    /// Restore Purchase and Contact Us are in the reference layout and are deliberately
    /// absent: neither has anything behind it in this project, and a button that does nothing
    /// is worse than one that is not there.
    /// </para>
    /// </summary>
    public sealed class PausePopup : Popup
    {
        [SerializeField]
        private Button continueButton;

        [SerializeField]
        private Button restartButton;

        [SerializeField]
        private Button quitButton;

        [SerializeField]
        private Button closeButton;

        [Tooltip("The player id from the save file, as the reference shows it.")]
        [SerializeField]
        private TMP_Text playerIdLabel;

        /// <summary>
        /// Who to ask for a restart. Handed over by the screen that opened this popup, because
        /// a popup instantiated under Boot's persistent canvas has no other honest way to
        /// reach an object in the Game scene — and searching for one is the runtime lookup
        /// `reference-binding.md` calls the last resort.
        /// </summary>
        private AttemptStarter attempt;

        /// <summary>Supplies the restart target. Nothing else in this popup depends on it.</summary>
        public void Bind(AttemptStarter startedAttempt)
        {
            attempt = startedAttempt;
        }

        protected internal override void OnOpened()
        {
            GameRoot root = GameRoot.Instance;

            if (playerIdLabel != null && root != null)
            {
                playerIdLabel.text = root.Save.Data.playerId;
            }
        }

        private void OnEnable()
        {
            Listen(continueButton, Close);
            Listen(closeButton, Close);
            Listen(restartButton, Restart);
            Listen(quitButton, Quit);
        }

        private void OnDisable()
        {
            Silence(continueButton, Close);
            Silence(closeButton, Close);
            Silence(restartButton, Restart);
            Silence(quitButton, Quit);
        }

        private void Restart()
        {
            if (attempt == null)
            {
                Debug.LogError("[UI] " + name + " has nothing to restart; whoever opened it did not call Bind.", this);
                return;
            }

            attempt.Restart();
            Close();
        }

        private void Quit()
        {
            GameRoot root = GameRoot.Instance;

            if (root == null)
            {
                Debug.LogWarning("[UI] Quit needs the scene flow, and there is no GameRoot: " +
                                 "this scene was entered without Boot.", this);
                return;
            }

            // The board is not saved, so quitting a level abandons it and it restarts next
            // time (D-008). Nothing has to be torn down here — Core unloads the Game scene.
            root.Scenes.ShowMenu();
            Close();
        }

        private void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                Debug.LogError("[UI] " + name + " is missing a button; run Tools > Colorful Sort > Build UI.", this);
                return;
            }

            button.onClick.AddListener(action);
        }

        private void Silence(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }
    }
}
