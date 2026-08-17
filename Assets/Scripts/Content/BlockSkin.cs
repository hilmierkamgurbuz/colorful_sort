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

        [Tooltip("The brick material for this colour, from Art/Materials/.")]
        [SerializeField]
        private Material material;

        // A visible placeholder rather than a tuning value: an unassigned colour reads
        // as white in the Inspector instead of as invisible black.
        [Tooltip("The same colour as a flat value, for UI that cannot use the material — the cover stripe, a HUD pip.")]
        [SerializeField]
        private Color uiColour = Color.white;

        public Mesh SymbolMesh => symbolMesh;

        public Material Material => material;

        public Color UiColour => uiColour;

        /// <summary>True once this skin can actually be spawned.</summary>
        public bool IsAssigned => symbolMesh != null && material != null;
    }
}
