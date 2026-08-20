using System;
using ColorfulSort.Content;
using UnityEngine;

namespace ColorfulSort.View
{
    /// <summary>
    /// One brick on the board. There is a single <c>Block</c> prefab for the whole game
    /// (D-004): the symbol mesh and the colour arrive at spawn from the skin set, so
    /// re-skinning the cat into a moon never touches a prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlockView : MonoBehaviour
    {
        [SerializeField]
        private MeshFilter meshFilter;

        [SerializeField]
        private MeshRenderer meshRenderer;

        [Tooltip("The plume this brick leaves while it flies — soft puffs dropped along its path, in its own colour.")]
        [SerializeField]
        private ParticleSystem plume;

        /// <summary>
        /// The brick's two material slots, reused for every skin this brick ever wears. A field
        /// rather than a fresh array per call: a level opens with up to 128 bricks, and the
        /// <c>sharedMaterials</c> setter copies whatever it is handed anyway.
        /// </summary>
        private readonly Material[] materials = new Material[2];

        /// <summary>URP's base colour, the property a brick carries its colour in.</summary>
        private static readonly int BaseColourProperty = Shader.PropertyToID("_BaseColor");

        /// <summary>What this brick was last dressed as, so it can answer for itself.</summary>
        private BlockSkin applied;

        /// <summary>
        /// The skin this brick is currently wearing, or null before its first <see cref="Apply"/>.
        /// <para>
        /// A brick knowing what it is beats the column keeping a parallel list of what it put where:
        /// the burst that throws sparks in a symbol's shape needs that symbol, and the alternative was
        /// threading it down from the board through every call that already knows which brick it means.
        /// </para>
        /// </summary>
        public BlockSkin Skin => applied;


        /// <summary>
        /// One scratch block for every brick in the game. It is written and handed to a renderer
        /// inside a single call and never read back, so sharing it allocates nothing per brick —
        /// and there is no second thread here to race it.
        /// </summary>
        private static MaterialPropertyBlock shadeBlock;

        /// <summary>
        /// Puts a colour's look on this brick: the symbol mesh, the brick's material and the
        /// darker one its engraved symbol is painted with (D-052). The mesh keeps the symbol's
        /// faces in their own slot, so slot order here is slot order there — body first, symbol
        /// second.
        /// <para>
        /// The <em>shared</em> mesh and materials are assigned deliberately: touching
        /// <c>renderer.material</c> or <c>renderer.materials</c> would clone per brick, which
        /// multiplies twelve materials into two hundred and takes the batching with it.
        /// </para>
        /// </summary>
        public void Apply(BlockSkin skin)
        {
            if (skin == null)
            {
                throw new ArgumentNullException(nameof(skin));
            }

            if (meshFilter == null || meshRenderer == null)
            {
                Debug.LogError("[BoardView] " + name + " has no mesh filter or renderer assigned; rebuild the Block prefab with Tools > Colorful Sort > Build BoardView Prefabs.", this);
                return;
            }

            applied = skin;
            meshFilter.sharedMesh = skin.SymbolMesh;

            // A brick out of the pool may have been darkened in a settled column, and a property
            // block outlives the mesh and material it was set on. Clearing it here is what makes a
            // recycled brick always start at its own colour (D-057).
            SetShade(1f);

            // And a brick out of the pool appears somewhere else on the board. A plume still holding
            // its puffs would leave them hanging over a board nobody moved anything on, and a
            // distance-emitting system would lay a fresh line along the jump itself (D-074).
            ClearPlume();

            // A skin with no symbol material is refused by BlockSkinSet.Validate, so this is the
            // half-set-up project rather than a state to design for: the brick still draws, in one
            // colour, instead of leaving a submesh with whatever the last skin left behind.
            materials[0] = skin.Material;
            materials[1] = skin.SymbolMaterial ?? skin.Material;
            meshRenderer.sharedMaterials = materials;
        }

        /// <summary>
        /// Multiplies this brick's colours by <paramref name="shade"/> — under 1 darkens it, and 1
        /// puts it back exactly. Both material slots are shaded separately so the symbol keeps its
        /// own darker tint instead of collapsing into the body's colour.
        /// <para>
        /// Done with a <see cref="MaterialPropertyBlock"/> per slot, never by touching
        /// <c>renderer.material</c>: a brick shares one material with every other brick of its
        /// colour (D-004), and writing to that would clone twelve materials into a hundred and take
        /// the batching with it. The colour being multiplied is read from the material itself, so no
        /// colour is written down here and a re-skin carries the settled look with it (D-057).
        /// </para>
        /// <para>
        /// Returning to 1 clears the override rather than writing the original back, which is what
        /// makes it exact: there is no remembered copy to drift from the material.
        /// </para>
        /// </summary>
        public void SetShade(float shade)
        {
            if (meshRenderer == null)
            {
                return;
            }

            Material[] slots = meshRenderer.sharedMaterials;

            for (int slot = 0; slot < slots.Length; slot++)
            {
                if (Mathf.Approximately(shade, 1f))
                {
                    meshRenderer.SetPropertyBlock(null, slot);
                    continue;
                }

                Material material = slots[slot];

                if (material == null || !material.HasProperty(BaseColourProperty))
                {
                    continue;
                }

                Color colour = material.GetColor(BaseColourProperty);

                shadeBlock = shadeBlock ?? new MaterialPropertyBlock();
                shadeBlock.Clear();
                shadeBlock.SetColor(BaseColourProperty, new Color(colour.r * shade, colour.g * shade, colour.b * shade, colour.a));
                meshRenderer.SetPropertyBlock(shadeBlock, slot);
            }
        }

        /// <summary>
        /// Brightens the engraved symbol and nothing else — the shape in the middle burns while the
        /// brick is up, and the body keeps its colour (D-075).
        /// <para>
        /// It is the same mechanism as <see cref="SetShade"/> with the multiplier above 1 and only
        /// the symbol's slot written: the colour is read off the material, so no colour is stored
        /// here and a re-skin brings its own. Over 1 is deliberate — the pipeline renders to an HDR
        /// target and bloom turns the overshoot into light, which is exactly what D-066 bought.
        /// </para>
        /// <para>
        /// A brightness of 1 clears the override rather than writing the original back, so restoring
        /// is exact and a settle can shade the brick afterwards without inheriting a leftover.
        /// </para>
        /// </summary>
        public void SetSymbolGlow(float brightness)
        {
            if (meshRenderer == null)
            {
                return;
            }

            Material[] slots = meshRenderer.sharedMaterials;

            // Slot 1 is the symbol; a brick whose skin had no symbol material wears its body material
            // in both slots, and brightening that would light the whole brick.
            if (slots.Length < 2 || slots[1] == null || slots[1] == slots[0])
            {
                return;
            }

            if (Mathf.Approximately(brightness, 1f))
            {
                meshRenderer.SetPropertyBlock(null, 1);
                return;
            }

            if (!slots[1].HasProperty(BaseColourProperty))
            {
                return;
            }

            Color colour = slots[1].GetColor(BaseColourProperty);

            shadeBlock = shadeBlock ?? new MaterialPropertyBlock();
            shadeBlock.Clear();
            shadeBlock.SetColor(
                BaseColourProperty,
                new Color(colour.r * brightness, colour.g * brightness, colour.b * brightness, colour.a));
            meshRenderer.SetPropertyBlock(shadeBlock, 1);
        }

        /// <summary>
        /// Paints this brick's two slots outright — body and engraved symbol — instead of scaling
        /// whatever the materials already hold. What a reveal needs: it walks the brick from the `?`
        /// look to the real one, and the two ends belong to *different skins*, so there is nothing to
        /// scale from (D-101).
        /// <para>
        /// This is the same mechanism as <see cref="SetShade"/> and the same reason for it — a
        /// property block per slot, never <c>renderer.material</c>, because one material serves every
        /// brick of its colour and touching it would clone twelve into a hundred (D-004). The
        /// difference is only that the colour arrives from the caller rather than being read back and
        /// multiplied.
        /// </para>
        /// <para>
        /// A brick whose skin had no symbol material wears its body material in both slots; painting
        /// slot 1 then would paint the whole brick, so that case writes the body colour to both and
        /// the symbol simply never appears — which is what that half-set-up skin already looks like.
        /// </para>
        /// </summary>
        public void PaintReveal(Color body, Color symbol)
        {
            if (meshRenderer == null)
            {
                return;
            }

            Material[] slots = meshRenderer.sharedMaterials;

            if (slots.Length == 0)
            {
                return;
            }

            shadeBlock = shadeBlock ?? new MaterialPropertyBlock();

            shadeBlock.Clear();
            shadeBlock.SetColor(BaseColourProperty, body);
            meshRenderer.SetPropertyBlock(shadeBlock, 0);

            if (slots.Length < 2 || slots[1] == null || slots[1] == slots[0])
            {
                return;
            }

            shadeBlock.Clear();
            shadeBlock.SetColor(BaseColourProperty, symbol);
            meshRenderer.SetPropertyBlock(shadeBlock, 1);
        }

        /// <summary>
        /// Hands both slots back to their materials. The exact counterpart of
        /// <see cref="PaintReveal"/>, and exact for the same reason <see cref="SetShade"/> at 1 is:
        /// the override is *cleared* rather than written back, so there is no remembered copy that
        /// can drift from the material a re-skin brings.
        /// </summary>
        public void ClearReveal()
        {
            if (meshRenderer == null)
            {
                return;
            }

            Material[] slots = meshRenderer.sharedMaterials;

            for (int slot = 0; slot < slots.Length; slot++)
            {
                meshRenderer.SetPropertyBlock(null, slot);
            }
        }

        /// <summary>
        /// Starts the plume this brick leaves while it flies (D-074): soft puffs dropped along the
        /// path it takes, in a lighter tone of its own colour, so the corridor it flew through is
        /// filled with light rather than outlined by a line.
        /// <para>
        /// The puffs are emitted <em>by distance</em>, which is what makes this cost nothing to keep
        /// in step: the brick's own movement paints the plume, so a fast stretch of the flight lays
        /// more of it and a brick standing still — waiting its turn in the stagger — lays none.
        /// </para>
        /// <para>
        /// Three numbers arrive from the config, because they have to agree with the flight they
        /// belong to: how long a puff lives, how big it is, and how many go into one cell of travel.
        /// The noise that makes the column wander, the fade curves and the puff texture are the
        /// prefab's and the art's, never written by code — those are the parts somebody tunes by eye,
        /// and a tool that rewrote them on every flight would be the mistake D-053 recorded.
        /// </para>
        /// <para>
        /// The colour goes in as the system's start colour, not into a material: one material serves
        /// twelve brick colours, and nothing is cloned.
        /// </para>
        /// </summary>
        public void StartPlume(float whiteLift, float seconds, float size, float perCell)
        {
            if (plume == null || seconds <= 0f || size <= 0f || perCell <= 0f)
            {
                return;
            }

            Color? neon = Neon(whiteLift);

            if (neon.HasValue)
            {
                ParticleSystem.MainModule main = plume.main;
                main.startColor = neon.Value;
                main.startLifetime = seconds;
                main.startSize = size;
            }

            ParticleSystem.EmissionModule emission = plume.emission;
            emission.rateOverDistance = perCell;

            // Cleared before it starts: whatever the last flight left behind is not part of this one.
            plume.Clear();
            plume.Play();
        }

        /// <summary>
        /// Stops feeding the plume. The puffs already dropped stay where they are and fade out over
        /// their own lifetime — clearing them instead would take the corridor of light away in the
        /// same frame the brick lands, which is the one thing it must not do.
        /// </summary>
        public void StopPlume()
        {
            if (plume != null)
            {
                plume.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>Takes every puff away at once. A pooled brick starts with no history.</summary>
        public void ClearPlume()
        {
            if (plume == null)
            {
                return;
            }

            plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            plume.Clear();
        }

        /// <summary>
        /// A brick's colour as light: the same hue at full value, plus a touch of white (D-063).
        /// <para>
        /// Mixing towards white is what *kills* neon — it desaturates, and a desaturated additive pass
        /// is a pale patch. Dividing by the strongest channel instead keeps the hue exactly and raises
        /// only its brightness, which is what makes a lighter tone of the brick's own colour rather
        /// than a whiter one. The small lift afterwards is the hot core a neon tube has.
        /// </para>
        /// <para>
        /// It lives here, on the brick, because two things are lit by it now — the lifted run's glow
        /// and the flying brick's plume — and a formula this hard-won is not worth having two copies
        /// of (D-073).
        /// </para>
        /// </summary>
        /// <summary>
        /// This brick's own colour as light, or null when it has no material to read one from. One
        /// accessor because two things are lit by it now — the plume behind a flying brick and the
        /// sparks a landing throws — and a formula won as painfully as this one is not worth having two
        /// copies of (D-063, D-073, D-080).
        /// </summary>
        public Color? Neon(float whiteLift)
        {
            Material[] slots = meshRenderer == null ? null : meshRenderer.sharedMaterials;
            Material body = slots != null && slots.Length > 0 ? slots[0] : null;

            if (body == null || !body.HasProperty(BaseColourProperty))
            {
                return null;
            }

            return NeonOf(body.GetColor(BaseColourProperty), whiteLift);
        }

        public static Color NeonOf(Color colour, float lift)
        {
            float strongest = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));

            // A brick with no colour at all — the placeholder white, or a near-black `?` — has no hue
            // to raise, so it is left alone rather than divided by nothing.
            Color hue = strongest > 0.001f
                ? new Color(colour.r / strongest, colour.g / strongest, colour.b / strongest, colour.a)
                : colour;

            return Color.Lerp(hue, Color.white, lift);
        }

        private void Reset()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }
    }
}
