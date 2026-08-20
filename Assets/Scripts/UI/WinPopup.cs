using ColorfulSort.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorfulSort.UI
{
    /// <summary>
    /// The level was solved. One button, and no close cross on purpose: a player who
    /// dismissed this would be left looking at a finished board with nothing left to do.
    /// <para>
    /// The button asks for the next level and nothing else. Whether there <em>is</em> one —
    /// and what happens when the database ends — is progression's answer, decided in
    /// `Meta` before this popup ever opens. A popup that checked "is this the last level"
    /// would be UI deciding progression policy, which is what rules/ui.md forbids.
    /// </para>
    /// </summary>
    public sealed class WinPopup : Popup
    {
        [SerializeField]
        private Button continueButton;

        [Tooltip("The coin and the amount, together. Hidden when the level paid nothing.")]
        [SerializeField]
        private GameObject rewardRow;

        [Tooltip("What the level paid, written as the style config says — the plus sign is authored, not added here.")]
        [SerializeField]
        private TMP_Text rewardLabel;

        /// <summary>Who to ask for the next level. Handed over by the screen that opened this popup.</summary>
        private AttemptStarter attempt;

        /// <summary>
        /// Supplies the progression seam and the words. The reward is <em>read</em>, never
        /// decided: `Meta` banked the coins the moment the level was solved and this only says
        /// how many (D-092). A replayed level pays nothing, and nothing is what the row shows —
        /// it goes away rather than reading "+0", which would look like a bug in the payout.
        /// </summary>
        public void Bind(AttemptStarter startedAttempt, UiStyleConfig style)
        {
            attempt = startedAttempt;

            int award = startedAttempt == null ? 0 : startedAttempt.LastWinAward;

            if (rewardRow != null)
            {
                rewardRow.SetActive(award > 0);
            }

            if (rewardLabel != null && style != null && award > 0)
            {
                rewardLabel.text = style.CoinReward(award);
            }
        }

        private void OnEnable()
        {
            if (continueButton == null)
            {
                Debug.LogError("[UI] " + name + " has no continue button; run Tools > Colorful Sort > Build UI.", this);
                return;
            }

            continueButton.onClick.AddListener(Continue);
        }

        private void OnDisable()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(Continue);
            }
        }

        private void Continue()
        {
            if (attempt == null)
            {
                Debug.LogError("[UI] " + name + " has nothing to continue into; whoever opened it did not call Bind.", this);
                return;
            }

            attempt.PlayNext();
            Close();
        }
    }
}
