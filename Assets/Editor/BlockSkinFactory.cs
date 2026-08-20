#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using ColorfulSort.Content;
using UnityEditor;
using UnityEngine;

namespace ColorfulSort.EditorTools
{
    /// <summary>
    /// Creates the skin assets a board needs to draw itself, and does the one part of that
    /// job a human should not do by hand: wiring a mesh that lives *inside* an FBX, whose
    /// file id is a hash, into a `.asset` file.
    /// <para>
    /// What it deliberately does **not** do is decide a colour. Each skin is created with
    /// its colour field at the visible white default; the designer sets the twelve colours
    /// in the Inspector and runs this again, which regenerates every brick material from
    /// them. That keeps one authority for "what does this colour look like" — the skin
    /// asset — with the material as its derivative (D-020), and it keeps colour values out
    /// of code where <c>.claude/rules/data.md</c> says they must not be.
    /// </para>
    /// <para>
    /// Re-runnable and non-destructive: an assigned mesh, an assigned material and an
    /// authored id mapping are never overwritten, because those assignments are exactly
    /// what a re-skin consists of (D-003).
    /// </para>
    /// </summary>
    public static class BlockSkinFactory
    {
        private const string ModelsFolder = "Assets/Art/Models/Blocks";

        /// <summary>Where the symbols lifted out of the brick meshes live; one asset per skin.</summary>
        private const string SymbolsFolder = "Assets/Art/Models/Blocks/Symbols";

        private const string SymbolMeshPrefix = "Symbol_";
        private const string SkinsFolder = "Assets/Data/Blocks";
        private const string MaterialsFolder = "Assets/Art/Materials";

        private const string MeshPrefix = "Block_";
        private const string ModelExtension = ".fbx";
        private const string SkinPrefix = "Skin_";
        private const string MaterialPrefix = "Brick_";

        /// <summary>What the symbol's material file is called: `Brick_Cat_Symbol.mat` beside `Brick_Cat.mat`.</summary>
        private const string SymbolMaterialSuffix = "_Symbol";
        private const string SetAssetName = "BlockSkinSet";

        /// <summary>
        /// The `?` brick's symbol. It is the hidden-cell look rather than a colour's symbol
        /// (reference §2), so it gets a skin like the others but no row in the colour map.
        /// </summary>
        private const string HiddenSymbol = "Question";

        /// <summary>Internal so the column tools generate their materials on the same shader.</summary>
        internal const string UrpLitShader = "Universal Render Pipeline/Lit";

        /// <summary>URP's base colour property. Internal for the same reason as the shader name.</summary>
        internal static readonly int BaseColourProperty = Shader.PropertyToID("_BaseColor");

