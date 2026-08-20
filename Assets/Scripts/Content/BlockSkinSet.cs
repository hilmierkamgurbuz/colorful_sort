using System;
using ColorfulSort.Board;
using UnityEngine;

namespace ColorfulSort.Content
{
    /// <summary>
    /// The one asset that maps a logical colour id to its look (D-003). Level data
    /// stores ids and nothing else, so a re-skin — the reference's cat becoming a
    /// moon — is repointing a slot here and touching nothing else in the project.
    /// <para>
    /// If a second place in the project also decided what a colour looks like, the
    /// single-writer invariant would be broken. This is that single place.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "BlockSkinSet", menuName = "Colorful Sort/Block Skin Set")]
    public sealed class BlockSkinSet : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            [Range(BlockColourId.MinId, BlockColourId.MaxId)]
            public int colourId;

            public BlockSkin skin;
        }

        private static readonly Entry[] NoEntries = new Entry[0];

        /// <summary>URP's base colour property, the one a brick material carries its colour in.</summary>
        private static readonly int BaseColourProperty = Shader.PropertyToID("_BaseColor");

        [Tooltip("One row per logical colour used by the levels. The id is what level data stores.")]
        [SerializeField]
        private Entry[] entries;

        [Tooltip("The ? brick: what a hidden cell looks like until it is revealed. Not a colour — every mystery cell shows this same skin, so it is one slot rather than a thirteenth row.")]
        [SerializeField]
        private BlockSkin hiddenSkin;

        [Tooltip("How much darker the engraved symbol is than the brick it is cut into. 0.65 reads on every colour; 1 would make it invisible.")]
        [Range(0.05f, 0.95f)]
        [SerializeField]
        private float symbolShade;

        // The one symbol colour in the project that is authored rather than derived, and the reason is
        // arithmetic: `symbolShade` darkens, and the `?` has to read LIGHTER than the near-black brick
        // it is cut into. No shade between 0 and 1 can produce that (D-099).
        [Tooltip("The ? mark's own colour. Authored, not derived: the shade above only darkens, and the ? has to be lighter than its dark brick.")]
        [SerializeField]
        private Color hiddenSymbolColour = Color.white;

        public int Count => entries == null ? 0 : entries.Length;

        /// <summary>
        /// The look of a cell whose colour the player cannot see yet. A hidden cell is not
        /// a colour id — <c>Board</c> keeps the real colour and simply reports the cell as
        /// hidden — so the `?` brick has exactly one appearance and it lives here rather
        /// than pretending to be a thirteenth entry.
        /// </summary>
        public BlockSkin HiddenSkin => hiddenSkin;

        /// <summary>
        /// What a symbol's colour is multiplied by. The symbol is a *derived* look, not an authored
        /// one: twelve hand-picked symbol colours would be twelve more chances for a brick and its
        /// own symbol to drift apart, which is the disagreement D-003 puts this asset in charge of
        /// preventing (D-052).
        /// </summary>
        public float SymbolShade => symbolShade;

        /// <summary>
        /// The `?` mark's colour. The single exception to "a symbol's colour is derived", and it is
        /// an exception on arithmetic rather than on taste: <see cref="SymbolShade"/> multiplies, so
        /// it can only ever darken, and the `?` is cut into a near-black brick where darker is
        /// invisible. Lighter is not reachable from a shade, so it is written down (D-099).
        /// <para>
        /// It lives here and not on <see cref="BlockSkin"/> deliberately. A field there would be
        /// twelve authorable symbol colours, which is exactly the drift D-052 closed; here it is one
        /// value for the one slot that was never a colour row to begin with (D-021).
        /// </para>
        /// </summary>
        public Color HiddenSymbolColour => hiddenSymbolColour;

        /// <summary>
        /// The colour a skin's engraved symbol is painted: derived from the skin's own for every
        /// colour row, and the authored <see cref="HiddenSymbolColour"/> for the `?` brick.
        /// <para>
        /// Both cases answer here rather than at the two call sites, because both call sites are
        /// asking the same question — the material generator writing the colour and
        /// <see cref="Validate"/> checking it — and a rule split across them is a rule that can
        /// disagree with itself.
        /// </para>
        /// </summary>
        public Color SymbolColour(BlockSkin skin)
        {
            if (skin == null)
            {
                throw new ArgumentNullException(nameof(skin));
            }

            if (skin == hiddenSkin)
            {
                return hiddenSymbolColour;
            }

            Color colour = skin.UiColour;

            return new Color(colour.r * symbolShade, colour.g * symbolShade, colour.b * symbolShade, colour.a);
        }

        /// <summary>
        /// Looks a colour's skin up. A linear scan over at most 12 rows, run once per
        /// block at spawn (≤128 blocks, once per level load): 12 integer comparisons
        /// against a dictionary's hashing and its own allocation — the cost model picks
        /// the readable one without hesitation.
        /// </summary>
        public bool TryGetSkin(BlockColourId colour, out BlockSkin skin)
        {
            Entry[] rows = entries ?? NoEntries;

            for (int row = 0; row < rows.Length; row++)
            {
                if (rows[row].colourId == colour.Value && rows[row].skin != null)
                {
                    skin = rows[row].skin;
                    return true;
                }
            }

            skin = null;
            return false;
        }

        /// <summary>
        /// The skin for a colour the level uses. A missing entry is a content defect,
        /// not a runtime condition to survive: a board would spawn invisible bricks.
        /// </summary>
        public BlockSkin GetSkin(BlockColourId colour)
        {
            BlockSkin skin;
            if (!TryGetSkin(colour, out skin))
            {
                throw new InvalidOperationException(name + " has no skin for " + colour + ".");
            }

            return skin;
        }

        public bool Validate(out string error)
        {
            Entry[] rows = entries ?? NoEntries;
            int seenIds = 0;

            // A fresh asset serialises this as 0, which the Inspector's slider cannot express and
            // which would paint every symbol black. Refused rather than defaulted, like every other
            // unauthored number in Data/ (rules/data.md).
            if (symbolShade <= 0f || symbolShade >= 1f)
            {
                error = "The symbol shade is " + symbolShade + "; it has to sit between 0 and 1 — at 0 a symbol is black, at 1 it is the brick's own colour.";
                return false;
            }

            // The whole point of this colour being authored is that it reads on a near-black brick, so
            // the one value it must not hold is the one an unset field holds. A `Color` serialises to
            // transparent black, which is exactly the invisible `?` this field exists to prevent
            // (rules/data.md, D-099).
            if (hiddenSymbolColour.a <= 0f)
            {
                error = "The ? mark's colour is fully transparent, so the ? would not be drawn at all. Set it in the Inspector — white is what it was designed as.";
                return false;
            }

            if (hiddenSymbolColour.maxColorComponent <= 0f)
            {
                error = "The ? mark's colour is black, which is invisible on the dark ? brick. It has to be LIGHTER than the brick, which is the reason this colour is authored instead of derived from the symbol shade.";
                return false;
            }

            for (int row = 0; row < rows.Length; row++)
            {
                int id = rows[row].colourId;

                if (id < BlockColourId.MinId || id > BlockColourId.MaxId)
                {
                    error = "Row " + row + " carries colour id " + id + ", which is outside " +
                            BlockColourId.MinId + ".." + BlockColourId.MaxId + ".";
                    return false;
                }

                int bit = 1 << id;
                if ((seenIds & bit) != 0)
                {
                    error = "Colour id " + id + " appears twice, so two rows would decide what it looks like.";
                    return false;
                }

                seenIds |= bit;

                if (rows[row].skin == null)
                {
                    error = "Colour id " + id + " has no skin assigned.";
                    return false;
                }

                if (!rows[row].skin.IsAssigned)
                {
                    error = "The skin for colour id " + id + " (" + rows[row].skin.name + ") is missing its mesh or material.";
                    return false;
                }

                if (!MaterialMatchesColour(rows[row].skin, out error))
                {
                    return false;
                }
            }

            if (hiddenSkin == null)
            {
                error = "The hidden ? brick has no skin, so a mystery cell would spawn nothing.";
                return false;
            }

            if (!hiddenSkin.IsAssigned)
            {
                error = "The hidden ? brick's skin (" + hiddenSkin.name + ") is missing its mesh or material.";
                return false;
            }

            if (!MaterialMatchesColour(hiddenSkin, out error))
            {
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// A skin's authored colour is the single authority for what that colour looks like,
        /// and its material is generated from it (Tools > Colorful Sort > Create Block Skins).
        /// A hand-edited material would quietly become a second authority — the brick on the
        /// board and the cover stripe beside it disagreeing — so a mismatch is a content
        /// defect, caught here.
        /// <para>
        /// A material whose shader has no base colour cannot be checked and is left alone:
        /// the rule exists to catch disagreement, not to demand a particular shader.
        /// </para>
        /// </summary>
        private bool MaterialMatchesColour(BlockSkin skin, out string error)
        {
            return MaterialIs(skin, skin.Material, skin.UiColour, "material", out error)
                   && MaterialIs(skin, skin.SymbolMaterial, SymbolColour(skin), "symbol material", out error);
        }

        /// <summary>
        /// One material against the colour it is supposed to carry. The symbol's expected colour is
        /// the skin's own times <see cref="SymbolShade"/>, so this check has to be told which of the
        /// two it is looking at — otherwise a correctly darkened symbol would report as a defect
        /// forever (D-052).
        /// </summary>
        private static bool MaterialIs(BlockSkin skin, Material material, Color expected, string role, out string error)
        {
            if (material == null || !material.HasProperty(BaseColourProperty))
            {
                error = null;
                return true;
            }

            Color materialColour = material.GetColor(BaseColourProperty);

            if (!SameColour(materialColour, expected))
            {
                error = skin.name + "'s " + role + " (" + material.name + ") is " + ToHex(materialColour) +
                        " while the skin says " + ToHex(expected) +
                        ". Re-run Tools > Colorful Sort > Create Block Skins to regenerate it from the skin.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Equal to the nearest 8-bit step, which is all a colour picker can express anyway.</summary>
        private static bool SameColour(Color left, Color right)
        {
            const float OneStep = 1f / 255f;

            return Mathf.Abs(left.r - right.r) <= OneStep &&
                   Mathf.Abs(left.g - right.g) <= OneStep &&
                   Mathf.Abs(left.b - right.b) <= OneStep;
        }

        private static string ToHex(Color colour)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(colour);
        }

        private void OnValidate()
        {
            if (Count == 0)
            {
                return;
            }

            string error;
            if (!Validate(out error))
            {
                Debug.LogWarning("[" + name + "] " + error, this);
            }
        }
    }
}
