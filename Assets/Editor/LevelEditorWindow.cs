#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ColorfulSort.Board;
using ColorfulSort.Content;
using ColorfulSort.View;
using UnityEditor;
using UnityEngine;

namespace ColorfulSort.EditorTools
{
    /// <summary>
    /// Where levels are authored (D-010): draw the layout grid, pick each column's kind
    /// and capacity, paint its cells from the project's own palette, and read the verdict
    /// <see cref="LevelDefinition.Validate"/> gives — which is every rule the game itself
    /// would run, so a level that is green here cannot fail on a device for a reason this
    /// window could have caught.
    /// <para>
    /// It writes through <c>SerializedObject</c>/<c>SerializedProperty</c> rather than
    /// through setters added to <c>Content</c> (D-032): the authored types stay read-only
    /// to code, and Undo, dirty-marking and asset saving come from Unity for free. The
    /// price is a dependency on serialized field *names*, which is why every path is
    /// resolved once when a level is bound — a rename then fails loudly here instead of
    /// silently writing nothing.
    /// </para>
    /// <para>
    /// It also holds no content of its own. The palette is the single
    /// <see cref="BlockSkinSet"/> (D-003, D-020) and every rule about what a legal column
    /// looks like belongs to <c>ColumnData</c>, which is why this window offers shapes but
    /// never enforces them: a second rulebook in a tool is how the two drift apart.
    /// </para>
    /// </summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        /// <summary>What a click on a cell does.</summary>
        private enum Brush
        {
            Paint,
            Erase,
        }

        private const string LevelsFolder = "Assets/Data/Levels";
        private const string LevelsFile = LevelsFolder + "/Levels.json";
        private const string LevelDatabaseAsset = LevelsFolder + "/LevelDatabase.asset";
        private const string PathLevels = "levels";

        /// <summary>The grid a new level starts on — the reference's own shape, and room to work in.</summary>
        private const int BlankRows = 2;

        private const int BlankColumns = 6;

        // The serialized names this window writes through — the price of D-032, paid in
        // one place and checked in CheckPaths().
        private const string PathLevelIndex = "levelIndex";
        private const string PathDifficulty = "difficulty";
        private const string PathLayoutRows = "layoutRows";
        private const string PathLayoutColumns = "layoutColumns";
        private const string PathColumns = "columns";
        private const string PathColumnCells = "columnCells";
        private const string PathKind = "kind";
        private const string PathCapacity = "capacity";
        private const string PathCells = "cells";
        private const string PathThaw = "thawAfterCompletions";
        private const string PathCoverKey = "coverKeyColourId";
        private const string PathColourId = "colourId";
        private const string PathHidden = "hidden";

        /// <summary>
        /// The plaque number of the level being edited, kept across a domain reload so a script
        /// recompile does not throw the author back to nothing. Not the level itself: a level is a
        /// transient object now and does not survive one (D-085).
        /// </summary>
        [SerializeField]
        private int openLevelIndex = -1;

        /// <summary>Every level in the file, decoded. This window edits the whole file, not a level.</summary>
        private List<LevelDefinition> loaded = new List<LevelDefinition>();

        /// <summary>The one being edited, which is an element of <see cref="loaded"/> and never an asset.</summary>
        private LevelDefinition level;

        /// <summary>What is wrong with the file, said once at the top rather than per keystroke.</summary>
        private string fileProblem;

        [SerializeField]
        private int selectedColumn = -1;

        [SerializeField]
        private int paintColourId = BlockColourId.MinId;

        [SerializeField]
        private bool paintHidden;

        [SerializeField]
        private Brush brush = Brush.Paint;

        // A brush default, not content: the number is on screen and every column added
        // takes whatever it currently says.
        [SerializeField]
        private ColumnKind newColumnKind = ColumnKind.Normal;

        [SerializeField]
        private int newColumnCapacity = 4;

        [SerializeField]
        private bool movingSelected;

        [SerializeField]
        private bool previewVariant;

        [SerializeField]
        private int variantIndex;

        [SerializeField]
        private Vector2 scroll;

        private SerializedObject serializedLevel;

        private BlockSkinSet skins;

        private string skinSetProblem;

        private string pathProblem;

        // The solvability verdict is deliberately not serialized and not stored in the asset:
        // it is true of one exact board, and the next painted cell makes it a lie.
        private SolverResult solverResult;

        private bool hasSolverResult;

