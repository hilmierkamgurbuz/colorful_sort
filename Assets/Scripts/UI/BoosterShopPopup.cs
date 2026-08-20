using ColorfulSort.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorfulSort.UI
{
    /// <summary>
    /// The popup behind a booster's <c>+</c>: what this booster does, what a pack of it holds,
    /// and what it costs. One prefab for all three boosters — which one it is showing is chosen
    /// at runtime, which is exactly why its title, its line of copy and its button's wording
    /// live in <see cref="UiStyleConfig"/> while the popup's fixed furniture is baked into the
    /// prefab (rules/ui.md, D-020).
    /// <para>
    /// It sells nothing itself. The price, whether the purse covers it and what the purchase
    /// does to the save are `Meta`'s answers, asked through the attempt seam — a popup that
    /// compared a balance to a price would be `UI` deciding economy policy, the same line
    /// `WinPopup` does not cross when it asks for the next level.
    /// </para>
    /// <para>
    /// There is deliberately no rewarded-ad half of this screen. The reference offers one, this
    /// project has no ad system (fingerprint.md → Network model), and a button with nothing
    /// behind it is what Pause's Restore Purchase and Contact Us were dropped for.
    /// </para>
    /// </summary>
    public sealed class BoosterShopPopup : Popup
    {
        [SerializeField]
        private TMP_Text titleLabel;

        [SerializeField]
        private TMP_Text blurbLabel;

        [Tooltip("The booster's picture. Handed over by the button that opened this, so one icon dresses both.")]
        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Button buyButton;

        [Tooltip("The buy button's caption — 'Get +3'.")]
        [SerializeField]
        private TMP_Text buyLabel;

        [Tooltip("The price under the caption.")]
        [SerializeField]
        private TMP_Text priceLabel;

        [SerializeField]
        private Button closeButton;

        [Tooltip("The coin pill at this popup's top-left. It is the only place in the game that shows a balance.")]
        [SerializeField]
        private CoinHud coinHud;

        private AttemptStarter attempt;

        private UiStyleConfig style;

        private BoosterId booster;

        /// <summary>
        /// Supplies everything this popup shows: who to buy from, where the words are, which
        /// booster it is and the picture the bar is already wearing for it. Called by the screen
        /// that opened it, because a popup under Boot's canvas has no honest way to find any of
        /// those in the Game scene.
        /// </summary>
        public void Bind(AttemptStarter startedAttempt, UiStyleConfig uiStyle, BoosterId which, Sprite icon)
        {
            attempt = startedAttempt;
            style = uiStyle;
            booster = which;

            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
            }

            Render();
        }

        private void OnEnable()
        {
            if (buyButton == null || closeButton == null)
            {
                Debug.LogError("[UI] " + name + " has no buy or close button; run Tools > Colorful Sort > Build UI.", this);
                return;
            }

            buyButton.onClick.AddListener(Buy);
            closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            if (buyButton != null)
            {
                buyButton.onClick.RemoveListener(Buy);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }
        }

        /// <summary>
        /// Draws the offer. Everything on screen is read from an authority — the copy from the
        /// style config, the pack and its price from `Meta` — so there is no number and no
        /// sentence stored in this component.
        /// </summary>
        private void Render()
        {
            if (attempt == null || style == null)
            {
                Debug.LogError("[UI] " + name + " was opened without being bound, so it has nothing to sell.", this);
                return;
            }

            // The balance belongs on this screen and on no other: the gameplay HUD and the menu
            // deliberately show none, because a counter you cannot spend from is decoration
            // (D-092). Here it is the number the price is judged against.
            if (coinHud != null)
            {
                coinHud.Show(attempt.Coins);
            }

            if (titleLabel != null)
            {
                titleLabel.text = style.BoosterTitleFor(booster);
            }

            if (blurbLabel != null)
            {
                blurbLabel.text = style.BoosterBlurbFor(booster);
            }

            BoosterOffer offer = attempt.OfferFor(booster);

            if (offer == null)
            {
                // The config prices no pack for this booster. `Meta` has already said so at
                // volume; here it means the button must not look pressable.
                if (buyButton != null)
                {
                    buyButton.interactable = false;
                }

                return;
            }

            if (buyLabel != null)
            {
                buyLabel.text = style.BuyLabelFor(offer.Charges);
            }

            if (priceLabel != null)
            {
                priceLabel.text = style.CoinAmount(offer.Price);
            }

            if (buyButton != null)
            {
                // Sprite Swap gives the disabled state its own drawing (rules/ui.md), so "you
                // cannot afford this" is a look the pack already ships rather than a tint.
                buyButton.interactable = attempt.CanBuy(booster);
            }
        }

        private void Buy()
        {
            if (attempt == null)
            {
                Debug.LogError("[UI] " + name + " has nothing to buy from; whoever opened it did not call Bind.", this);
                return;
            }

            if (attempt.TryBuyBooster(booster))
            {
                // The charges are in the save and the bar re-reads itself from the same event
                // that banked them, so there is nothing left for this popup to be open for.
                Close();
                return;
            }

            // Refused — the purse moved under the player, or the offer is missing. Re-drawing is
            // what makes the button honest about it instead of leaving a live button that fails.
            Render();
        }
    }
}
