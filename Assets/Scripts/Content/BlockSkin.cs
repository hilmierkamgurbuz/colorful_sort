using UnityEngine;

namespace ColorfulSort.Content
{
    /// <summary>
    /// What one logical colour looks like: the embossed symbol mesh and the brick
    /// material. Which colour id this belongs to is <em>not</em> stored here — the
    /// mapping lives in <see cref="BlockSkinSet"/>, so there is exactly one place that
    /// decides what a colour looks like (D-003), and re-skinning the cat into a moon
    /// is repointing one slot in that set.
    /// </summary>
    [CreateAssetMenu(fileName = "Skin_Symbol", menuName = "Colorful Sort/Block Skin")]
    public sealed class BlockSkin : ScriptableObject
    {
        [Tooltip("The brick mesh carrying this skin's embossed symbol, from Art/Models/Blocks/.")]
        [SerializeField]
        private Mesh symbolMesh;

        [Tooltip("The symbol's faces on their own, for the sparks a landing throws. Generated from the brick mesh by Create Block Skins.")]
        [SerializeField]
        private Mesh sparkMesh;

        [Tooltip("The brick material for this colour, from Art/Materials/.")]
        [SerializeField]
        private Material material;

        [Tooltip("The engraved symbol's material — the same colour, darker. Generated, like the brick's own.")]
        [SerializeField]
        private Material symbolMaterial;

        // A visible placeholder rather than a tuning value: an unassigned colour reads
        // as white in the Inspector instead of as invisible black.
        [Tooltip("The same colour as a flat value, for UI that cannot use the material — the cover stripe, a HUD pip.")]
        [SerializeField]
        private Color uiColour = Color.white;

        public Mesh SymbolMesh => symbolMesh;

        /// <summary>
        /// The engraved symbol lifted out on its own — centred, one unit across and already turned to
        /// face the camera — so a burst of particles can be shaped like it.
        /// <para>
        /// It is a *second* mesh and not a renaming of the first: despite its name,
        /// <see cref="SymbolMesh"/> is the whole brick, with the symbol as its second submesh. That
        /// name is older than this field and is left alone rather than rippled through twelve assets
        /// and the factory that writes them.
        /// </para>
        /// <para>
        /// Optional: a skin with no spark mesh simply throws no sparks. It is deliberately not part of
        /// <see cref="IsAssigned"/>, because a brick with no spark mesh still draws perfectly and
        /// refusing to spawn it would turn a missing effect into a missing board.
        /// </para>
        /// </summary>
        public Mesh SparkMesh => sparkMesh;

        public Material Material => material;

        /// <summary>
        /// What the engraved symbol is painted with. The mesh carries the symbol's faces in their
        /// own material slot, so this is a second material rather than a second colour on the
        /// brick's own: left to one material, the symbol is the brick's colour and reads as nothing
        /// but a faint shadow (D-052).
        /// </summary>
        public Material SymbolMaterial => symbolMaterial;

        public Color UiColour => uiColour;

        /// <summary>True once this skin can actually be spawned.</summary>
        public bool IsAssigned => symbolMesh != null && material != null && symbolMaterial != null;
    }
}