        [MenuItem("Tools/Colorful Sort/Level Editor")]
        public static void Open()
        {
            LevelEditorWindow window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(760f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            FindSkinSet();
            LoadFile();
        }

        private void OnDisable()
        {
            ReleaseLoaded();
        }

        private void OnProjectChange()
        {
            FindSkinSet();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (fileProblem != null)
            {
                EditorGUILayout.HelpBox(fileProblem, MessageType.Warning);
            }

            if (level == null)
            {
                EditorGUILayout.HelpBox(
                    "Pick a level or create one. Every level lives in " + LevelsFile +
                    " — there are no level assets (.claude/rules/data.md).",
                    MessageType.Info);
                return;
            }

            if (serializedLevel == null || serializedLevel.targetObject == null)
            {
                Bind();
            }

            if (pathProblem != null)
            {
                EditorGUILayout.HelpBox(pathProblem, MessageType.Error);
                return;
            }

            if (skinSetProblem != null)
            {
                EditorGUILayout.HelpBox(skinSetProblem, MessageType.Warning);
            }

            serializedLevel.Update();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawLevelFields();
            DrawColumnBar();
            DrawBoard();
            DrawBrush();
            DrawSelectedColumn();

            // Everything above edits the asset and everything below reads it back, so the
            // edits are committed here: a validity banner one click behind the board is
            // worse than no banner at all.
            if (serializedLevel.ApplyModifiedProperties())
            {
                // The board just changed, so a verdict about the old one is now a lie.
                hasSolverResult = false;
            }

            bool shippable = DrawValidation();
            DrawSolvability(shippable);
            DrawVariantPreview(shippable);

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // A list rather than an object picker: there is nothing to pick any more. Every level is
            // a line in one file, and this window opens that file (D-085).
            var names = new string[loaded.Count];

            for (int ordinal = 0; ordinal < loaded.Count; ordinal++)
            {
                names[ordinal] = "Level " + loaded[ordinal].LevelIndex;
            }

            EditorGUI.BeginChangeCheck();
            int chosen = EditorGUILayout.Popup(loaded.IndexOf(level), names, EditorStyles.toolbarPopup, GUILayout.Width(160f));

            if (EditorGUI.EndChangeCheck())
            {
                Select(chosen);
            }

            if (GUILayout.Button("New Level", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                CreateLevel();
            }

            using (new EditorGUI.DisabledScope(loaded.Count == 0))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    SaveFile();
                }
            }

            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                LoadFile();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(LevelsFile, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Reads every level out of the file. The window edits the whole file rather than a level,
        /// because that is what the file is — and the levels it hands back are transient objects it
        /// owns, so they are released before a new read replaces them (D-085).
        /// </summary>
        private void LoadFile()
        {
            ReleaseLoaded();
            loaded = new List<LevelDefinition>();
            fileProblem = null;

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelsFile);

            if (asset == null)
            {
                fileProblem = LevelsFile + " does not exist yet. New Level starts it.";
            }
            else if (!LevelCodec.TryDecodeAll(asset.text, out loaded, out string error))
            {
                loaded = new List<LevelDefinition>();
                fileProblem = LevelsFile + " could not be read: " + error;
            }

            Select(loaded.FindIndex(candidate => candidate.LevelIndex == openLevelIndex));
        }

        /// <summary>
        /// Writes every level back, in plaque order, and points the database at the file.
        /// <para>
        /// The whole file each time, because the whole file is the unit — and it is sorted here rather
        /// than trusted, so the database's "ascending, no duplicates" rule is true by construction
        /// instead of by a button somebody has to remember to press.
        /// </para>
        /// </summary>
        private void SaveFile()
        {
            if (serializedLevel != null)
            {
                serializedLevel.ApplyModifiedPropertiesWithoutUndo();
            }

            loaded.Sort((left, right) => left.LevelIndex.CompareTo(right.LevelIndex));
            File.WriteAllText(LevelsFile, LevelCodec.Encode(loaded));
            AssetDatabase.ImportAsset(LevelsFile);

            EnsureDatabase();
            Select(loaded.FindIndex(candidate => candidate.LevelIndex == openLevelIndex));

            Debug.Log("[Colorful Sort] " + LevelsFile + " written: " + loaded.Count +
                      " level(s) in play order. Ordinal 0 is level " +
                      (loaded.Count == 0 ? "-" : loaded[0].LevelIndex.ToString()) + ".");

            ReportUnshippable();
        }

        /// <summary>
        /// Names every level in the file the board would refuse to open.
        /// <para>
        /// Saving is deliberately **not** blocked on this: half-built work has to be storable, and the
        /// window already shows the open level's own verdict. What was missing is the verdict for the
        /// levels you are *not* looking at — three of them shipped hiding cells in `Normal` columns,
        /// which only `Covered` and `Mystery` may do, and the first anybody heard of it was the level
        /// refusing to open in play (D-097).
        /// </para>
        /// <para>
        /// `LevelCodecTests.ShippedFile_IsReadableAndEveryLevelInItIsShippable` is the guard that
        /// actually holds the line, since it also catches a hand-edited file. This is the same news,
        /// at the moment the mistake is made rather than at the next test run.
        /// </para>
        /// </summary>
        private void ReportUnshippable()
        {
            for (int index = 0; index < loaded.Count; index++)
            {
                LevelDefinition level = loaded[index];

                if (level != null && !level.Validate(out string error))
                {
                    Debug.LogError("[Colorful Sort] Level " + level.LevelIndex +
                                   " is in " + LevelsFile + " and would not open: " + error);
                }
            }
        }

        /// <summary>
        /// Points <c>LevelDatabase</c> at the file. It used to collect level assets; there are none to
        /// collect, so what is left is one reference — still written through
        /// <c>SerializedProperty</c>, for the reason everything here is (D-032).
        /// </summary>
        private void EnsureDatabase()
        {
            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(LevelDatabaseAsset);

            if (database == null)
            {
                database = CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(database, LevelDatabaseAsset);
            }

            var serialized = new SerializedObject(database);
            serialized.FindProperty(PathLevels).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(LevelsFile);
            serialized.ApplyModifiedProperties();
            database.Reload();
            AssetDatabase.SaveAssets();

            if (!database.Validate(out string error))
            {
                Debug.LogError("[Colorful Sort] " + LevelDatabaseAsset + " is not shippable: " + error, database);
            }
        }

        /// <summary>Opens one of the loaded levels, or nothing when the ordinal is not one.</summary>
        private void Select(int ordinal)
        {
            level = ordinal >= 0 && ordinal < loaded.Count ? loaded[ordinal] : null;
            openLevelIndex = level == null ? -1 : level.LevelIndex;
            selectedColumn = -1;
            Bind();
        }

        /// <summary>
        /// Destroys the transient levels this window made. They are marked
        /// <c>HideAndDontSave</c>, so nothing else ever will.
        /// </summary>
        private void ReleaseLoaded()
        {
            if (loaded == null)
            {
                return;
            }

            for (int ordinal = 0; ordinal < loaded.Count; ordinal++)
            {
                if (loaded[ordinal] != null)
                {
                    DestroyImmediate(loaded[ordinal]);
                }
            }

            loaded.Clear();
            level = null;
        }

        private void DrawLevelFields()
        {
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedLevel.FindProperty(PathLevelIndex));
            EditorGUILayout.PropertyField(serializedLevel.FindProperty(PathDifficulty));
            EditorGUILayout.PropertyField(serializedLevel.FindProperty(PathLayoutRows));
            EditorGUILayout.PropertyField(serializedLevel.FindProperty(PathLayoutColumns));
        }

