using System;
using ColorfulSort.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorfulSort.UI
{
    /// <summary>
    /// One of the three boosters in the gameplay bar. One component for all three, because the
    /// blueprint already plans one <c>BoosterButton</c> prefab with three instances — the only
    /// difference between them is which command they send and what they say.
    /// <para>
    /// It now carries a <em>charge</em>, and the badge is how the player reads it: a red count
    /// while there are charges left, a green <c>+</c> when there are none. What the press means
    /// follows the badge — with charges it sends the command, empty it asks for the shop — and
    /// that is the whole of the branch. The button spends nothing itself and prices nothing:
    /// the charge is taken in `Meta`, after `Board` has accepted the mutation (D-091,
    /// rules/ui.md).
    /// </para>
    /// <para>
    /// It decides nothing about legality either. Whether a board can take another column, or has
    /// two cells worth rearranging, is <c>Board</c>'s answer, read through the attempt seam — so
    /// the greyed-out button and the refused command can never disagree. An empty booster is the
    /// one case that ignores legality: the shop opens whatever the board thinks, because buying
    /// charges for later is not a move.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoosterButton : MonoBehaviour
    {
        [Tooltip("Which command this button sends (reference §3, left to right).")]
        [SerializeField]
        private BoosterId booster;

        [SerializeField]
        private Button button;

        [Tooltip("This booster's picture. The shop popup borrows it, so one icon dresses both.")]
        [SerializeField]
        private Image icon;

        [Header("Badges")]
        [Tooltip("The red disc with the remaining count on it. Shown only while there are charges.")]
        [SerializeField]
        private GameObject countBadge;

        [SerializeField]
        private TMP_Text countLabel;

        [Tooltip("The green plus. Shown only when the booster is empty, and pressing opens the shop.")]
        [SerializeField]
        private GameObject plusBadge;

        private AttemptStarter attempt;

        /// <summary>
        /// This button ran out and was pressed. The screen that owns the bar opens the shop —
        /// `GameplayHud` already owns which popup opens when, and a button that instantiated a
        /// popup itself would be a second place deciding that.
        /// </summary>
        public event Action<BoosterButton> ShopRequested;

        /// <summary>Which booster this is, so whoever opens the shop knows what to sell.</summary>
        public BoosterId Booster => booster;

        /// <summary>
        /// The picture this button wears. The shop popup is handed it rather than looking one up,
        /// which is what keeps a booster's icon in exactly one place — the prefab instance the
        /// user dressed (D-071).
        /// </summary>
        public Sprite Icon => icon == null ? null : icon.sprite;

        /// <summary>Supplies the attempt this button acts on, and renders its first state.</summary>
        public void Bind(AttemptStarter startedAttempt)
        {
            attempt = startedAttempt;
            Refresh();
        }

        /// <summary>
        /// Re-reads what is left and whether the command is available. Called when the board
        /// changed and when the purse did, never per frame: a button that asked <c>CanUndo</c>
        /// in <c>Update</c> is the defect rules/ui.md names by example.
        /// </summary>
        public void Refresh()
        {
            int charges = attempt == null ? 0 : attempt.ChargesOf(booster);
            bool empty = charges <= 0;

            if (countBadge != null)
            {
                countBadge.SetActive(!empty);
            }

            if (plusBadge != null)
            {
                plusBadge.SetActive(empty);
            }

            if (countLabel != null && !empty)
            {
                countLabel.text = charges.ToString();
            }

            if (button != null)
            {
                // Empty is always pressable, because what it opens is a shop rather than a move.
                button.interactable = empty ? attempt != null : IsAvailable();
            }
        }

        private void OnEnable()
        {
            if (button == null)
            {
                Debug.LogError("[UI] " + name + " has no button; run Tools > Colorful Sort > Build UI.", this);
                return;
            }

            button.onClick.AddListener(Press);
            Refresh();
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(Press);
            }
        }

        private bool IsAvailable()
        {
            if (attempt == null)
            {
                return false;
            }

            switch (booster)
            {
                case BoosterId.Undo:
                    return attempt.CanUndo;

                case BoosterId.AddColumn:
                    return attempt.CanAddColumn;

                case BoosterId.Shuffle:
                    return attempt.CanShuffle;

                default:
                    return false;
            }
        }

        private void Press()
        {
            if (attempt == null)
            {
                Debug.LogError("[UI] " + name + " has no attempt to act on; whoever built the bar did not call Bind.", this);
                return;
            }

            if (attempt.ChargesOf(booster) <= 0)
            {
                Action<BoosterButton> shop = ShopRequested;

                if (shop != null)
                {
                    shop(this);
                    return;
                }

                Debug.LogWarning("[UI] " + name + " is empty and nothing is listening for the shop, " +
                                 "so the press does nothing.", this);
                return;
            }

            switch (booster)
            {
                case BoosterId.Undo:
                    attempt.Undo();
                    break;

                case BoosterId.AddColumn:
                    attempt.AddColumn();
                    break;

                case BoosterId.Shuffle:
                    attempt.Shuffle();
                    break;
            }
        }
    }
}
