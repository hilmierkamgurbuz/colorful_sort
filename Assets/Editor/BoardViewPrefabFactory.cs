#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using ColorfulSort.Content;
using ColorfulSort.Meta;
using ColorfulSort.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorfulSort.EditorTools
{
    /// <summary>
    /// Builds the four BoardView prefabs and wires the Game scene to them.
    /// <para>
    /// A script rather than a click-through for the same reason the scene bootstrapper is one:
    /// what goes into these prefabs is the art pack's contract, not taste — the slot draws
    /// <c>Tiled</c> so per-level capacity is visible (D-007), the sorting orders are the pack's
    /// (column 10, overlay 30), and the Z offsets are what layer transparent slot art against
    /// opaque bricks (D-005). Typed by hand, any one of them is wrong in a way that looks
    /// almost right.
    /// </para>
    /// <para>
    /// Non-destructive: an existing prefab is left exactly as it is, and in the scene only
    /// <em>empty</em> reference slots are filled. Delete a prefab to have it rebuilt.
    /// </para>
    /// </summary>
    public static class BoardViewPrefabFactory
    {
        private const string PrefabFolder = "Assets/Prefabs/BoardView";
        private const string GameplaySprites = "Assets/Art/Sprites/Gameplay";
        private const string BlockModels = "Assets/Art/Models/Blocks";

        private const string SkinSetAsset = "Assets/Data/Blocks/BlockSkinSet.asset";
        private const string LayoutConfigAsset = "Assets/Data/Config/BoardLayoutConfig.asset";
        private const string AnimationConfigAsset = "Assets/Data/Config/BoardAnimationConfig.asset";
        private const string LevelDatabaseAsset = "Assets/Data/Levels/LevelDatabase.asset";
        private const string ProgressionConfigAsset = "Assets/Data/Config/ProgressionConfig.asset";
        private const string GameScene = "Assets/Scenes/Game.unity";

        // The art pack's sorting order: background 0, column 10, bricks 20, overlay 30.
        private const int ColumnSortingOrder = 10;
        private const int CoverSortingOrder = 30;

        /// <summary>The hand-drawn tray a normal column wears; the ice variant keeps the pack's.</summary>
        private const string ColumnTraySprite = "slot";

        /// <summary>The line between two cells, drawn by the view once per boundary.</summary>
        private const string CellDividerSprite = "slot_bolme";

        /// <summary>
        /// The 3D plate a column's bricks stand on, under the tray art. Internal because
        /// <c>BlockSkinFactory</c> has to know it is not a brick: it shares the models folder and
        /// the <c>Block_</c> prefix, and taken for a symbol it would grow a skin of its own.
        /// </summary>
        internal const string BasePlateMesh = "Assets/Art/Models/Blocks/Block_Base.fbx";

        /// <summary>The generated material the plate wears — the FBX's own renders magenta under URP.</summary>
        private const string BasePlateMaterial = "Assets/Art/Materials/Slot_Base.mat";

        /// <summary>What the base plate object is called inside a column prefab.</summary>
        private const string BasePlateName = "Base";

        /// <summary>The shadow a finished column sinks under, and what it is called in the prefab.</summary>
        private const string SettledShadowSprite = "slot_completed_shadow";

        /// <summary>The glow behind a lifted run: the same shape as the shadow, painted white so a tint can light it.</summary>
        private const string GlowSprite = "block_glow";

        private const string GlowName = "Glow";

        /// <summary>Between the tray and the bricks: over the column art, under the geometry that hides it.</summary>
        private const int GlowSortingOrder = 15;

        /// <summary>The additive shader and material the glow burns with — alpha blending cannot glow (D-061).</summary>
        private const string GlowShader = "Colorful Sort/Block Glow";

        /// <summary>The plume a flying brick leaves: its own child, because one object holds one renderer.</summary>
        private const string PlumeName = "Plume";

        /// <summary>The line V-8b drew before the reference showed a plume. Removed where it is found.</summary>
        private const string RetiredTrailName = "Trail";

        private const string RetiredTrailMaterial = "Assets/Art/Materials/Block_Trail.mat";

        private const string PlumeMaterial = "Assets/Art/Materials/Block_Plume.mat";

        /// <summary>
        /// The bursts' own material: the plume's drawing and the glow's shader, with the one piece of
        /// render state that separates light in front of the board from light behind a brick (D-079).
        /// </summary>
        private const string SparkMaterial = "Assets/Art/Materials/Block_Spark.mat";

        /// <summary>
        /// What the bursts draw with once their particles are shaped like the symbol: the same shader
        /// at the same Z test, and **no texture at all**, so the shape is the mesh's own silhouette
        /// rather than a soft puff smeared over whatever UVs the symbol's faces happened to carry
        /// (D-080). Block_Spark.mat is kept beside it: pointing the renderers back is the whole of the
        /// revert if symbol sparks read worse than soft ones.
        /// </summary>
        private const string SymbolSparkMaterial = "Assets/Art/Materials/Block_Symbol.mat";

        private const string ZTestProperty = "_ZTest";

        private const string PlumeTexture = "plume_puff";

        /// <summary>The finish burst: one system per column, thrown once at the middle of the slot.</summary>
        private const string FinishName = "Finish";

        /// <summary>
        /// The single burst these two replace. It was dressed for a throw at every brick and it was
        /// never seen even once, because nothing ever played it — so it is retired by name rather than
        /// re-dressed, which keeps both new systems create-only and makes the migration run once
        /// (D-074's shape, D-078's reason).
        /// </summary>
        private const string RetiredSparkleName = "Sparkle";

        /// <summary>
        /// The placement burst: a second system per column, and the reason it is a second one is that
        /// <c>EmitParams</c> can override a particle's position, velocity, lifetime, size and colour
        /// but never its gravity — these sparks climb and fade, the finish burst's fall back (D-078).
        /// </summary>
        private const string RiseName = "Rise";

        private const int SparkleMaxParticles = 160;

        private const int RiseMaxParticles = 200;

        /// <summary>
        /// What a plume looks like when it is first made, and only then — lifetime, size and density
        /// come from the config at flight time, and everything here is the designer's from the moment
        /// it exists (D-053). The noise is what makes the column wander instead of running straight;
        /// the drift is the slow rise that reads as smoke.
        /// </summary>
        private const float PlumeNoiseStrength = 0.35f;

        /// <summary>Read off ColumnView rather than copied: the plume and the sparks wander alike (D-082).</summary>
        private const float PlumeNoiseFrequency = ColumnView.WanderFrequency;

        private const float PlumeDrift = 0.35f;

        private const int PlumeMaxParticles = 220;

        private const string GlowMaterial = "Assets/Art/Materials/Block_Glow.mat";

        private const string SettledShadowName = "SettledShadow";

        // Bricks are opaque meshes and the slot art is transparent, so depth does the layering
        // (D-005). The offset has to clear a brick's *volume*, not just its centre: a brick is a
        // cube one cell deep, so its faces are half a brick either side of the cell centre and a
        // sprite closer than that is inside it — which is how a cover ends up behind the bricks
        // it is supposed to hide (D-028). Measured at build time, plus this margin.
        private const float DepthMargin = 0.1f;

        [MenuItem("Tools/Colorful Sort/Build BoardView Prefabs")]
        public static void BuildPrefabs()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "BoardView");

            List<string> created = new List<string>();

            // The brick decides how far the slot art and the cover have to stand off, so it is
            // measured before anything is built — and measured *after* the scale that fits it into
            // a cell, since that is the size it will actually be in the scene.
            Vector3 brick = BrickSize();
            float footprint = Mathf.Max(brick.x, brick.z);
            float brickScale = footprint > 0f && Mathf.Abs(footprint - 1f) > 0.01f ? 1f / footprint : 1f;
            float standOff = brick.z * brickScale * 0.5f + DepthMargin;

            GameObject column = BuildColumnPrefab(standOff, created);

            if (column == null)
            {
                return;
            }

            BuildColumnVariant(column, "Column_Ice", "slot_ice_complete_2cell", false, created);
            BuildColumnVariant(column, "Column_Covered", null, true, created);
            BuildBlockPrefab(brickScale, created);

            // Creating is only half the job: a column prefab that already existed before the tray,
            // the dividers and the base plate did has to be brought up to them, or the tool would
            // demand the prefab be deleted — which throws away every reference to it.
            var notes = new List<string>();
            RepairColumnPrefabs(BrickTopY(), notes);
            EnsureBlockPlume(notes);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Colorful Sort] BoardView prefabs: " +
                      (created.Count == 0 ? "nothing to create, all four exist" : "created " + string.Join(", ", created.ToArray())) +
                      (notes.Count == 0 ? " and nothing to repair." : "; " + string.Join(", ", notes.ToArray()) + "."));
        }

        [MenuItem("Tools/Colorful Sort/Wire Game Scene")]
        public static void WireGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[Colorful Sort] Wiring cancelled: there are unsaved scene changes.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
            List<string> notes = new List<string>();

            Transform boardRoot = FindRoot(scene, "--Board--");
            Transform cameraRoot = FindRoot(scene, "--Camera--");

            if (boardRoot == null || cameraRoot == null)
            {
                Debug.LogError("[Colorful Sort] " + GameScene + " has no --Board-- or --Camera-- root; run Tools > Colorful Sort > Bootstrap Scenes first.");
                return;
            }

            BoardView board = Require<BoardView>(boardRoot.gameObject, notes, "BoardView on --Board--");
            BoardInput boardInput = Require<BoardInput>(boardRoot.gameObject, notes, "BoardInput on --Board--");
            BoardMoveAnimator moveAnimator = Require<BoardMoveAnimator>(boardRoot.gameObject, notes, "BoardMoveAnimator on --Board--");
            Transform columns = RequireChild(boardRoot, "Columns", notes);
            Transform pool = RequireChild(boardRoot, "Pool", notes);
            Camera camera = cameraRoot.GetComponentInChildren<Camera>();

            SerializedObject boardObject = new SerializedObject(board);
            FillIfEmpty(boardObject, "boardCamera", camera, notes);
            FillIfEmpty(boardObject, "columnRoot", columns, notes);
            FillIfEmpty(boardObject, "idleBlockRoot", pool, notes);
            FillIfEmpty(boardObject, "layout", AssetDatabase.LoadAssetAtPath<BoardLayoutConfig>(LayoutConfigAsset), notes);

            // The view reads the same animation asset the animator does, for the settle timings only
            // (D-057). Two readers of one asset, no second writer.
            FillIfEmpty(boardObject, "animationConfig", AssetDatabase.LoadAssetAtPath<BoardAnimationConfig>(AnimationConfigAsset), notes);
            FillIfEmpty(boardObject, "glow", EnsureGlow(columns, notes), notes);
            FillIfEmpty(boardObject, "skins", AssetDatabase.LoadAssetAtPath<BlockSkinSet>(SkinSetAsset), notes);
            FillIfEmpty(boardObject, "normalColumn", LoadPrefabComponent<ColumnView>("Column"), notes);
            FillIfEmpty(boardObject, "iceColumn", LoadPrefabComponent<ColumnView>("Column_Ice"), notes);
            FillIfEmpty(boardObject, "coveredColumn", LoadPrefabComponent<ColumnView>("Column_Covered"), notes);
            FillIfEmpty(boardObject, "blockPrefab", LoadPrefabComponent<BlockView>("Block"), notes);
            FillIfEmpty(boardObject, "input", boardInput, notes);
            FillIfEmpty(boardObject, "animator", moveAnimator, notes);
            boardObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject animatorObject = new SerializedObject(moveAnimator);
            FillIfEmpty(animatorObject, "config", AssetDatabase.LoadAssetAtPath<BoardAnimationConfig>(AnimationConfigAsset), notes);
            animatorObject.ApplyModifiedPropertiesWithoutUndo();

            Transform systems = FindRoot(scene, "--Systems--");

            if (systems == null)
            {
                systems = new GameObject("--Systems--").transform;
                notes.Add("added --Systems--");
            }

            AttemptStarter starter = Require<AttemptStarter>(systems.gameObject, notes, "AttemptStarter on --Systems--");
            SerializedObject starterObject = new SerializedObject(starter);
            FillIfEmpty(starterObject, "board", board, notes);

            // Which level opens is progression's answer now, not a serialized one: the starter
            // takes the database and the config and asks Meta for an ordinal (5A). A "play this
            // level" slot would be a second answer to the same question.
            FillIfEmpty(starterObject, "database", AssetDatabase.LoadAssetAtPath<LevelDatabase>(LevelDatabaseAsset), notes);
            FillIfEmpty(starterObject, "progressionConfig", AssetDatabase.LoadAssetAtPath<ProgressionConfig>(ProgressionConfigAsset), notes);
            starterObject.ApplyModifiedPropertiesWithoutUndo();

            EnsureDirectionalLight(scene, notes);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Colorful Sort] Game scene wiring: " +
                      (notes.Count == 0 ? "already complete, nothing changed." : string.Join("; ", notes.ToArray()) + "."));
        }

        // ------------------------------------------------------------ prefabs

        private static GameObject BuildColumnPrefab(float standOff, List<string> created)
        {
            string path = PrefabFolder + "/Column.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (existing != null)
            {
                return existing;
            }

            Sprite slotSprite = LoadSprite(ColumnTraySprite);

            if (slotSprite == null)
            {
                return null;
            }

            var root = new GameObject("Column");
            ColumnView view = root.AddComponent<ColumnView>();

            SpriteRenderer slot = CreateSlotRenderer(root.transform, slotSprite, standOff);
            Transform blocks = CreateChild(root.transform, "Blocks", 0f);
            Transform cover = CreateChild(root.transform, "Cover", -standOff);

            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("slot").objectReferenceValue = slot;
            serialized.FindProperty("blockRoot").objectReferenceValue = blocks;
            serialized.FindProperty("coverRoot").objectReferenceValue = cover;
            serialized.FindProperty("coverSortingOrder").intValue = CoverSortingOrder;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            created.Add("Column");
            return asset;
        }

        /// <summary>
        /// Brings a column prefab that already exists up to the current anatomy, part by part:
        /// the hand-drawn tray in place of the pack's, the divider sprite the view draws per
        /// boundary, and the 3D base plate the bricks stand on. Every part is added only if it is
        /// absent, which is the shape <c>UiFactory</c> earned the hard way — a tool that cannot
        /// repair what an older version of itself made only works once.
        /// <para>
        /// Variants are skipped on purpose: a child added to the base reaches them, and loading a
        /// variant's contents to save it back is how a variant loses its parent.
        /// </para>
        /// </summary>
        private static void RepairColumnPrefabs(float brickTopY, List<string> notes)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (asset == null || asset.GetComponent<ColumnView>() == null)
                {
                    continue;
                }

                if (PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant)
                {
                    continue;
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    if (RepairColumn(contents, brickTopY, notes))
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        notes.Add(System.IO.Path.GetFileNameWithoutExtension(path) + " brought up to date");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static bool RepairColumn(GameObject column, float brickTopY, List<string> notes)
        {
            ColumnView view = column.GetComponent<ColumnView>();
            var serialized = new SerializedObject(view);
            var slot = (SpriteRenderer)serialized.FindProperty("slot").objectReferenceValue;

            if (slot == null)
            {
                notes.Add(column.name + " has no slot renderer, so it was left alone");
                return false;
            }

            bool changed = false;

            // Only the pack's old normal tray is migrated. An ice column, or anything the user
            // dressed by hand, keeps the sprite it was given.
            if (slot.sprite == null || slot.sprite.name == "slot_complete_2cell")
            {
                Sprite tray = LoadSprite(ColumnTraySprite);

                if (tray != null)
                {
                    slot.sprite = tray;
                    changed = true;
                }
            }

            SpriteDrawMode wanted = ColumnMetrics.FromSprite(slot.sprite).TilesPerCell
                ? SpriteDrawMode.Tiled
                : SpriteDrawMode.Sliced;

            if (slot.drawMode != wanted)
            {
                ApplySlotDrawMode(slot);
                changed = true;
            }

            SerializedProperty divider = serialized.FindProperty("cellDivider");

            if (divider != null && divider.objectReferenceValue == null)
            {
                Sprite line = LoadSprite(CellDividerSprite);

                if (line != null)
                {
                    divider.objectReferenceValue = line;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            changed |= EnsureBasePlate(column, ColumnMetrics.FromSprite(slot.sprite), brickTopY, notes);
            changed |= EnsureSettledShadow(column, serialized, slot, notes);
            changed |= EnsureColumnSparkle(column, serialized, notes);
            return changed;
        }

        /// <summary>
        /// The plate a column's bricks stand on, created if it is missing and placed every run.
        /// <para>
        /// Placed by a *relationship*, not an offset: the plate's studs end exactly where the studs
        /// of a brick standing in the cell below would end, so the lowest brick swallows them the
        /// same way it swallows the studs of a brick beneath it. Aligning the plate's studs with the
        /// cell floor instead — which is what V-2 did — left them standing in the open, because a
        /// brick's body starts a little above its cell's floor (D-051). Both numbers are measured
        /// off the meshes, so re-exporting either moves the plate on its own.
        /// </para>
        /// </summary>
        private static bool EnsureBasePlate(GameObject column, ColumnMetrics metrics, float brickTopY, List<string> notes)
        {
            Mesh mesh = null;

            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(BasePlateMesh))
            {
                mesh = mesh ?? sub as Mesh;
            }

            if (mesh == null)
            {
                notes.Add("no base plate mesh at " + BasePlateMesh + ", so " + column.name + " has no floor");
                return false;
            }

            Material material = BasePlateMaterialAsset(notes);

            // A brick in cell 0 has its origin half a cell above the floor, so a brick one cell
            // lower would have its studs end here.
            float studTop = metrics.Skirt - 0.5f + brickTopY;
            var position = new Vector3(0f, studTop - mesh.bounds.max.y, 0f);

            Transform existing = column.transform.Find(BasePlateName);

            if (existing != null)
            {
                // Its transform is the prefab's business now, not this tool's (D-053). The derived
                // seating below is what a *new* plate is created with; once it exists, the number a
                // designer nudged by eye is the better one, and rewriting it every run was how this
                // tool would have quietly undone their tuning.
                var renderer = existing.GetComponent<MeshRenderer>();

                if (renderer == null || material == null || !IsFbxMaterial(renderer.sharedMaterial))
                {
                    return false;
                }

                renderer.sharedMaterial = material;
                notes.Add(column.name + "'s base plate given " + material.name);
                return true;
            }

            var plate = new GameObject(BasePlateName);
            plate.transform.SetParent(column.transform, false);
            plate.transform.localPosition = position;

            plate.AddComponent<MeshFilter>().sharedMesh = mesh;
            plate.AddComponent<MeshRenderer>().sharedMaterial = material;
            return true;
        }

        /// <summary>
        /// The shadow a finished column wears (D-057). It hangs at the cover's stand-off, which is
        /// what puts it in front of the bricks, and it is created fully transparent: `ColumnView`
        /// fades it in when a colour is gathered and sizes it to the tray on every build.
        /// <para>
        /// The Z comes from the `Cover` child rather than being recomputed, so the two overlays stay
        /// on the same plane whatever the brick's depth turns out to be — and a covered column can
        /// never be finished, so they can never both be visible.
        /// </para>
        /// </summary>
        /// <summary>
        /// The sparks a finished column throws (D-075): one particle system for the whole column,
        /// which the view emits at whichever brick is throwing them. Per column and not per brick,
        /// because a burst has to come *from* a place, not follow one — sixteen systems on the board
        /// instead of a hundred and twenty-eight.
        /// <para>
        /// Part of the repair pass, so a column prefab that predates it gains the child as well: the
        /// create-only trap this project has now met five times (D-071). The look is written once and
        /// then belongs to whoever tunes it (D-053); the count, and where each burst comes from, are
        /// the view's at emit time.
        /// </para>
        /// </summary>
        private static bool EnsureColumnSparkle(GameObject column, SerializedObject serialized, List<string> notes)
        {
            // The one-burst system this replaces was never once seen on screen — it was emitted into
            // while stopped, so it drew nothing (D-078) — and its look was written for a burst at each
            // brick, which is no longer what happens. Retiring it by name is what keeps both new
            // systems create-only: they are dressed once because they are new, and the pass is
            // self-terminating, exactly as the plume retired the trail it replaced (D-074).
            bool changed = RemoveRetiredChild(column, RetiredSparkleName, notes);

            changed |= EnsureBurst(column, serialized, "finish", FinishName, SparkleMaxParticles, Gravity.Falls, notes);
            changed |= EnsureBurst(column, serialized, "rise", RiseName, RiseMaxParticles, Gravity.None, notes);

            return changed;
        }

        /// <summary>Whether a burst's particles are pulled back down, which is the whole reason there are two.</summary>
        private enum Gravity
        {
            None,
            Falls,
        }

        /// <summary>
        /// Finds or makes one burst child, dresses it if it is new, repairs what must be true of it in
        /// either case, and binds it to its field on <c>ColumnView</c>.
        /// </summary>
        private static bool EnsureBurst(
            GameObject column,
            SerializedObject serialized,
            string field,
            string childName,
            int maxParticles,
            Gravity gravity,
            List<string> notes)
        {
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                notes.Add(column.name + ": ColumnView has no '" + field + "' field, so " + childName + " cannot be bound");
                return false;
            }

            Transform existing = column.transform.Find(childName);

            if (existing == null)
            {
                var child = new GameObject(childName);
                child.transform.SetParent(column.transform, false);
                existing = child.transform;
                notes.Add("added " + column.name + "/" + childName);
            }

            var system = existing.GetComponent<ParticleSystem>();

            if (system == null)
            {
                system = existing.gameObject.AddComponent<ParticleSystem>();
                DressBurst(system, maxParticles, gravity);
            }

            bool changed = EnsureSparkleMaterial(system, notes);

            // Every build, not only the first: this is the half a designer never tunes, and leaving it
            // to creation alone is what let a mixed-mode velocity curve sit in a shipped prefab.
            changed |= RepairBurst(system, column.name + "/" + childName, maxParticles, gravity, notes);

            if (property.objectReferenceValue != system)
            {
                property.objectReferenceValue = system;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// A new burst's **starting point**, so a fresh system is never blank. What it is not, any
        /// more, is the burst's look.
        /// <para>
        /// D-053 said the tool creates a look once and the designer owns it afterwards, which is right
        /// wherever the designer can actually reach it. Every number asked for across three rounds of
        /// this effect — size, count, spread, wander, gravity, the shape of the motion — lived here,
        /// and each one cost a prefab rebuild to change. So the emission shape, the noise and the
        /// motion are the view's now, written per burst out of the config (D-082); what is still the
        /// prefab's is the renderer and the fade curves. The values below are what a system carries
        /// before the first burst overwrites them, and a sane blank is worth having.
        /// </para>
        /// <para>
        /// It emits nothing on its own — the view says when, how many and from where — and it
        /// simulates in world space, so a spark stays where it was thrown if the board is re-laid.
        /// </para>
        /// <para>
        /// The two bursts differ in one thing that is not decoration: <see cref="Gravity.None"/> is what
        /// makes the placement burst's authored climb *exact*, since with no pull the distance is speed
        /// times lifetime and the config's height means what it says. The finish burst falls, which is
        /// the "go up and come back down" the reference shows.
        /// </para>
        /// </summary>
        private static void DressBurst(ParticleSystem system, int maxParticles, Gravity gravity)
        {
            bool falls = gravity == Gravity.Falls;

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = falls ? new ParticleSystem.MinMaxCurve(0.9f) : new ParticleSystem.MinMaxCurve(0.55f);

            // Small, because the spec is "little ones": a spark the size of a brick's face reads as a
            // flash rather than as a handful of sparks.
            main.startSize = falls
                ? new ParticleSystem.MinMaxCurve(0.10f, 0.22f)
                : new ParticleSystem.MinMaxCurve(0.07f, 0.15f);

            // Zero, because the push is the view's: it arrives per burst through EmitParams, out of the
            // config, and a start speed here would be a second opinion about how fast these travel.
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.gravityModifier = falls ? new ParticleSystem.MinMaxCurve(0.7f, 1.3f) : new ParticleSystem.MinMaxCurve(0f);
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            // A starting point only: the view sizes this per burst out of the config, because "how
            // much of the brick's base do they come off" is exactly the kind of number that was
            // unreachable here (D-082).
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = falls ? 0.28f : 0.18f;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            // The placement burst fades the whole way — "solarak kaybolacak" — while the finish burst
            // holds its light and lets go at the end, so the fall is visible before it goes.
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = falls
                    ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.55f, 0.5f), new GradientAlphaKey(0f, 1f) },
            });

            var renderer = system.GetComponent<ParticleSystemRenderer>();

            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sortingOrder = CoverSortingOrder;
                renderer.receiveShadows = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// What must be true of a burst however it was tuned, written on every build.
        /// <para>
        /// The split is D-053's, applied to a repair rather than to a creation: sizes, colours, noise
        /// and curves are the designer's the moment the system exists, but a system that emits on its
        /// own, simulates in the wrong space, or carries the wrong pull is not tuned — it is broken, and
        /// the view's arithmetic is wrong about it. Leaving these to creation alone is exactly what let
        /// a mixed-mode velocity curve survive in a shipped prefab (D-078).
        /// </para>
        /// </summary>
        private static bool RepairBurst(ParticleSystem system, string who, int maxParticles, Gravity gravity, List<string> notes)
        {
            bool changed = false;
            ParticleSystem.MainModule main = system.main;

            if (main.playOnAwake)
            {
                main.playOnAwake = false;
                notes.Add(who + ": play-on-awake off — the view says when a burst happens");
                changed = true;
            }

            if (main.simulationSpace != ParticleSystemSimulationSpace.World)
            {
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                notes.Add(who + ": simulating in world space — a spark stays where it was thrown");
                changed = true;
            }

            if (main.maxParticles < maxParticles)
            {
                main.maxParticles = maxParticles;
                notes.Add(who + ": max particles raised to " + maxParticles);
                changed = true;
            }

            // D-078 cleared the placement burst's gravity here every build, because its climb was one
            // flat speed and any pull would have made the authored height a lie. That is reversed on
            // purpose: the rise decelerates now, so the pull is *computed per burst* from that same
            // height and duration (D-081) — and a repair pass that zeroed it every build would be
            // fighting the view for a value the view is the authority on. What stays repaired is what
            // no one tunes and nothing computes.

            ParticleSystem.EmissionModule emission = system.emission;

            if (emission.rateOverTime.constantMax != 0f || emission.rateOverDistance.constantMax != 0f)
            {
                emission.rateOverTime = 0f;
                emission.rateOverDistance = 0f;
                notes.Add(who + ": emission rates cleared — every particle comes from an explicit Emit");
                changed = true;
            }

            changed |= RepairVelocityAxes(system, who, notes);
            return changed;
        }

        /// <summary>Takes a child this tool has replaced off a prefab, if it is still there.</summary>
        private static bool RemoveRetiredChild(GameObject owner, string childName, List<string> notes)
        {
            Transform retired = owner.transform.Find(childName);

            if (retired == null)
            {
                return false;
            }

            Object.DestroyImmediate(retired.gameObject);
            notes.Add(owner.name + "/" + childName + " retired; its work is split across two systems now");
            return true;
        }

        /// <summary>
        /// Puts a Velocity over Lifetime module's three axes into one curve mode, which Unity requires
        /// and which nothing but a script ever breaks.
        /// <para>
        /// This is the console's "Particle Velocity curves must all be in the same mode", logged every
        /// frame a brick flew: <see cref="DressPlume"/> wrote <c>y</c> as a two-constant range and left
        /// <c>x</c> and <c>z</c> at a single constant. In the Inspector the three move together, so the
        /// fault only exists on a module a script has touched — which is why it belongs here and not in
        /// a person's checklist.
        /// </para>
        /// </summary>
        private static bool RepairVelocityAxes(ParticleSystem system, string who, List<string> notes)
        {
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;

            if (!velocity.enabled)
            {
                return false;
            }

            ParticleSystemCurveMode mode = velocity.y.mode;

            if (velocity.x.mode == mode && velocity.z.mode == mode)
            {
                return false;
            }

            velocity.x = AxisInMode(velocity.x, mode);
            velocity.z = AxisInMode(velocity.z, mode);
            notes.Add(who + ": velocity x and z put into " + mode + " to match y — Unity refuses a mixed module");
            return true;
        }

        /// <summary>
        /// The same axis expressed in another curve mode, carrying whatever value it already held. A
        /// value is only readable as a constant when the axis *is* a constant; every other mode is a
        /// curve this tool never authored, so it starts the promoted axis at zero rather than inventing
        /// a shape for it.
        /// </summary>
        private static ParticleSystem.MinMaxCurve AxisInMode(ParticleSystem.MinMaxCurve axis, ParticleSystemCurveMode mode)
        {
            float value = axis.mode == ParticleSystemCurveMode.Constant ? axis.constant : 0f;

            switch (mode)
            {
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(value, value);
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, value));
                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(
                        1f,
                        AnimationCurve.Constant(0f, 1f, value),
                        AnimationCurve.Constant(0f, 1f, value));
                default:
                    return new ParticleSystem.MinMaxCurve(value);
            }
        }

        /// <summary>
        /// A spark's own additive material — the same drawing and the same shader as the plume's, and
        /// a different answer to one question: whether a brick may hide it.
        /// <para>
        /// The bursts used the plume's material until they were seen not to appear at all, and the
        /// reason was written in the shader all along: it is the *glow's* shader, whose depth test is
        /// what leaves only a rim showing behind a lifted run. A spark is emitted inside a brick's own
        /// mesh — under it for a placement, in the middle of a full column for a finish — so the same
        /// rule that makes the glow correct made every burst invisible (D-079).
        /// </para>
        /// <para>
        /// The plume material is replaced where it is found, not only a missing or default one: a pass
        /// that fills only an empty slot cannot correct a choice it made itself last task, which is the
        /// lesson D-071 recorded and this project keeps re-learning.
        /// </para>
        /// </summary>
        private static bool EnsureSparkleMaterial(ParticleSystem sparkle, List<string> notes)
        {
            var renderer = sparkle == null ? null : sparkle.GetComponent<ParticleSystemRenderer>();

            if (renderer == null)
            {
                return false;
            }

            Material current = renderer.sharedMaterial;

            // Anything this pass has ever put here is replaceable by what it wants there now. Filling
            // only an empty slot cannot correct a choice the pass made itself last task, which is the
            // trap this project has now met eight times (D-071, D-078, D-079).
            if (current != null
                && !current.name.StartsWith("Default-Particle")
                && !current.name.StartsWith("Sprite")
                && current != AssetDatabase.LoadAssetAtPath<Material>(PlumeMaterial)
                && current != AssetDatabase.LoadAssetAtPath<Material>(SparkMaterial))
            {
                return false;
            }

            Material additive = SymbolSparkMaterialAsset(notes);

            if (additive == null || current == additive)
            {
                return false;
            }

            renderer.sharedMaterial = additive;
            notes.Add("sparks now burn with " + additive.name + ", which no brick can hide");
            return true;
        }

        /// <summary>
        /// The spark material, created once and then left alone (D-052). It is the plume's texture and
        /// the glow's shader with one property changed — <c>_ZTest Always</c> — because a spark is
        /// light in front of the board and the glow is light behind a brick, and one material cannot be
        /// both. A second material rather than a second shader: the difference is a single piece of
        /// render state, and two copies of a shader this project fought over for three rounds (D-064,
        /// D-065) is the last thing it needs.
        /// </summary>
        /// <summary>
        /// The symbol burst's material: the glow's shader at <c>_ZTest Always</c> and no texture, so
        /// what is drawn is the mesh's silhouette lit by the particle's own colour. Created once and
        /// then left alone (D-052).
        /// </summary>
        private static Material SymbolSparkMaterialAsset(List<string> notes)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(SymbolSparkMaterial);

            if (existing != null)
            {
                return existing;
            }

            Shader additive = Shader.Find(GlowShader);

            if (additive == null)
            {
                notes.Add("shader '" + GlowShader + "' is missing, so the symbol sparks will blend instead of burn");
                return null;
            }

            // No _MainTex: the shader declares it "white", which is exactly what a shape wants when the
            // shape is the geometry. A texture here would be sampled over the symbol's own UVs, which
            // were authored for a brick face and mean nothing on a particle.
            var material = new Material(additive) { name = Path.GetFileNameWithoutExtension(SymbolSparkMaterial) };
            material.SetFloat(ZTestProperty, (float)UnityEngine.Rendering.CompareFunction.Always);
            EnsureFolder("Assets/Art", "Materials");
            AssetDatabase.CreateAsset(material, SymbolSparkMaterial);
            notes.Add(SymbolSparkMaterial + " created — the symbol's own outline, burning, in front of the board");
            return material;
        }

        private static Material SparkMaterialAsset(List<string> notes)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterial);

            if (existing != null)
            {
                return existing;
            }

            Shader additive = Shader.Find(GlowShader);

            if (additive == null)
            {
                notes.Add("shader '" + GlowShader + "' is missing, so the sparks will blend instead of burn");
                return null;
            }

            Texture2D puff = LoadTexture(PlumeTexture);

            if (puff == null)
            {
                notes.Add("no " + PlumeTexture + " texture, so the sparks would be drawn as squares");
                return null;
            }

            var material = new Material(additive) { name = Path.GetFileNameWithoutExtension(SparkMaterial) };
            material.SetFloat(ZTestProperty, (float)UnityEngine.Rendering.CompareFunction.Always);
            material.mainTexture = puff;
            EnsureFolder("Assets/Art", "Materials");
            AssetDatabase.CreateAsset(material, SparkMaterial);
            notes.Add(SparkMaterial + " created — additive, and never hidden by a brick");
            return material;
        }

        private static bool EnsureSettledShadow(GameObject column, SerializedObject serialized, SpriteRenderer slot, List<string> notes)
        {
            SerializedProperty property = serialized.FindProperty("settledShadow");

            if (property == null)
            {
                return false;
            }

            Transform existing = column.transform.Find(SettledShadowName);

            if (existing != null)
            {
                if (property.objectReferenceValue != null)
                {
                    return false;
                }

                property.objectReferenceValue = existing.GetComponent<SpriteRenderer>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }

            Sprite sprite = LoadSprite(SettledShadowSprite);

            if (sprite == null)
            {
                notes.Add("no " + SettledShadowSprite + " sprite, so a finished column will only darken");
                return false;
            }

            Transform cover = column.transform.Find("Cover");
            float depth = cover != null ? cover.localPosition.z : -slot.transform.localPosition.z;

            var shadow = new GameObject(SettledShadowName);
            shadow.transform.SetParent(column.transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0f, depth);

            var renderer = shadow.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = sprite.bounds.size;
            renderer.sortingLayerID = slot.sortingLayerID;
            renderer.sortingOrder = CoverSortingOrder;
            renderer.color = new Color(1f, 1f, 1f, 0f);

            property.objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            notes.Add(column.name + " gained its settled shadow");
            return true;
        }

        /// <summary>
        /// Whether a material is the one the plate's FBX brought in — the magenta one, on a shader
        /// URP does not know. Nothing else is replaced: a material the user assigned is theirs, and
        /// only a missing or imported one is a defect this tool should fix (D-053).
        /// </summary>
        private static bool IsFbxMaterial(Material material)
        {
            return material == null || AssetDatabase.GetAssetPath(material) == BasePlateMesh;
        }

        /// <summary>
        /// The plate's material, generated the way a brick's is (D-020): a URP Lit asset whose base
        /// colour is *sampled from the tray sprite the plate sits in*, so the art stays the one
        /// authority for what a column is coloured and a re-exported tray brings the plate with it.
        /// The FBX's own material is not used — it comes in on a shader URP does not know, which is
        /// what draws it magenta (D-052).
        /// <para>
        /// Created once and then left alone, so a colour the user tunes by hand survives every run.
        /// </para>
        /// </summary>
        private static Material BasePlateMaterialAsset(List<string> notes)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(BasePlateMaterial);

            if (existing != null)
            {
                return existing;
            }

            Shader lit = Shader.Find(BlockSkinFactory.UrpLitShader);

            if (lit == null)
            {
                notes.Add("shader '" + BlockSkinFactory.UrpLitShader + "' is missing, so the base plate keeps the material its FBX brought");
                return null;
            }

            var material = new Material(lit) { name = "Slot_Base" };
            Color sampled;

            if (TraySampleColour(out sampled))
            {
                material.SetColor(BlockSkinFactory.BaseColourProperty, sampled);
            }
            else
            {
                notes.Add("the tray sprite could not be sampled, so Slot_Base.mat was left at the shader's own colour");
            }

            EnsureFolder("Assets/Art", "Materials");
            AssetDatabase.CreateAsset(material, BasePlateMaterial);
            notes.Add("Slot_Base.mat created from the tray's colour");
            return material;
        }

        /// <summary>
        /// The average colour of the tray's drawn interior, read from the PNG on disk rather than
        /// through the imported texture: a sprite's texture is not readable unless someone ticks
        /// Read/Write Enabled, and this pass has no business changing an import setting to peek at
        /// a pixel. The middle half of the image is sampled, which is interior on every tray the
        /// pack or the user has drawn.
        /// </summary>
        private static bool TraySampleColour(out Color colour)
        {
            colour = Color.white;
            string path = GameplaySprites + "/" + ColumnTraySprite + ".png";

            if (!File.Exists(path))
            {
                return false;
            }

            var texture = new Texture2D(2, 2);

            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    return false;
                }

                int left = texture.width / 4;
                int right = texture.width - left;
                int bottom = texture.height / 4;
                int top = texture.height - bottom;

                float r = 0f;
                float g = 0f;
                float b = 0f;
                int counted = 0;

                for (int y = bottom; y < top; y++)
                {
                    for (int x = left; x < right; x++)
                    {
                        Color pixel = texture.GetPixel(x, y);

                        if (pixel.a < 0.5f)
                        {
                            continue;
                        }

                        r += pixel.r;
                        g += pixel.g;
                        b += pixel.b;
                        counted++;
                    }
                }

                if (counted == 0)
                {
                    return false;
                }

                colour = new Color(r / counted, g / counted, b / counted, 1f);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Ice and Covered are prefab <em>variants</em> of Column, not copies: they differ by
        /// art and by the overlay they carry, so a change to the base column has to reach them
        /// (blueprint prefab inventory, scene-structure.md).
        /// </summary>
        private static void BuildColumnVariant(GameObject basePrefab, string variantName, string slotSpriteName, bool covered, List<string> created)
        {
            string path = PrefabFolder + "/" + variantName + ".prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = variantName;

            ColumnView view = instance.GetComponent<ColumnView>();
            SerializedObject serialized = new SerializedObject(view);

            if (slotSpriteName != null)
            {
                Sprite iceSprite = LoadSprite(slotSpriteName);

                if (iceSprite != null)
                {
                    var slot = (SpriteRenderer)serialized.FindProperty("slot").objectReferenceValue;
                    slot.sprite = iceSprite;
                }

                // What it wears once it thaws: the ice art is integrated, so thawing is a sprite
                // swap back to the normal column (D-030) — which is now the hand-drawn tray, or a
                // thawed column would be the only one still wearing the pack's.
                serialized.FindProperty("thawedSlot").objectReferenceValue = LoadSprite(ColumnTraySprite);
            }

            if (covered)
            {
                serialized.FindProperty("coverTopCap").objectReferenceValue = LoadSprite("cover_top_cap");
                serialized.FindProperty("coverCell").objectReferenceValue = LoadSprite("cover_cell_repeat");
                serialized.FindProperty("coverSeparator").objectReferenceValue = LoadSprite("cover_separator");
                serialized.FindProperty("coverBottomCap").objectReferenceValue = LoadSprite("cover_bottom_cap");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            created.Add(variantName);
        }

        private static void BuildBlockPrefab(float brickScale, List<string> created)
        {
            string path = PrefabFolder + "/Block.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            var root = new GameObject("Block");
            MeshFilter filter = root.AddComponent<MeshFilter>();
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            BlockView view = root.AddComponent<BlockView>();

            if (!Mathf.Approximately(brickScale, 1f))
            {
                // One cell is one unit, so a brick has to fill one unit of floor. Scaling by the
                // measured footprint keeps every symbol the same size instead of trusting the
                // FBX export scale.
                root.transform.localScale = new Vector3(brickScale, brickScale, brickScale);
                Debug.Log("[Colorful Sort] The brick meshes do not measure one cell across, so Block is scaled by " +
                          brickScale.ToString("0.###") + " to fill one.");
            }

            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("meshFilter").objectReferenceValue = filter;
            serialized.FindProperty("meshRenderer").objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            created.Add("Block");
        }

        private static SpriteRenderer CreateSlotRenderer(Transform parent, Sprite sprite, float standOff)
        {
            Transform slotTransform = CreateChild(parent, "Slot", standOff);
            var renderer = slotTransform.gameObject.AddComponent<SpriteRenderer>();

            renderer.sprite = sprite;
            ApplySlotDrawMode(renderer);
            renderer.size = sprite.bounds.size;
            renderer.sortingOrder = ColumnSortingOrder;

            return renderer;
        }

        /// <summary>
        /// The draw mode is the sprite's own answer, not a setting to remember: art whose middle
        /// band measures a whole number of cells repeats it (D-007, capacity you can count), and a
        /// flat tray stretches. Both respect <c>size</c>; `Simple` is the mode that would ignore it.
        /// </summary>
        private static void ApplySlotDrawMode(SpriteRenderer renderer)
        {
            ColumnMetrics metrics = ColumnMetrics.FromSprite(renderer.sprite);

            renderer.drawMode = metrics.TilesPerCell ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced;
            renderer.tileMode = SpriteTileMode.Continuous;
        }

        private static Transform CreateChild(Transform parent, string childName, float depth)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = new Vector3(0f, 0f, depth);
            return child.transform;
        }

        /// <summary>
        /// The largest brick across the twelve symbol meshes, axis by axis: the width and depth
        /// say whether a brick overflows a cell, and the depth is also what the slot art and the
        /// cover have to stand clear of. Falls back to one cell if no mesh is found, which is what
        /// the art contract says a brick is anyway.
        /// </summary>
        private static Vector3 BrickSize()
        {
            var largest = Vector3.zero;

            foreach (Mesh mesh in BrickMeshes())
            {
                largest = Vector3.Max(largest, mesh.bounds.size);
            }

            return largest == Vector3.zero ? Vector3.one : largest;
        }

        /// <summary>
        /// How far the tallest brick reaches above its own origin — the top of its studs. It is what
        /// the base plate is aligned against, because a plate whose studs end where a brick's studs
        /// end is a plate the brick above sits on rather than hovers over (D-051).
        /// </summary>
        private static float BrickTopY()
        {
            float top = 0f;

            foreach (Mesh mesh in BrickMeshes())
            {
                top = Mathf.Max(top, mesh.bounds.max.y);
            }

            // Half a cell is what the art contract says a brick's origin sits in the middle of.
            return top <= 0f ? 0.5f : top;
        }

        /// <summary>
        /// The symbol meshes, and not the floor: <c>Block_Base</c> lives in the same folder and is a
        /// plate, so measuring it as a brick would give the wrong footprint and the wrong stud line.
        /// </summary>
        private static IEnumerable<Mesh> BrickMeshes()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { BlockModels }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (path == BasePlateMesh)
                {
                    continue;
                }

                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var mesh = asset as Mesh;

                    if (mesh == null)
                    {
                        continue;
                    }

                    yield return mesh;
                }
            }
        }

        // ------------------------------------------------------------ scene

        private static void EnsureDirectionalLight(Scene scene, List<string> notes)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<Light>() != null)
                {
                    return;
                }
            }

            var lightObject = new GameObject("--Light--");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;

            // Unity's own default rotation for a new directional light. The angle is a look
            // decision and the scene is its authority — change it in the scene, not in code.
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            notes.Add("added --Light-- (bricks are lit, so the embossed symbol reads)");
        }

        private static Transform FindRoot(Scene scene, string rootName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return root.transform;
                }
            }

            return null;
        }

        /// <summary>
        /// The light behind a lifted run: one renderer for the whole board, since only one run is ever
        /// up. It is created dark and disabled — `BoardView` tints it to the run's colour, sizes it to
        /// the run's length and parents it to the run's bottom brick, which is what makes it follow the
        /// rock with no per-frame code (D-060).
        /// <para>
        /// Its sorting order sits between the tray and the bricks: the tray must not paint over it,
        /// and the bricks are opaque geometry that hides all of it but the rim, which is the effect.
        /// </para>
        /// </summary>
        private static SpriteRenderer EnsureGlow(Transform columns, List<string> notes)
        {
            Transform existing = columns.Find(GlowName);

            if (existing != null)
            {
                // Repaired, not just created: this object was made before the additive material
                // existed, and a tool that only fills in what it creates works exactly once — the
                // mistake V-3b already made with the base plate (D-061).
                var found = existing.GetComponent<SpriteRenderer>();
                EnsureGlowMaterial(found, notes);
                return found;
            }

            Sprite sprite = LoadSprite(GlowSprite);

            if (sprite == null)
            {
                notes.Add("no " + GlowSprite + " sprite, so a lifted run will not glow");
                return null;
            }

            var glow = new GameObject(GlowName);
            glow.transform.SetParent(columns, false);

            var renderer = glow.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = sprite.bounds.size;
            renderer.sortingOrder = GlowSortingOrder;
            EnsureGlowMaterial(renderer, notes);
            renderer.enabled = false;

            notes.Add("added " + columns.name + "/" + GlowName);
            return renderer;
        }

        /// <summary>
        /// Puts the additive material on the glow, whether the renderer was just made or has been
        /// sitting there since before the material existed. Only a missing material or Unity's own
        /// sprite default is replaced: a material somebody chose on purpose is theirs, and an
        /// alpha-blended default is not a choice, it is the reason the glow does not glow (D-061).
        /// </summary>
        private static void EnsureGlowMaterial(SpriteRenderer renderer, List<string> notes)
        {
            if (renderer == null)
            {
                return;
            }

            Material current = renderer.sharedMaterial;

            // Unity's own sprite materials are the ones called "Sprite-…" or "Sprites-…"; anything
            // else in that slot is somebody's decision and is left alone.
            if (current != null && !current.name.StartsWith("Sprite"))
            {
                return;
            }

            Material additive = GlowMaterialAsset(notes);

            if (additive == null || current == additive)
            {
                return;
            }

            renderer.sharedMaterial = additive;
            notes.Add("the glow now burns with " + additive.name);
        }

        /// <summary>
        /// The glow's material asset: created once from the project's additive glow shader and then
        /// left alone, the way `Slot_Base.mat` is (D-052). One material lights every colour, because
        /// the tint travels in the renderer's vertex colour rather than in the material (D-061).
        /// </summary>
        private static Material GlowMaterialAsset(List<string> notes)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterial);

            if (existing != null)
            {
                return existing;
            }

            Shader additive = Shader.Find(GlowShader);

            if (additive == null)
            {
                notes.Add("shader '" + GlowShader + "' is missing, so the glow will blend instead of burn");
                return null;
            }

            var material = new Material(additive) { name = "Block_Glow" };
            EnsureFolder("Assets/Art", "Materials");
            AssetDatabase.CreateAsset(material, GlowMaterial);
            notes.Add("Block_Glow.mat created");
            return material;
        }

        /// <summary>
        /// Gives the Block prefab the plume it leaves while it flies (D-074), and gives it to the
        /// prefab that is already there — which is the whole point of this being an ensure rather
        /// than part of <see cref="BuildBlockPrefab"/>. That method returns the moment the prefab
        /// exists, so anything added to a brick after the first run can only arrive here; this
        /// project has paid for that lesson five times over (D-071).
        /// <para>
        /// What it writes is existence: the child, the particle system, the additive material and the
        /// reference in <c>BlockView</c>. What it writes only once, at creation, is the look — the
        /// noise, the drift and the fade curves — because those are what somebody tunes by eye, and a
        /// tool that rewrote them every run would be undoing their work (D-053).
        /// </para>
        /// <para>
        /// It also takes away the <c>Trail</c> child an earlier version of this tool added: a line was
        /// the wrong reading of the reference, and leaving it behind would draw both.
        /// </para>
        /// </summary>
        private static void EnsureBlockPlume(List<string> notes)
        {
            string path = PrefabFolder + "/Block.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            try
            {
                var view = contents.GetComponent<BlockView>();

                if (view == null)
                {
                    notes.Add("Block.prefab carries no BlockView, so it can have no plume");
                    return;
                }

                bool changed = false;

                Transform retired = contents.transform.Find(RetiredTrailName);

                if (retired != null)
                {
                    Object.DestroyImmediate(retired.gameObject);
                    notes.Add("the old " + RetiredTrailName + " child is gone; a flying brick leaves a plume now");
                    changed = true;

                    // Its material goes with it. Nothing else ever referenced it — this tool made it
                    // one task ago for that child alone — and an orphan in Art/Materials/ is a thing
                    // somebody has to work out the purpose of later.
                    if (AssetDatabase.LoadAssetAtPath<Material>(RetiredTrailMaterial) != null)
                    {
                        AssetDatabase.DeleteAsset(RetiredTrailMaterial);
                        notes.Add(RetiredTrailMaterial + " deleted with it");
                    }
                }

                Transform existing = contents.transform.Find(PlumeName);

                if (existing == null)
                {
                    var child = new GameObject(PlumeName);
                    child.transform.SetParent(contents.transform, false);
                    existing = child.transform;
                    changed = true;
                }

                var plume = existing.GetComponent<ParticleSystem>();

                if (plume == null)
                {
                    plume = existing.gameObject.AddComponent<ParticleSystem>();
                    DressPlume(plume);
                    changed = true;
                }

                // The material is ensured on every run, unlike the look: a plume drawing with
                // Unity's default particle material does not add light, it blends — which is the
                // exact fault the glow was chased through three times (D-061).
                if (EnsurePlumeMaterial(plume, notes))
                {
                    changed = true;
                }

                // Also every run, and this is the prefab the fault is actually sitting in: the plume
                // was created before DressPlume wrote all three velocity axes, and a dressing pass that
                // only runs at creation can never reach it (D-071's trap, seventh time).
                if (RepairVelocityAxes(plume, "Block/" + PlumeName, notes))
                {
                    changed = true;
                }

                var serialized = new SerializedObject(view);
                FillIfEmpty(serialized, "plume", plume, notes);

                if (serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    changed = true;
                }

                if (!changed)
                {
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                notes.Add("Block now carries its " + PlumeName);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// A new plume's look, written once.
        /// <para>
        /// The three decisions that make it a plume rather than a line of beads: it simulates in
        /// <em>world</em> space, so a puff stays where it was dropped instead of being dragged along
        /// by the brick; it emits by distance, so the corridor is painted by the brick's own movement
        /// and stays the same density however fast the flight is; and its noise pushes each puff off
        /// the path, which is what makes the column wander the way smoke does.
        /// </para>
        /// <para>
        /// Lifetime, size and density are left as sensible starting values and are overwritten from
        /// the config on every flight — those three have to agree with the flight, so the asset owns
        /// them (D-074).
        /// </para>
        /// </summary>
        private static void DressPlume(ParticleSystem plume)
        {
            ParticleSystem.MainModule main = plume.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.5f;
            main.startSize = 0.7f;
            main.startSpeed = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.gravityModifier = 0f;
            main.maxParticles = PlumeMaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            // No rate over *time*: a brick standing still — one waiting its turn in the stagger —
            // must lay nothing at all.
            ParticleSystem.EmissionModule emission = plume.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 12f;

            ParticleSystem.ShapeModule shape = plume.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            // The slow rise that reads as smoke leaving rather than paint sitting there. All three axes
            // are written, and written in the same mode: Unity refuses a module whose x, y and z are in
            // different ones, and writing only y is what put "Particle Velocity curves must all be in
            // the same mode" in the console on every flight (D-078).
            ParticleSystem.VelocityOverLifetimeModule velocity = plume.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(PlumeDrift * 0.5f, PlumeDrift);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.NoiseModule noise = plume.noise;
            noise.enabled = true;
            noise.strength = PlumeNoiseStrength;
            noise.frequency = PlumeNoiseFrequency;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            // Grows a little as it drifts and thins out, which is what turns a row of puffs into one
            // column of light.
            ParticleSystem.SizeOverLifetimeModule size = plume.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.75f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0.55f)));

            // The additive pass multiplies by alpha, so this gradient *is* the fade in and out.
            ParticleSystem.ColorOverLifetimeModule colour = plume.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0f, 1f),
                },
            });

            var renderer = plume.GetComponent<ParticleSystemRenderer>();

            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
                renderer.sortingOrder = GlowSortingOrder;
                renderer.receiveShadows = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// Puts the additive material on a plume, whether it was just made or has been there since
        /// before the material existed. Returns whether anything changed. Only a missing material or
        /// Unity's own default is replaced — a material somebody chose is theirs (D-061).
        /// </summary>
        private static bool EnsurePlumeMaterial(ParticleSystem plume, List<string> notes)
        {
            var renderer = plume == null ? null : plume.GetComponent<ParticleSystemRenderer>();

            if (renderer == null)
            {
                return false;
            }

            Material current = renderer.sharedMaterial;

            if (current != null && !current.name.StartsWith("Default-Particle") && !current.name.StartsWith("Sprite"))
            {
                return false;
            }

            Material additive = PlumeMaterialAsset(notes);

            if (additive == null || current == additive)
            {
                return false;
            }

            renderer.sharedMaterial = additive;
            notes.Add("the plume now burns with " + additive.name);
            return true;
        }

        /// <summary>
        /// The plume's material: the same additive shader the glow uses, with the soft puff as its
        /// texture, created once and then left alone. It is a second asset rather than the glow's
        /// because the two carry different textures — the shader is shared, the material is not.
        /// </summary>
        private static Material PlumeMaterialAsset(List<string> notes)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(PlumeMaterial);

            if (existing != null)
            {
                return existing;
            }

            Shader additive = Shader.Find(GlowShader);

            if (additive == null)
            {
                notes.Add("shader '" + GlowShader + "' is missing, so a flying brick will blend instead of burn");
                return null;
            }

            var material = new Material(additive) { name = "Block_Plume" };
            Texture2D puff = LoadTexture(PlumeTexture);

            if (puff == null)
            {
                notes.Add("no " + PlumeTexture + " texture, so the plume would be drawn as squares");
                return null;
            }

            material.mainTexture = puff;
            EnsureFolder("Assets/Art", "Materials");
            AssetDatabase.CreateAsset(material, PlumeMaterial);
            notes.Add("Block_Plume.mat created");
            return material;
        }

        private static Transform RequireChild(Transform parent, string childName, List<string> notes)
        {
            Transform existing = parent.Find(childName);

            if (existing != null)
            {
                return existing;
            }

            var child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            notes.Add("added " + parent.name + "/" + childName);
            return child.transform;
        }

        private static T Require<T>(GameObject target, List<string> notes, string description) where T : Component
        {
            T existing = target.GetComponent<T>();

            if (existing != null)
            {
                return existing;
            }

            notes.Add("added " + description);
            return target.AddComponent<T>();
        }

        private static void FillIfEmpty(SerializedObject serialized, string propertyName, Object value, List<string> notes)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);

            if (property == null)
            {
                Debug.LogWarning("[Colorful Sort] " + serialized.targetObject.GetType().Name + " has no field '" + propertyName + "'.");
                return;
            }

            if (property.objectReferenceValue != null)
            {
                return;
            }

            if (value == null)
            {
                notes.Add(propertyName + " left empty (its asset does not exist yet)");
                return;
            }

            property.objectReferenceValue = value;
            notes.Add("wired " + propertyName);
        }

        private static T LoadPrefabComponent<T>(string prefabName) where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + prefabName + ".prefab");
            return prefab == null ? null : prefab.GetComponent<T>();
        }

        /// <summary>
        /// The same file, taken as a texture rather than as a sprite: a particle material samples a
        /// texture, and the import pass makes every gameplay PNG a sprite — which carries one.
        /// </summary>
        private static Texture2D LoadTexture(string textureName)
        {
            string path = GameplaySprites + "/" + textureName + ".png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (texture == null)
            {
                Debug.LogError("[Colorful Sort] " + path + " is missing. Run Tools > Colorful Sort > Apply Art Import Settings first.");
            }

            return texture;
        }

        private static Sprite LoadSprite(string spriteName)
        {
            string path = GameplaySprites + "/" + spriteName + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                Debug.LogError("[Colorful Sort] " + path + " is not a sprite (or is missing). Run Tools > Colorful Sort > Apply Art Import Settings first.");
            }

            return sprite;
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