        [MenuItem("Tools/Colorful Sort/Create Block Skins")]
        public static void CreateBlockSkins()
        {
            Shader lit = Shader.Find(UrpLitShader);

            if (lit == null)
            {
                Debug.LogError("[Colorful Sort] Shader '" + UrpLitShader + "' was not found, so no brick material can be created. Is the project still on URP?");
                return;
            }

            List<string> symbols = FindSymbols();

            if (symbols.Count == 0)
            {
                Debug.LogWarning("[Colorful Sort] No " + MeshPrefix + "*.fbx models under " + ModelsFolder + "; nothing to build.");
                return;
            }

            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Blocks");
            EnsureFolder("Assets/Art", "Materials");

            int createdSkins = 0;
            int refreshedMaterials = 0;
            int placeholderColours = 0;

            Dictionary<string, BlockSkin> skins = new Dictionary<string, BlockSkin>();

            foreach (string symbol in symbols)
            {
                bool created;
                BlockSkin skin = LoadOrCreateSkin(symbol, out created);
                skins[symbol] = skin;

                if (created)
                {
                    createdSkins++;
                }

                WireMesh(skin, symbol);
                WireSparkMesh(skin, symbol);

                if (RefreshMaterial(skin, symbol, lit))
                {
                    refreshedMaterials++;
                }

                if (skin.UiColour == Color.white)
                {
                    placeholderColours++;
                }
            }

            string setReport = BuildSet(symbols, skins);

            // A symbol's colour is the *set's* answer, not the skin's: the skin's own darkened by the
            // shade for a colour row, and the authored `?` colour for the hidden brick (D-099). Either
            // way the set has to exist before those materials can be written, which is why this is a
            // second pass rather than part of the loop above (D-052). Nothing here has to know which of
            // the two a skin is — `SymbolColour` is asked and answers.
            BlockSkinSet set = AssetDatabase.LoadAssetAtPath<BlockSkinSet>(SkinsFolder + "/" + SetAssetName + ".asset");

            if (set == null || set.SymbolShade <= 0f || set.SymbolShade >= 1f)
            {
                Debug.LogWarning("[Colorful Sort] " + SetAssetName + " has no usable symbol shade, so the engraved symbols keep the brick's own colour. " +
                                 "Set Symbol Shade on the set and run this again.");
            }
            else
            {
                foreach (string symbol in symbols)
                {
                    if (RefreshSymbolMaterial(skins[symbol], symbol, lit, set))
                    {
                        refreshedMaterials++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Colorful Sort] Block skins: " + createdSkins + " skin(s) created, " +
                      refreshedMaterials + " material(s) written, " + setReport + ".");

            if (placeholderColours > 0)
            {
                Debug.LogWarning("[Colorful Sort] " + placeholderColours + " skin(s) still carry the placeholder white. Set their colour in " +
                                 SkinsFolder + " (the `?` brick is the palette's mystery colour) and run this again to regenerate the materials.");
            }
        }

        /// <summary>Every `Block_<Symbol>.fbx` in the models folder, in a stable order.</summary>
        private static List<string> FindSymbols()
        {
            List<string> symbols = new List<string>();

            if (!AssetDatabase.IsValidFolder(ModelsFolder))
            {
                return symbols;
            }

            // An FBX's main asset is a GameObject, so that is what the search asks for; the
            // extension check is what keeps a stray prefab in the folder out of the list.
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { ModelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!path.EndsWith(ModelExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // The floor plate shares this folder and shares the prefix, but it is not a colour:
                // treated as one it would grow a thirteenth skin and a material nothing spawns.
                if (path == BoardViewPrefabFactory.BasePlateMesh)
                {
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(path);

                if (fileName.StartsWith(MeshPrefix, StringComparison.Ordinal))
                {
                    symbols.Add(fileName.Substring(MeshPrefix.Length));
                }
            }

            symbols.Sort(StringComparer.Ordinal);
            return symbols;
        }

        private static BlockSkin LoadOrCreateSkin(string symbol, out bool created)
        {
            string path = SkinsFolder + "/" + SkinPrefix + symbol + ".asset";
            BlockSkin skin = AssetDatabase.LoadAssetAtPath<BlockSkin>(path);
            created = false;

            if (skin != null)
            {
                return skin;
            }

            skin = ScriptableObject.CreateInstance<BlockSkin>();
            AssetDatabase.CreateAsset(skin, path);
            created = true;
            return skin;
        }

        /// <summary>
        /// Points an empty mesh slot at its FBX's mesh. An already-assigned slot is left
        /// alone: pointing a skin at a different symbol is what a re-skin *is*, and a tool
        /// that undid it on every run would make that impossible.
        /// </summary>
        private static void WireMesh(BlockSkin skin, string symbol)
        {
            if (skin.SymbolMesh != null)
            {
                return;
            }

            string modelPath = ModelsFolder + "/" + MeshPrefix + symbol + ".fbx";
            Mesh mesh = null;

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                mesh = asset as Mesh;

                if (mesh != null)
                {
                    break;
                }
            }

            if (mesh == null)
            {
                Debug.LogWarning("[Colorful Sort] " + modelPath + " holds no mesh, so " + skin.name + " has nothing to show.");
                return;
            }

            SerializedObject serialized = new SerializedObject(skin);
            serialized.FindProperty("symbolMesh").objectReferenceValue = mesh;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Lifts this skin's engraved symbol out of its brick mesh into an asset of its own, so a burst
        /// of particles can be shaped like it (D-080).
        /// <para>
        /// Rebuilt on every run rather than only when the slot is empty. That is the opposite of
        /// <see cref="WireMesh"/> and deliberate: the brick mesh is a reference to art somebody
        /// authored, while this is *derived* from that art — so it is stale the moment the FBX changes,
        /// and a pass that skips what already exists would leave the old symbol in place with nothing
        /// to say so. Eight tasks in this project have now been cost by create-only passes (D-071,
        /// D-078, D-079); derived data is the case where the answer is simply "always rebuild".
        /// </para>
        /// <para>
        /// It writes over the same asset path rather than making a new one, so every reference to it —
        /// the skin's, and any particle renderer already pointing at it — survives the rebuild.
        /// </para>
        /// </summary>
        private static void WireSparkMesh(BlockSkin skin, string symbol)
        {
            if (skin.SymbolMesh == null)
            {
                return;
            }

            string error;
            string assetName = SymbolMeshPrefix + symbol;
            Mesh built = SymbolMeshBuilder.Build(skin.SymbolMesh, assetName, out error);

            if (built == null)
            {
                Debug.LogWarning("[Colorful Sort] " + skin.name + " keeps its old sparks: " + error + ".");
                return;
            }

            EnsureFolder(ModelsFolder, "Symbols");

            string path = SymbolsFolder + "/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
            }
            else
            {
                // Copied into the asset that is already there, so the GUID and every reference to it
                // survive: replacing the file would silently empty the mesh slot on the skin and on
                // whichever particle renderer was last pointed at it.
                Vector3[] vertices = built.vertices;
                Vector3[] normals = built.normals;
                Vector2[] uv = built.uv;

                existing.Clear();
                existing.vertices = vertices;

                // Guarded rather than copied blindly: a channel whose length does not match the
                // vertices is refused by Unity, and an FBX that ships no UVs is a normal FBX.
                if (normals != null && normals.Length == vertices.Length)
                {
                    existing.normals = normals;
                }

                if (uv != null && uv.Length == vertices.Length)
                {
                    existing.uv = uv;
                }

                existing.subMeshCount = 1;
                existing.SetTriangles(built.triangles, 0);
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(built);
                built = existing;
            }

            SerializedObject serialized = new SerializedObject(skin);
            serialized.FindProperty("sparkMesh").objectReferenceValue = built;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Makes the skin's material say what the skin says. Creates it on the first run;
        /// afterwards it only rewrites the base colour, so a designer's smoothness or
        /// normal-map edits on the material survive.
        /// </summary>
        private static bool RefreshMaterial(BlockSkin skin, string symbol, Shader lit)
        {
            return RefreshMaterial(
                skin, "material", MaterialsFolder + "/" + MaterialPrefix + symbol + ".mat", skin.UiColour, lit);
        }

        /// <summary>
        /// The material the engraved symbol's own mesh slot wears: the same asset shape as the
        /// brick's, in the same colour darkened by the set's shade (D-052). Its own file, because a
        /// brick and its symbol are two slots on one mesh and Unity paints a slot with a material.
        /// </summary>
        private static bool RefreshSymbolMaterial(BlockSkin skin, string symbol, Shader lit, BlockSkinSet set)
        {
            return RefreshMaterial(
                skin,
                "symbolMaterial",
                MaterialsFolder + "/" + MaterialPrefix + symbol + SymbolMaterialSuffix + ".mat",
                set.SymbolColour(skin),
                lit);
        }

        /// <summary>
        /// Makes one of a skin's materials say what the skin says. Creates it on the first run;
        /// afterwards it only rewrites the base colour, so a designer's smoothness or
        /// normal-map edits on the material survive.
        /// </summary>
        private static bool RefreshMaterial(BlockSkin skin, string propertyName, string path, Color colour, Shader lit)
        {
            var serialized = new SerializedObject(skin);
            SerializedProperty property = serialized.FindProperty(propertyName);
            var material = property.objectReferenceValue as Material;
            bool wrote = false;

            if (material == null)
            {
                material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    material = new Material(lit);
                    AssetDatabase.CreateAsset(material, path);
                    wrote = true;
                }

                property.objectReferenceValue = material;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (!material.HasProperty(BaseColourProperty))
            {
                Debug.LogWarning("[Colorful Sort] " + material.name + " uses a shader without a base colour, so " +
                                 skin.name + "'s colour cannot be written into it.");
                return wrote;
            }

            if (material.GetColor(BaseColourProperty) != colour)
            {
                material.SetColor(BaseColourProperty, colour);
                EditorUtility.SetDirty(material);
                wrote = true;
            }

            return wrote;
        }

        /// <summary>
        /// Creates the set if it is missing and seeds the colour map only when it is empty.
        /// The seeded ids are an arbitrary starting order — which colour id wears which
        /// symbol is the designer's mapping, and once a row exists this tool never touches
        /// it again.
        /// </summary>
        private static string BuildSet(List<string> symbols, Dictionary<string, BlockSkin> skins)
        {
            string path = SkinsFolder + "/" + SetAssetName + ".asset";
            BlockSkinSet set = AssetDatabase.LoadAssetAtPath<BlockSkinSet>(path);

            if (set == null)
            {
                set = ScriptableObject.CreateInstance<BlockSkinSet>();
                AssetDatabase.CreateAsset(set, path);
            }

            SerializedObject serialized = new SerializedObject(set);
            SerializedProperty entries = serialized.FindProperty("entries");
            SerializedProperty hidden = serialized.FindProperty("hiddenSkin");

            string report;

            if (entries.arraySize > 0)
            {
                report = SetAssetName + " left as authored (" + entries.arraySize + " colour row(s))";
            }
            else
            {
                int row = 0;

                foreach (string symbol in symbols)
                {
                    if (symbol == HiddenSymbol)
                    {
                        continue;
                    }

                    entries.InsertArrayElementAtIndex(row);
                    SerializedProperty entry = entries.GetArrayElementAtIndex(row);
                    entry.FindPropertyRelative("colourId").intValue = row + 1;
                    entry.FindPropertyRelative("skin").objectReferenceValue = skins[symbol];
                    row++;
                }

                report = SetAssetName + " seeded with " + row + " colour row(s)";
            }

            BlockSkin hiddenSkin;

            if (hidden.objectReferenceValue == null && skins.TryGetValue(HiddenSymbol, out hiddenSkin))
            {
                hidden.objectReferenceValue = hiddenSkin;
                report += " and the ? brick";
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return report;
        }

        private static void EnsureFolder(string parent, string folder)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + folder))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif
