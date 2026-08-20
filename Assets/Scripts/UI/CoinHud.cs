using TMPro;
using UnityEngine;

namespace ColorfulSort.UI
{
    /// <summary>
    /// The coin pill: what the player has, written the way the style config says to write it.
    /// <para>
    /// It is not screen furniture. The gameplay screen and the menu deliberately show no
    /// balance — a counter the player cannot spend anything from is decoration — so the pill
    /// lives in the booster shop, which is the one place in the game where coins mean
    /// something (D-092). It sits at the popup's top-left, over the host's scrim.
    /// </para>
    /// <para>
    /// It holds no balance of its own and it animates nothing. Both are on purpose: the purse
    /// is `Meta`'s and this is handed a number (rules/ui.md), and the climb it used to do
    /// belonged to the coin flight, which went with it — a counter with nothing left to count
    /// in is a coroutine kept for a feature that no longer exists.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoinHud : MonoBehaviour
    {
        [Tooltip("Where the coin amount's format lives, so no text is assembled in C#.")]
        [SerializeField]
        private UiStyleConfig style;

        [SerializeField]
        private TMP_Text amountLabel;

        /// <summary>Draws a balance. The caller reads it from `Meta`; this only writes it down.</summary>
        public void Show(int coins)
        {
            if (amountLabel == null)
            {
                return;
            }

            if (style == null)
            {
                Debug.LogError("[UI] " + name + " has no style config, so it cannot format a balance; " +
                               "run Tools > Colorful Sort > Build UI.", this);
                return;
            }

            amountLabel.text = style.CoinAmount(coins);
        }
    }
}
