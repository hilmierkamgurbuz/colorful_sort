using System;
using ColorfulSort.Content;
using ColorfulSort.Meta;
using UnityEngine;

namespace ColorfulSort.UI
{
    /// <summary>
    /// The two things about the interface that code is not allowed to know: what text looks
    /// like, and what text says when the words are chosen at runtime.
    /// <para>
    /// The line between this asset and a prefab is the line <c>data-source.md</c> draws.
    /// Copy that never changes — a popup's title, the word on a button — is <em>baked</em>
    /// into the prefab that shows it, because baking something fixed is cheaper than looking
    /// it up and a second copy here would be a second authority. What lands here is what
    /// cannot be baked: the difficulty word is picked from an enum at runtime, and the plaque
    /// is a number the level supplies. Both need a table, and a table of text in C# is what
    /// the project's invariant forbids.
    /// </para>
    /// <para>
    /// The colours are the reference style (reference §6). They live here rather than on a
    /// material because the material is <em>generated</em> from them — one authority, one
    /// derived artefact, the same shape as a <c>BlockSkin</c>'s colour and its URP material
    /// (D-020). Re-styling every label in the game is an edit to this asset.
    /// </para>
    /// <para>
    /// No field carries a default. A fresh asset is invalid and <see cref="Validate"/> says
    /// so, rather than shipping a guessed look that nobody chose (rules/data.md, and the same
    /// reasoning as <c>BoardAnimationConfig</c>).
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "UiStyleConfig", menuName = "Colorful Sort/UI Style Config")]
    public sealed class UiStyleConfig : ScriptableObject
    {
        [Header("Text style")]
        [Tooltip("The face of every label. Reference: off-white #FFF6D6.")]
        [SerializeField]
        private Color textFill;

        [Tooltip("The outline every label carries. Reference: dark purple #4237A1.")]
        [SerializeField]
        private Color textOutline;

        [Tooltip("The soft drop shadow under every label.")]
        [SerializeField]
        private Color textShadow;

        [Tooltip("Outline thickness, in TextMeshPro's 0..1 signed-distance units.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float outlineWidth;

        [Tooltip("How far the shadow sits from the glyph, in the same 0..1 units.")]
        [SerializeField]
        private Vector2 shadowOffset;

        [Tooltip("How far the shadow's edge is blurred, in the same 0..1 units.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float shadowSoftness;

        [Header("Copy chosen at runtime")]
        [Tooltip("The level plaque. {0} is the level's own index — the number the player sees, not the ordinal Meta progresses by.")]
        [SerializeField]
        private string levelPlaqueFormat;

        [Tooltip("The menu's level button. {0} is the same index; the casing is authored here, not applied in code.")]
        [SerializeField]
        private string menuLevelFormat;

        [Tooltip("What the menu's level button reads when the level file holds nothing to play.")]
        [SerializeField]
        private string menuNoLevelText;

        [SerializeField]
        private string difficultyNormal;

        [SerializeField]
        private string difficultyHard;

        [SerializeField]
        private string difficultySuperHard;

        [Header("The booster shop's copy")]

        // Three titles and three blurbs rather than one string with a placeholder: they are
        // three different sentences about three different things, and a shared format would be
        // a template nobody can write good copy inside. They are here and not baked into the
        // popup because ONE popup prefab serves all three boosters — which of them it is
        // showing is chosen at runtime, and that is exactly the line this asset is on the far
        // side of.

        [Tooltip("The Extra Tube popup's title.")]
        [SerializeField]
        private string boosterTitleAddColumn;

        [Tooltip("The Undo popup's title.")]
        [SerializeField]
        private string boosterTitleUndo;

        [Tooltip("The Shuffle popup's title.")]
        [SerializeField]
        private string boosterTitleShuffle;

        [Tooltip("One line saying what an extra column does.")]
        [SerializeField]
        private string boosterBlurbAddColumn;

        [Tooltip("One line saying what undo does.")]
        [SerializeField]
        private string boosterBlurbUndo;

        [Tooltip("One line saying what shuffle does.")]
        [SerializeField]
        private string boosterBlurbShuffle;

        [Tooltip("The buy button's caption. {0} is how many charges the pack holds.")]
        [SerializeField]
        private string boosterBuyFormat;

        [Tooltip("How a coin amount is written, in the shop's pill and on a price. {0} is the number.")]
        [SerializeField]
        private string coinAmountFormat;

        [Tooltip("What the win popup says it paid. {0} is the coins; the plus sign is authored here, never added in code.")]
        [SerializeField]
        private string coinRewardFormat;

        public Color TextFill => textFill;

        public Color TextOutline => textOutline;

        public Color TextShadow => textShadow;

        public float OutlineWidth => outlineWidth;

        public Vector2 ShadowOffset => shadowOffset;

        public float ShadowSoftness => shadowSoftness;

        /// <summary>The plaque for one level, e.g. "Level 79".</summary>
        public string PlaqueFor(int levelIndex)
        {
            return string.Format(levelPlaqueFormat, levelIndex);
        }

        /// <summary>
        /// What the menu's level button reads, e.g. "LEVEL 79".
        /// <para>
        /// A second format beside the plaque's rather than one shared string, and upper-cased in the
        /// asset rather than in code: a button and a plaque are different things and this is exactly
        /// where they differ, and copy chosen at runtime belongs in the config (rules/ui.md, D-086).
        /// </para>
        /// </summary>
        public string MenuLevelFor(int levelIndex)
        {
            return string.Format(menuLevelFormat, levelIndex);
        }

        /// <summary>What the menu's level button reads when there is no level to play.</summary>
        public string MenuNoLevel => menuNoLevelText;

        /// <summary>
        /// The word for an authored difficulty. `Content` owns which one a level is
        /// (`DifficultyLabel`, authored and never derived, reference §4); this owns what
        /// that reads as on screen, which is why its own codemap note says the displayed
        /// strings belong to UI.
        /// </summary>
        public string LabelFor(DifficultyLabel difficulty)
        {
            switch (difficulty)
            {
                case DifficultyLabel.Normal:
                    return difficultyNormal;

                case DifficultyLabel.Hard:
                    return difficultyHard;

                case DifficultyLabel.SuperHard:
                    return difficultySuperHard;

                default:
                    // A new label was added to the enum and nobody gave it a word. That is a
                    // code/data mismatch, not a value to fall back from.
                    throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "No display string for this difficulty.");
            }
        }

        /// <summary>
        /// The shop popup's title for one booster. One prefab shows all three, so which words
        /// it carries is a runtime choice and therefore config, exactly like the difficulty
        /// word above (D-020).
        /// </summary>
        public string BoosterTitleFor(BoosterId booster)
        {
            switch (booster)
            {
                case BoosterId.AddColumn:
                    return boosterTitleAddColumn;

                case BoosterId.Undo:
                    return boosterTitleUndo;

                case BoosterId.Shuffle:
                    return boosterTitleShuffle;

                default:
                    throw new ArgumentOutOfRangeException(nameof(booster), booster, "No shop title for this booster.");
            }
        }

        /// <summary>The one line under the icon: what this booster actually does.</summary>
        public string BoosterBlurbFor(BoosterId booster)
        {
            switch (booster)
            {
                case BoosterId.AddColumn:
                    return boosterBlurbAddColumn;

                case BoosterId.Undo:
                    return boosterBlurbUndo;

                case BoosterId.Shuffle:
                    return boosterBlurbShuffle;

                default:
                    throw new ArgumentOutOfRangeException(nameof(booster), booster, "No shop blurb for this booster.");
            }
        }

        /// <summary>What the buy button reads, e.g. "Get +3".</summary>
        public string BuyLabelFor(int charges)
        {
            return string.Format(boosterBuyFormat, charges);
        }

        /// <summary>A coin amount as the player reads it — the shop's balance and a price both.</summary>
        public string CoinAmount(int coins)
        {
            return string.Format(coinAmountFormat, coins);
        }

        /// <summary>
        /// What the win popup says the level paid, e.g. "+20". The number is `Meta`'s — this
        /// owns only how it is written, which is why the plus sign is authored in the asset and
        /// not concatenated in code.
        /// </summary>
        public string CoinReward(int coins)
        {
            return string.Format(coinRewardFormat, coins);
        }

        /// <summary>
        /// Whether this asset has actually been authored. An invisible label — alpha zero,
        /// which is what a fresh `Color` field is — is the failure this catches: it looks
        /// like a layout bug for as long as it takes to notice the text is there.
        /// </summary>
        public bool Validate(out string error)
        {
            if (textFill.a <= 0f)
            {
                error = "Text fill is fully transparent; every label would be invisible.";
                return false;
            }

            if (textOutline.a <= 0f)
            {
                error = "Text outline is fully transparent; the reference style is an outlined face.";
                return false;
            }

            if (string.IsNullOrEmpty(levelPlaqueFormat) || !levelPlaqueFormat.Contains("{0}"))
            {
                error = "The level plaque format must contain {0}, or the plaque shows no number.";
                return false;
            }

            if (string.IsNullOrEmpty(menuLevelFormat) || !menuLevelFormat.Contains("{0}"))
            {
                error = "The menu level format must contain {0}, or the button shows no number.";
                return false;
            }

            if (string.IsNullOrEmpty(menuNoLevelText))
            {
                error = "The menu needs something to say when there is no level; a blank button reads as broken.";
                return false;
            }

            if (string.IsNullOrEmpty(difficultyNormal) ||
                string.IsNullOrEmpty(difficultyHard) ||
                string.IsNullOrEmpty(difficultySuperHard))
            {
                error = "Every difficulty needs a display string; a level authored with a blank label shows nothing.";
                return false;
            }

            if (string.IsNullOrEmpty(boosterTitleAddColumn) ||
                string.IsNullOrEmpty(boosterTitleUndo) ||
                string.IsNullOrEmpty(boosterTitleShuffle) ||
                string.IsNullOrEmpty(boosterBlurbAddColumn) ||
                string.IsNullOrEmpty(boosterBlurbUndo) ||
                string.IsNullOrEmpty(boosterBlurbShuffle))
            {
                error = "Every booster needs a shop title and a line saying what it does; " +
                        "the popup is one prefab and reads both from here.";
                return false;
            }

            if (string.IsNullOrEmpty(boosterBuyFormat) || !boosterBuyFormat.Contains("{0}"))
            {
                error = "The buy button format must contain {0}, or the button never says how many charges it gives.";
                return false;
            }

            if (string.IsNullOrEmpty(coinAmountFormat) || !coinAmountFormat.Contains("{0}"))
            {
                error = "The coin amount format must contain {0}, or the shop's pill shows no number at all.";
                return false;
            }

            if (string.IsNullOrEmpty(coinRewardFormat) || !coinRewardFormat.Contains("{0}"))
            {
                error = "The coin reward format must contain {0}, or the win popup never says what the level paid.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
