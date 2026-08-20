using ColorfulSort.Core;
using ColorfulSort.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace ColorfulSort.UI
{
    /// <summary>
    /// The board has no legal move left. Retry re-deals, Quit goes back to the menu — and no
    /// close cross, for the same reason the Win popup has none: dismissing it would leave the
    /// player on a board that cannot be played.
    /// <para>
    /// It costs nothing. What a deadlock should cost is `OPEN-3` — the current assumption is
    /// one heart, with the restart from this popup free — and hearts are `Meta`'s economy in
    /// task 5. A popup that spent a currency nobody owns yet would be this class deciding
    /// progression policy, which is exactly what rules/ui.md forbids.
    /// </para>
    /// </summary>
    public sealed class FailPopup : Popup
    {
        [SerializeField]
        private Button retryButton;

        [SerializeField]
        private Button quitButton;

        /// <summary>Who to ask for a re-deal. Handed over by the screen that opened this popup.</summary>
        private AttemptStarter attempt;

        /// <summary>Supplies the retry target. Nothing else in this popup depends on it.</summary>
        public void Bind(AttemptStarter startedAttempt)
        {
            attempt = startedAttempt;
        }

        private void OnEnable()
        {
            if (retryButton == null || quitButton == null)
            {
                Debug.LogError("[UI] " + name + " is missing a button; run Tools > Colorful Sort > Build UI.", this);
                return;
            }

            retryButton.onClick.AddListener(Retry);
            quitButton.onClick.AddListener(Quit);
        }

        private void OnDisable()
        {
            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(Retry);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(Quit);
            }
        }

        private void Retry()
        {
            if (attempt == null)
            {
                Debug.LogError("[UI] " + name + " has nothing to retry; whoever opened it did not call Bind.", this);
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

            root.Scenes.ShowMenu();
            Close();
        }
    }
}