        private void DrawColumnBar()
        {
            SerializedProperty columns = Columns();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(columns.arraySize + " column(s)", GUILayout.Width(90f));

            newColumnKind = (ColumnKind)EditorGUILayout.EnumPopup(newColumnKind, GUILayout.Width(90f));
            newColumnCapacity = EditorGUILayout.IntSlider(newColumnCapacity, ColumnData.MinCapacity, ColumnData.MaxCapacity, GUILayout.Width(170f));

            int free = FirstFreeCell();

            using (new EditorGUI.DisabledScope(free < 0))
            {
                if (GUILayout.Button("Add column", GUILayout.Width(90f)))
                {
                    AddColumn(free);
                }
            }

            using (new EditorGUI.DisabledScope(columns.arraySize == 0))
            {
                if (GUILayout.Button("Remove last", GUILayout.Width(90f)))
                {
                    EnsurePlacementAuthored();
                    columns.arraySize--;
                    serializedLevel.FindProperty(PathColumnCells).arraySize = columns.arraySize;
                    selectedColumn = Mathf.Min(selectedColumn, columns.arraySize - 1);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (free < 0)
            {
                EditorGUILayout.HelpBox(
                    "Every cell of the layout grid is taken. Make the grid bigger to add another column.",
                    MessageType.None);
            }

            SerializedProperty cells = serializedLevel.FindProperty(PathColumnCells);

            if (cells.arraySize != 0 && cells.arraySize != columns.arraySize)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(
                    "The layout places " + cells.arraySize + " column(s) but the level has " + columns.arraySize +
                    ". Until they agree, the board below is drawn with the plain fill.",
                    MessageType.Warning);

                if (GUILayout.Button("Reset placement", GUILayout.Width(120f), GUILayout.Height(38f)))
                {
                    EnsurePlacementAuthored();
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// The layout grid: every cell of it, with the columns standing where the level put
        /// them. Placing is a click — on an empty cell it either moves the selected column
        /// there or drops a new one in, which is the whole reason the grid is drawn rather
        /// than a dense row of whatever exists.
        /// </summary>
        private void DrawBoard()
        {
            List<ColumnSnapshot> board = AuthoredBoard();
            int[] placement = Placement();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                movingSelected
                    ? "Click a grid cell to move column " + selectedColumn + " into it."
                    : "Empty cell: a new column. Header: select it. Cell: paint it.",
                EditorStyles.miniLabel);

            int rows = Mathf.Max(1, serializedLevel.FindProperty(PathLayoutRows).intValue);
            int perRow = Mathf.Max(1, serializedLevel.FindProperty(PathLayoutColumns).intValue);

            Vector2 size = LevelBoardDrawer.Size(board, rows, perRow);
            Rect area = GUILayoutUtility.GetRect(size.x, size.y);
            BoardHit hit = LevelBoardDrawer.Draw(area, board, placement, rows, perRow, skins, selectedColumn, true, false);

            if (!hit.HitGrid)
            {
                return;
            }

            // Moving is armed deliberately, because placing and moving are the same gesture:
            // without the switch, every second click would drag the column just placed.
            if (movingSelected && selectedColumn >= 0 && selectedColumn < board.Count)
            {
                MoveColumn(selectedColumn, hit.GridCell);
                movingSelected = false;
                Repaint();
                return;
            }

            if (!hit.HitColumn)
            {
                AddColumn(hit.GridCell);
                Repaint();
                return;
            }

            selectedColumn = hit.Column;

            if (hit.HitCell)
            {
                ApplyBrush(hit.Column, hit.Cell);
            }

            Repaint();
        }

        private void DrawBrush()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Brush", GUILayout.Width(110f));
            brush = (Brush)EditorGUILayout.EnumPopup(brush, GUILayout.Width(90f));
            paintHidden = GUILayout.Toggle(paintHidden, "hidden ?", EditorStyles.miniButton, GUILayout.Width(70f));
            GUILayout.Label(
                brush == Brush.Paint
                    ? "Click a cell to paint it; clicking above the stack fills up to it."
                    : "Click a cell to remove it and everything above it.",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            paintColourId = ColourStrip("Paint colour", paintColourId, false);
        }

        private void DrawSelectedColumn()
        {
            SerializedProperty columns = Columns();

            EditorGUILayout.Space();

            if (selectedColumn < 0 || selectedColumn >= columns.arraySize)
            {
                EditorGUILayout.HelpBox("Click a column's header to edit its kind and capacity.", MessageType.None);
                return;
            }

            SerializedProperty column = columns.GetArrayElementAtIndex(selectedColumn);
            SerializedProperty kind = column.FindPropertyRelative(PathKind);

            EditorGUILayout.LabelField("Column " + selectedColumn, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(kind);
            EditorGUILayout.IntSlider(column.FindPropertyRelative(PathCapacity), ColumnData.MinCapacity, ColumnData.MaxCapacity);

            switch ((ColumnKind)kind.enumValueIndex)
            {
                case ColumnKind.Ice:
                    EditorGUILayout.PropertyField(column.FindPropertyRelative(PathThaw));
                    break;

                case ColumnKind.Covered:
                    SerializedProperty key = column.FindPropertyRelative(PathCoverKey);
                    key.intValue = ColourStrip("Cover key colour", key.intValue, true);
                    break;
            }

            EditorGUILayout.BeginHorizontal();

            movingSelected = GUILayout.Toggle(
                movingSelected, movingSelected ? "Click a cell…" : "Move to cell", EditorStyles.miniButton, GUILayout.Width(100f));

            if (GUILayout.Button("Clear cells", GUILayout.Width(100f)))
            {
                column.FindPropertyRelative(PathCells).arraySize = 0;
            }

            if (GUILayout.Button("Fill to capacity", GUILayout.Width(110f)))
            {
                FillColumn(column);
            }

            if (GUILayout.Button("Delete column", GUILayout.Width(110f)))
            {
                EnsurePlacementAuthored();
                serializedLevel.FindProperty(PathColumnCells).DeleteArrayElementAtIndex(selectedColumn);
                columns.DeleteArrayElementAtIndex(selectedColumn);
                selectedColumn = -1;
                movingSelected = false;
                EditorGUILayout.EndHorizontal();
                return;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// The shippability button, run on every repaint because editor-time work costs
        /// nothing in the cost model and a stale verdict is what lets a broken level ship.
        /// The message is the rules' own — this window never writes its own version of a
        /// rule <c>ColumnData</c> already owns.
        /// </summary>
        private bool DrawValidation()
        {
            string error;
            bool shippable;

            try
            {
                shippable = level.Validate(out error);
            }
            catch (Exception exception)
            {
                // Validate() converts the illegal-data exceptions it expects. Anything
                // else is a defect worth reading rather than an exception thrown once per
                // repaint into the console.
                shippable = false;
                error = exception.Message;
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                shippable ? "Shippable — every rule the game would run passes on this board." : error,
                shippable ? MessageType.Info : MessageType.Error);

            return shippable;
        }

        /// <summary>
        /// The second gate (D-010). `Validate()` proves a board is legal; a legal board can
        /// still be impossible, and only a search knows which. It runs on demand rather than
        /// per repaint — a window that stops to think on every keystroke is a window nobody
        /// uses — and the verdict is thrown away the moment the board changes.
        /// </summary>
        private void DrawSolvability(bool shippable)
        {
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(!shippable))
            {
                if (GUILayout.Button("Check solvability", GUILayout.Width(140f)))
                {
                    RunSolver();
                }
            }

            GUILayout.Label(
                "Searches the authored board with the '?' cells' real colours, so the answer is " +
                "\"solvable with perfect information\" (D-011). It can take a moment.",
                EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!hasSolverResult)
            {
                return;
            }

            MessageType tone;

            switch (solverResult.Verdict)
            {
                case SolverVerdict.Solvable:
                    tone = MessageType.Info;
                    break;

                case SolverVerdict.Unsolvable:
                    tone = MessageType.Error;
                    break;

                default:
                    tone = MessageType.Warning;
                    break;
            }

            EditorGUILayout.HelpBox(solverResult.Summary, tone);

            if (solverResult.Verdict == SolverVerdict.Solvable && solverResult.Solution.Count > 0)
            {
                EditorGUILayout.LabelField(Moves(solverResult.Solution), EditorStyles.miniLabel);
            }
        }

        private void RunSolver()
        {
            try
            {
                solverResult = BoardSolver.Solve(level.ToLevelData());
            }
            catch (Exception exception)
            {
                // A board that cannot even be built is not an unsolvable board; the validity
                // banner already says what is wrong with it.
                solverResult = new SolverResult(SolverVerdict.NotProven, 0, new SolverMove[0], exception.Message);
            }

            hasSolverResult = true;
        }

        /// <summary>
        /// The solution in one line, so the verdict can be checked by a person rather than
        /// believed. Long ones are cut off: the point is to be able to follow the first moves,
        /// not to read forty of them in a label.
        /// </summary>
        private static string Moves(IReadOnlyList<SolverMove> solution)
        {
            const int shown = 20;
            var line = new StringBuilder("Solution: ");

            for (int move = 0; move < solution.Count && move < shown; move++)
            {
                if (move > 0)
                {
                    line.Append(", ");
                }

                line.Append(solution[move].From).Append(" to ").Append(solution[move].To);
            }

            if (solution.Count > shown)
            {
                line.Append(", … (").Append(solution.Count - shown).Append(" more)");
            }

            return line.ToString();
        }

        /// <summary>
        /// Steps through the looks a player can actually meet. Every variant is an
        /// isomorphism of the authored board (D-014), so the verdict above covers all of
        /// them — this is here to be looked at, which is the whole reason the variant set
        /// is small and enumerable rather than one board per seed (D-015).
        /// </summary>
        private void DrawVariantPreview(bool shippable)
        {
            EditorGUILayout.Space();
            previewVariant = EditorGUILayout.ToggleLeft("Preview attempt variants", previewVariant);

            if (!previewVariant)
            {
                return;
            }

            if (!shippable)
            {
                EditorGUILayout.HelpBox("A variant is built from the authored board, so the board has to be valid first.", MessageType.Warning);
                return;
            }

            variantIndex = Mathf.Max(0, EditorGUILayout.IntField("Variant index", variantIndex));
            EditorGUILayout.LabelField(
                "How many variants ship is Meta's tuning number in Data/Config/ (D-015); this stepper is the tool's own.",
                EditorStyles.miniLabel);

            try
            {
                LevelData authored = level.ToLevelData();
                LevelData variant = AttemptScramble.ForVariant(authored, variantIndex).Apply(authored);

                var board = new List<ColumnSnapshot>(variant.Columns.Count);

                for (int column = 0; column < variant.Columns.Count; column++)
                {
                    board.Add(ColumnSnapshot.FromColumnData(variant.Columns[column]));
                }

                int rows = Mathf.Max(1, serializedLevel.FindProperty(PathLayoutRows).intValue);
                int perRow = Mathf.Max(1, serializedLevel.FindProperty(PathLayoutColumns).intValue);

                // Rows are drawn centred here, the way BoardLayout lays the real board out
                // (D-034) — the preview is the view that has to match the screen.
                Vector2 size = LevelBoardDrawer.Size(board, rows, perRow);
                Rect area = GUILayoutUtility.GetRect(size.x, size.y);
                LevelBoardDrawer.Draw(area, board, Placement(), rows, perRow, skins, -1, false, true);
            }
            catch (Exception exception)
            {
                // The scrambled level goes back through the LevelData constructor, so a
                // throw here means a variant broke an invariant the authored board keeps.
                // That is worth seeing loudly.
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }
        }

        /// <summary>
        /// Paints or erases one cell. Cells are stored bottom-up and contiguously — a
        /// column holds its blocks from the floor with no gaps — so painting above the
        /// stack fills up to the clicked cell and erasing takes everything above it.
        /// </summary>
        private void ApplyBrush(int columnIndex, int cell)
        {
            SerializedProperty column = Columns().GetArrayElementAtIndex(columnIndex);
            SerializedProperty cells = column.FindPropertyRelative(PathCells);

            if (brush == Brush.Erase)
            {
                cells.arraySize = Mathf.Min(cell, cells.arraySize);
                return;
            }

            if (cell >= column.FindPropertyRelative(PathCapacity).intValue)
            {
                return;
            }

            int first = cells.arraySize;

            if (cell < cells.arraySize)
            {
                WriteCell(cells.GetArrayElementAtIndex(cell));
                return;
            }

            cells.arraySize = cell + 1;

            for (int index = first; index <= cell; index++)
            {
                WriteCell(cells.GetArrayElementAtIndex(index));
            }
        }

        private void FillColumn(SerializedProperty column)
        {
            SerializedProperty cells = column.FindPropertyRelative(PathCells);
            int capacity = column.FindPropertyRelative(PathCapacity).intValue;
            int first = cells.arraySize;
            cells.arraySize = capacity;

            for (int index = first; index < capacity; index++)
            {
                WriteCell(cells.GetArrayElementAtIndex(index));
            }
        }

        private void WriteCell(SerializedProperty cell)
        {
            cell.FindPropertyRelative(PathColourId).intValue = paintColourId;
            cell.FindPropertyRelative(PathHidden).boolValue = paintHidden;
        }

        /// <summary>
        /// Adds a column and puts it in a grid cell in the same breath. The two arrays are
        /// grown together on purpose: a column with no cell, or a cell with no column, is a
        /// level that fails validation for a reason the author never typed.
        /// </summary>
        private void AddColumn(int gridCell)
        {
            if (gridCell < 0)
            {
                return;
            }

            EnsurePlacementAuthored();

            SerializedProperty columns = Columns();
            int index = columns.arraySize;
            columns.arraySize = index + 1;

            SerializedProperty cells = serializedLevel.FindProperty(PathColumnCells);
            cells.arraySize = columns.arraySize;
            cells.GetArrayElementAtIndex(index).intValue = gridCell;

            // Growing the array copies the previous element, so every field is written.
            SerializedProperty column = columns.GetArrayElementAtIndex(index);
            column.FindPropertyRelative(PathKind).enumValueIndex = (int)newColumnKind;
            column.FindPropertyRelative(PathCapacity).intValue = newColumnCapacity;
            column.FindPropertyRelative(PathCells).arraySize = 0;
            column.FindPropertyRelative(PathCoverKey).intValue = 0;

            // Not a designer number: an Ice column that thaws after zero completions is
            // refused by ColumnData, so 1 is the rules' floor rather than a chosen value.
            column.FindPropertyRelative(PathThaw).intValue = newColumnKind == ColumnKind.Ice ? 1 : 0;

            selectedColumn = index;
        }

        /// <summary>
        /// The board as the drawer wants it. Rebuilt per repaint: editor frequency is ×0
        /// in the cost model, and a cache would be one more thing to keep in step with an
        /// asset the Inspector can also edit.
        /// </summary>
        private List<ColumnSnapshot> AuthoredBoard()
        {
            SerializedProperty columns = Columns();
            var board = new List<ColumnSnapshot>(columns.arraySize);

            for (int index = 0; index < columns.arraySize; index++)
            {
                SerializedProperty column = columns.GetArrayElementAtIndex(index);
                SerializedProperty cells = column.FindPropertyRelative(PathCells);
                var authored = new CellDefinition[cells.arraySize];

                for (int cell = 0; cell < authored.Length; cell++)
                {
                    SerializedProperty entry = cells.GetArrayElementAtIndex(cell);
                    authored[cell].colourId = entry.FindPropertyRelative(PathColourId).intValue;
                    authored[cell].hidden = entry.FindPropertyRelative(PathHidden).boolValue;
                }

                board.Add(new ColumnSnapshot(
                    (ColumnKind)column.FindPropertyRelative(PathKind).enumValueIndex,
                    column.FindPropertyRelative(PathCapacity).intValue,
                    authored,
                    column.FindPropertyRelative(PathThaw).intValue,
                    column.FindPropertyRelative(PathCoverKey).intValue));
            }

            return board;
        }

        /// <summary>
        /// Where each column stands, in slot order. A placement that disagrees with the
        /// column count is drawn as the plain fill — the warning above says so, and a level
        /// half typed in should still be visible (D-033).
        /// </summary>
        private int[] Placement()
        {
            SerializedProperty columns = Columns();
            SerializedProperty cells = serializedLevel.FindProperty(PathColumnCells);
            var placement = new int[columns.arraySize];
            bool authored = cells.arraySize == columns.arraySize;

            for (int slot = 0; slot < placement.Length; slot++)
            {
                placement[slot] = authored ? cells.GetArrayElementAtIndex(slot).intValue : slot;
            }

            return placement;
        }

        /// <summary>
        /// Writes down what an empty placement already meant — the plain fill — so that the
        /// placement and the columns can be kept parallel from here on. Called before every
        /// structural edit, when the two still agree, so it is a no-op on a level that has a
        /// placement already.
        /// </summary>
        private void EnsurePlacementAuthored()
        {
            SerializedProperty columns = Columns();
            SerializedProperty cells = serializedLevel.FindProperty(PathColumnCells);

            if (cells.arraySize == columns.arraySize)
            {
                return;
            }

            cells.arraySize = columns.arraySize;

            for (int slot = 0; slot < columns.arraySize; slot++)
            {
                cells.GetArrayElementAtIndex(slot).intValue = slot;
            }
        }

        /// <summary>The first grid cell no column stands in, or -1 when the grid is full.</summary>
        private int FirstFreeCell()
        {
            int[] placement = Placement();
            int rows = Mathf.Max(1, serializedLevel.FindProperty(PathLayoutRows).intValue);
            int perRow = Mathf.Max(1, serializedLevel.FindProperty(PathLayoutColumns).intValue);

            for (int cell = 0; cell < rows * perRow; cell++)
            {
                if (BoardLayout.SlotInCell(placement, cell) == BoardLayout.NoSlot)
                {
                    return cell;
                }
            }

            return -1;
        }

        /// <summary>
        /// Moves a column to a grid cell, swapping with whoever already stands there. A swap
        /// rather than a landing: two columns in one cell is a placement `Validate()` refuses
        /// for a reason the author never typed.
        /// </summary>
        private void MoveColumn(int slot, int gridCell)
        {
            EnsurePlacementAuthored();

            SerializedProperty cells = serializedLevel.FindProperty(PathColumnCells);
            int occupant = BoardLayout.SlotInCell(Placement(), gridCell);

            if (occupant >= 0 && occupant != slot)
            {
                cells.GetArrayElementAtIndex(occupant).intValue = cells.GetArrayElementAtIndex(slot).intValue;
            }

            cells.GetArrayElementAtIndex(slot).intValue = gridCell;
        }

        /// <summary>
        /// A row of the project's real colours. Used twice — the paint colour and a
        /// cover's key — which is what earns it a method.
        /// </summary>
        private int ColourStrip(string label, int current, bool allowNone)
        {
            int picked = current;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(110f));

            if (allowNone && SwatchButton("none", Color.grey, current == 0))
            {
                picked = 0;
            }

            for (int id = BlockColourId.MinId; id <= BlockColourId.MaxId; id++)
            {
                if (!HasColour(id))
                {
                    continue;
                }

                if (SwatchButton(LevelBoardDrawer.SymbolOf(skins, id), LevelBoardDrawer.SwatchOf(skins, id), current == id))
                {
                    picked = id;
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            return picked;
        }

        private bool SwatchButton(string label, Color colour, bool selected)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = colour;
            bool clicked = GUILayout.Button(selected ? "▸ " + label : label, GUILayout.Width(64f), GUILayout.Height(22f));
            GUI.backgroundColor = previous;
            return clicked;
        }

        /// <summary>
        /// Whether the project's palette carries this id. With no skin set at all every id
        /// is offered — a level is ids and nothing else (D-003), so it can still be
        /// authored; the swatches simply draw magenta until the skins exist.
        /// </summary>
        private bool HasColour(int id)
        {
            if (skins == null)
            {
                return true;
            }

            BlockSkin skin;
            return skins.TryGetSkin(new BlockColourId(id), out skin);
        }

        private SerializedProperty Columns()
        {
            return serializedLevel.FindProperty(PathColumns);
        }

        /// <summary>
        /// Starts a level on the next free plaque number.
        /// <para>
        /// It builds the level by decoding an empty row rather than by reaching for a constructor,
        /// and that is on purpose: the codec is the only way a level comes into existence, so the
        /// shape this window makes and the shape the game reads cannot be two different things
        /// (D-085). It is also why `Content` needs no public setter for the editor's sake.
        /// </para>
        /// </summary>
        private void CreateLevel()
        {
            int plaque = LevelDefinition.FirstLevelIndex;

            for (int ordinal = 0; ordinal < loaded.Count; ordinal++)
            {
                plaque = Mathf.Max(plaque, loaded[ordinal].LevelIndex + 1);
            }

            // A grid with room in it. The board is drawn as layoutRows x layoutColumns and a column is
            // added by CLICKING one of those cells, so a 1x1 blank offered exactly one cell and no
            // second column could ever be placed — it looked like a working level and was a dead end.
            // 2x6 is the reference's own shape (Level 79), so a new level starts looking like the
            // levels this game has rather than like a number picked for the occasion; the two fields
            // sit right above the board, so widening it further stays the author's call (D-087).
            var blank = new LevelCodec.LevelRow
            {
                i = plaque,
                d = 0,
                r = BlankRows,
                c = BlankColumns,
                p = new int[0],
                k = new string[0],
            };

            if (!LevelCodec.TryDecode(blank, out LevelDefinition created, out string error))
            {
                Debug.LogError("[Colorful Sort] a blank level could not be made: " + error);
                return;
            }

            loaded.Add(created);
            Select(loaded.Count - 1);
        }

        private void Bind()
        {
            serializedLevel = level == null ? null : new SerializedObject(level);
            movingSelected = false;
            hasSolverResult = false;
            pathProblem = serializedLevel == null ? null : CheckPaths();
        }

        /// <summary>
        /// D-032's price, paid once per bound level: a renamed serialized field would
        /// otherwise turn every edit in this window into a silent no-op.
        /// </summary>
        private string CheckPaths()
        {
            string[] levelPaths = { PathLevelIndex, PathDifficulty, PathLayoutRows, PathLayoutColumns, PathColumns, PathColumnCells };

            for (int index = 0; index < levelPaths.Length; index++)
            {
                if (serializedLevel.FindProperty(levelPaths[index]) == null)
                {
                    return Missing(nameof(LevelDefinition), levelPaths[index]);
                }
            }

            SerializedProperty columns = serializedLevel.FindProperty(PathColumns);

            if (columns.arraySize == 0)
            {
                return null;
            }

            SerializedProperty column = columns.GetArrayElementAtIndex(0);
            string[] columnPaths = { PathKind, PathCapacity, PathCells, PathThaw, PathCoverKey };

            for (int index = 0; index < columnPaths.Length; index++)
            {
                if (column.FindPropertyRelative(columnPaths[index]) == null)
                {
                    return Missing(nameof(ColumnDefinition), columnPaths[index]);
                }
            }

            SerializedProperty cells = column.FindPropertyRelative(PathCells);

            if (cells.arraySize == 0)
            {
                return null;
            }

            SerializedProperty cell = cells.GetArrayElementAtIndex(0);
            string[] cellPaths = { PathColourId, PathHidden };

            for (int index = 0; index < cellPaths.Length; index++)
            {
                if (cell.FindPropertyRelative(cellPaths[index]) == null)
                {
                    return Missing(nameof(CellDefinition), cellPaths[index]);
                }
            }

            return null;
        }

        private static string Missing(string type, string path)
        {
            return "This window writes " + type + "." + path + " by name and that field no longer exists. " +
                   "Rename it here too (D-032) — until then nothing typed in this window would be saved.";
        }

        /// <summary>
        /// Finds the project's single skin set. More than one is not a preference, it is
        /// the single-authority invariant broken (D-003), so the window says so rather
        /// than picking one.
        /// </summary>
        private void FindSkinSet()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(BlockSkinSet));

            if (guids.Length == 0)
            {
                skins = null;
                skinSetProblem = "No " + nameof(BlockSkinSet) + " in the project, so the palette has no colours to show. " +
                                 "Run Tools > Colorful Sort > Create Block Skins. Levels store ids, so authoring still works.";
                return;
            }

            skins = AssetDatabase.LoadAssetAtPath<BlockSkinSet>(AssetDatabase.GUIDToAssetPath(guids[0]));
            skinSetProblem = guids.Length == 1
                ? null
                : guids.Length + " " + nameof(BlockSkinSet) + " assets exist. Exactly one decides what a colour looks like (D-003); " +
                  "this window is showing " + AssetDatabase.GUIDToAssetPath(guids[0]) + ".";
        }
    }
}
#endif
