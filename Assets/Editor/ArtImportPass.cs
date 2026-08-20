#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ColorfulSort.EditorTools
{
    /// <summary>
    /// Puts the art pack's contract onto the imported sprites, and puts it there the same
    /// way every time. The numbers below are <em>given, not chosen</em>
    /// (`ArtSource/colorful-sort-pack/Unity_Import_Guide.md`, reference §6): one logical
    /// cell is 512 px = 1 Unity unit, the slot family's pivots are what makes a column
    /// stack from its base, and the two borders are what let `Draw Mode: Tiled` step in
    /// exact 512 px cells (D-007). Getting one of them wrong by hand is invisible until a
    /// four-cell column looks wrong on a phone.
    /// <para>
    /// Idempotent: it compares before it writes, so a second run reports "already correct"
    /// and reimports nothing.
    /// </para>
    /// <para>
    /// The UI sprites are here on the same terms: their 9-slice borders are read from the
    /// pack's `AssetManifest.json` and are the reason a popup body can be any size without
    /// its rounded corners stretching. They keep the manifest's own 100 pixels per unit
    /// rather than the board's 512 — a `Canvas` scales by its reference resolution and never
    /// consults pixels-per-unit, so the two numbers are answers to different questions and
    /// forcing the board's onto UI would only make sprite sizes read wrongly in the
    /// Inspector.
    /// </para>
    /// </summary>
    public static class ArtImportPass
    {
        private const string GameplayFolder = "Assets/Art/Sprites/Gameplay";
        private const string BackgroundsFolder = "Assets/Art/Sprites/Backgrounds";
        private const string UiFolder = "Assets/Art/Sprites/UI";

        /// <summary>1 logical cell = 512 px = 1 Unity unit.</summary>
        private const float CellPixelsPerUnit = 512f;

        /// <summary>
        /// The scale the hand-drawn column art is authored at. It is a *measured* number, not a
        /// choice: the tray's drawn interior is 188 px, and at 160 px per unit that is 1.175
        /// units — one cell for the brick with a little of the tray showing on either side. The
        /// rails are not on the nine-slice boundary, so no border can state this; only a
        /// measurement can, and it moves if the art is re-exported at another size.
        /// </summary>
        private const float ColumnArtPixelsPerUnit = 160f;

        /// <summary>The pack's own figure for UI art, which the canvas does not use anyway.</summary>
        private const float UiPixelsPerUnit = 100f;

        /// <summary>0 means "leave the importer's own value alone".</summary>
        private const float KeepPixelsPerUnit = 0f;

        private const int MaxTextureSize = 4096;

        /// <summary>The world-space tiling pattern is the one sprite that repeats.</summary>
        private const string TilingPattern = "gameplay_block_pattern_512";

        private static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);

        private static readonly Vector4 NoBorder = Vector4.zero;

        // (left, bottom, right, top) — a column sprite's border says one thing and one thing
        // only: the bottom is its skirt, the top is its crown, and everything between them is
        // cells. The pack's own art used to fold a cell into its bottom border, which meant
        // `ColumnMetrics` had to subtract one before trusting the number; these values say the
        // same skirts (0.625 and 1.25 units at 512 px per unit) without the arithmetic (D-048).
        private static readonly Vector4 NormalSlotBorder = new Vector4(160f, 320f, 160f, 320f);
        private static readonly Vector4 IceSlotBorder = new Vector4(160f, 640f, 160f, 320f);

        // The hand-drawn tray, at 160 px per unit: 46 px of skirt is 0.2875 units, which is exactly
        // the height of the Block_Base plate the bricks stand on, and 24 px of side is the rim.
        //
        // The top is 28 px, and that number does two jobs at once. It is the largest distance from
        // the top edge at which the tray's drawn interior begins — 7 px under the wave's crests,
        // 21 in the dip between them, 28 in the corners — so it is the smallest border that keeps
        // the whole wave out of the stretched middle band. And because the crown is read from it
        // (D-048), it puts the top cell's ceiling right where the interior starts, which leaves the
        // highest brick's studs (0.045 above that ceiling) inside the wave rather than under it: the
        // tray frames them instead of floating over them (D-051).
        private static readonly Vector4 ColumnTrayBorder = new Vector4(24f, 46f, 24f, 28f);

        /// <summary>The completed-column shadow stretches over any capacity, so its corners are sliced.</summary>
        private static readonly Vector4 CompletedShadowBorder = new Vector4(40f, 40f, 40f, 40f);

        /// <summary>
        /// The glow's border: the tray's, with the top raised from 28 to 42 px. Measured, not chosen —
        /// the drawing's corner is still widening at y 39 and only settles at y 40, and everything the
        /// border leaves out gets stretched (D-068).
        /// </summary>
        private static readonly Vector4 GlowBorder = new Vector4(24f, 46f, 24f, 42f);

        // (left, bottom, right, top) — the UI borders, copied from the `border` field of the
        // pack's AssetManifest.json. Every sprite the manifest calls `Sliced` has one; the
        // ones it calls `Simple` (icons, coin, heart, the round shells) are drawn at their
        // authored size and must keep a zero border, or a nine-slice would cut them apart.
        private static readonly Vector4 LevelButtonBorder = new Vector4(128f, 88f, 128f, 104f);
        private static readonly Vector4 SquareButtonBorder = new Vector4(72f, 72f, 72f, 88f);
        private static readonly Vector4 WideButtonBorder = new Vector4(88f, 64f, 88f, 80f);
        private static readonly Vector4 HudPillBorder = new Vector4(72f, 56f, 72f, 72f);
        private static readonly Vector4 PopupBodyBorder = new Vector4(96f, 112f, 96f, 136f);
        private static readonly Vector4 PopupHeaderBorder = new Vector4(112f, 72f, 112f, 88f);

        private struct SpriteContract
        {
            public Vector2 pivot;
            public Vector4 border;
            public TextureWrapMode wrap;
            public float pixelsPerUnit;
        }

        [MenuItem("Tools/Colorful Sort/Apply Art Import Settings")]
        public static void ApplyImportSettings()
        {
            List<string> changed = new List<string>();
            int inspected = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                inspected += Pass(GameplayFolder, GameplayContract, changed);
                inspected += Pass(BackgroundsFolder, BackgroundContract, changed);
                inspected += Pass(UiFolder, UiContract, changed);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();

            if (inspected == 0)
            {
                Debug.LogWarning("[Colorful Sort] Import pass found no sprites. Is the art pack imported under Assets/Art/Sprites/?");
                return;
            }

            Debug.Log("[Colorful Sort] Import pass: " + changed.Count + " of " + inspected +
                      " sprite(s) updated" + (changed.Count == 0 ? " (all already correct)." : ": " + string.Join(", ", changed.ToArray()) + "."));
        }

        private static int Pass(string folder, Func<string, SpriteContract> contractFor, List<string> changed)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("[Colorful Sort] " + folder + " does not exist; nothing to import there.");
                return 0;
            }

            int inspected = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer == null)
                {
                    continue;
                }

                inspected++;
                string fileName = Path.GetFileNameWithoutExtension(path);
                SpriteContract contract = contractFor(fileName);

                if (Apply(importer, contract))
                {
                    changed.Add(fileName);
                }
            }

            return inspected;
        }

        /// <summary>
        /// Pivots come straight from the import guide's assembly recipe: the pieces that a
        /// column grows upward from sit on their bottom edge, the piece that caps it hangs
        /// from its top edge, and everything that is overlaid — a cover cell, the mystery
        /// face, an icicle — is centred on the cell it covers.
        /// </summary>
        private static SpriteContract GameplayContract(string fileName)
        {
            SpriteContract contract = new SpriteContract
            {
                pivot = Centre,
                border = NoBorder,
                wrap = TextureWrapMode.Clamp,
                pixelsPerUnit = CellPixelsPerUnit,
            };

            switch (fileName)
            {
                // The hand-drawn tray and the two pieces that go with it are authored at their
                // own scale, so they carry their own pixels-per-unit. Everything else in this
                // folder is the generated pack at 512.
                case "slot":
                    contract.pivot = BottomCentre;
                    contract.border = ColumnTrayBorder;
                    contract.pixelsPerUnit = ColumnArtPixelsPerUnit;
                    break;

                // The light behind a lifted run: the tray's own drawing with its pixels forced to
                // white (D-062). Same scale and pivot, but a *taller top border* on purpose — the
                // tray's corner radius only settles at y 40, and a nine-slice stretches whatever the
                // border does not cover, which is what turned that corner into a hard shoulder
                // (D-068). Nothing reads this sprite's metrics, so the two borders may differ; do not
                // "tidy" it back to the tray's.
                case "block_glow":
                    contract.pivot = BottomCentre;
                    contract.border = GlowBorder;
                    contract.pixelsPerUnit = ColumnArtPixelsPerUnit;
                    break;

                case "slot_bolme":
                    // A divider sits *on* a cell boundary, so it is centred on it, and at this
                    // scale it is already as wide as the tray's interior — nothing to stretch,
                    // which is why it carries no border.
                    contract.pivot = Centre;
                    contract.pixelsPerUnit = ColumnArtPixelsPerUnit;
                    break;

                // One puff of the plume a flying brick leaves (D-074). It is read as a *texture* by
                // a particle material rather than as a sprite by a renderer, so its pivot and
                // pixels-per-unit are never used — what matters is that it stays a clamped,
                // unstretched image whose alpha is the puff's shape. Centred and at the cell scale
                // anyway, so it behaves like everything else in this folder if it is ever drawn as
                // a sprite.
                case "plume_puff":
                    contract.pivot = Centre;
                    contract.pixelsPerUnit = ColumnArtPixelsPerUnit;
                    break;

                // The completed-column shadow and the glow behind a lifted run are the same drawing:
                // the glow is that shape with its pixels forced to white, so a per-renderer tint can
                // light it — a tint multiplies, and the shadow's own dark navy could only darken
                // (D-060). Same pivot, same scale, same border, because it is the same art.
                case "slot_completed_shadow":
                    contract.pivot = BottomCentre;
                    contract.border = CompletedShadowBorder;
                    contract.pixelsPerUnit = ColumnArtPixelsPerUnit;
                    break;

                case "slot_complete_2cell":
                    contract.pivot = BottomCentre;
                    contract.border = NormalSlotBorder;
                    break;

                case "slot_ice_complete_2cell":
                    contract.pivot = BottomCentre;
                    contract.border = IceSlotBorder;
                    break;

                // The legacy modular triplets, kept usable for a column height the complete
                // sprite cannot express.
                case "slot_top":
                case "slot_ice_top":
                case "cover_top_cap":
                    contract.pivot = BottomCentre;
                    break;

                case "slot_bottom":
                case "slot_ice_bottom":
                    contract.pivot = TopCentre;
                    break;
            }

            return contract;
        }

        private static SpriteContract BackgroundContract(string fileName)
        {
            // A background is stretched by an Image or an AspectRatioFitter, both of which
            // ignore pixels-per-unit, so this pass does not impose one — except on the
            // tiling pattern, which is used in world space and has to tile per cell.
            return new SpriteContract
            {
                pivot = Centre,
                border = NoBorder,
                wrap = fileName == TilingPattern ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                pixelsPerUnit = fileName == TilingPattern ? CellPixelsPerUnit : KeepPixelsPerUnit,
            };
        }

        /// <summary>
        /// The UI contract: a centred pivot for everything, and the manifest's border on the
        /// six families that stretch. Matching is by prefix because a button ships as three
        /// files — `_normal`, `_pressed`, `_disabled` — that must be sliced identically, or
        /// the button changes shape when it is held down.
        /// </summary>
        private static SpriteContract UiContract(string fileName)
        {
            return new SpriteContract
            {
                pivot = Centre,
                border = UiBorder(fileName),
                wrap = TextureWrapMode.Clamp,
                pixelsPerUnit = UiPixelsPerUnit,
            };
        }

        private static Vector4 UiBorder(string fileName)
        {
            if (fileName.StartsWith("level_button", StringComparison.Ordinal))
            {
                return LevelButtonBorder;
            }

            if (fileName.StartsWith("square_", StringComparison.Ordinal))
            {
                return SquareButtonBorder;
            }

            if (fileName.StartsWith("wide_", StringComparison.Ordinal))
            {
                return WideButtonBorder;
            }

            switch (fileName)
            {
                case "hud_pill_9slice":
                    return HudPillBorder;

                case "popup_body_9slice":
                    return PopupBodyBorder;

                case "popup_header_9slice":
                    return PopupHeaderBorder;

                default:
                    // Icons, the coin, the heart and the round shells are drawn whole.
                    return NoBorder;
            }
        }

        /// <summary>
        /// Writes only what differs, so a second run reimports nothing. The comparisons are
        /// spelled out one per line rather than hidden behind a generic setter, because
        /// <see cref="TextureImporterSettings"/> exposes properties and a property cannot be
        /// passed by reference.
        /// </summary>
        private static bool Apply(TextureImporter importer, SpriteContract contract)
        {
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            bool changed = false;

            if (settings.textureType != TextureImporterType.Sprite)
            {
                settings.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            // A texture imported as `Default` carries `npotScale: ToNearest`, which a Sprite may not
            // have: writing the settings back with it makes Unity reset the field and warn, once per
            // sprite per run. Saying what a Sprite requires is the difference between a clean console
            // and three warnings that look like the art's fault (D-069).
            if (settings.npotScale != TextureImporterNPOTScale.None)
            {
                settings.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }

            if (settings.spriteMode != (int)SpriteImportMode.Single)
            {
                settings.spriteMode = (int)SpriteImportMode.Single;
                changed = true;
            }

            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                changed = true;
            }

            if (settings.spriteAlignment != (int)SpriteAlignment.Custom)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                changed = true;
            }

            if (settings.spritePivot != contract.pivot)
            {
                settings.spritePivot = contract.pivot;
                changed = true;
            }

            if (settings.spriteBorder != contract.border)
            {
                settings.spriteBorder = contract.border;
                changed = true;
            }

            if (!settings.alphaIsTransparency)
            {
                settings.alphaIsTransparency = true;
                changed = true;
            }

            if (settings.mipmapEnabled)
            {
                settings.mipmapEnabled = false;
                changed = true;
            }

            if (!settings.sRGBTexture)
            {
                settings.sRGBTexture = true;
                changed = true;
            }

            if (settings.wrapMode != contract.wrap)
            {
                settings.wrapMode = contract.wrap;
                changed = true;
            }

            if (settings.filterMode != FilterMode.Bilinear)
            {
                settings.filterMode = FilterMode.Bilinear;
                changed = true;
            }

            if (changed)
            {
                importer.SetTextureSettings(settings);
            }

            // Pixels-per-unit, size and compression live on the importer itself, not on the
            // settings object that carries the sprite's own geometry.
            if (contract.pixelsPerUnit > KeepPixelsPerUnit &&
                !Mathf.Approximately(importer.spritePixelsPerUnit, contract.pixelsPerUnit))
            {
                importer.spritePixelsPerUnit = contract.pixelsPerUnit;
                changed = true;
            }

            if (importer.maxTextureSize != MaxTextureSize)
            {
                importer.maxTextureSize = MaxTextureSize;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }
    }
}
#endif
