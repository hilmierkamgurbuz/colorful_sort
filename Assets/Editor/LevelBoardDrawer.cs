#if UNITY_EDITOR
using System.Collections.Generic;
using ColorfulSort.Board;
using ColorfulSort.Content;
using ColorfulSort.View;
using UnityEditor;
using UnityEngine;

namespace ColorfulSort.EditorTools
{
    /// <summary>
    /// What a click on a drawn board landed on: an empty grid cell, a column's header, one
    /// of its cells, or nothing at all.
    /// </summary>
    public readonly struct BoardHit
    {
        public static readonly BoardHit None = new BoardHit(-1, -1, -1);

        /// <summary>The grid cell clicked, as row × gridColumns + column, or -1.</summary>
        public readonly int GridCell;

        /// <summary>The slot standing in that grid cell, or -1 when the cell is empty.</summary>
        public readonly int Column;

        /// <summary>Cell index bottom-up inside the column, or -1 when its header was hit.</summary>
        public readonly int Cell;

        public BoardHit(int gridCell, int column, int cell)
        {
            GridCell = gridCell;
            Column = column;
            Cell = cell;
        }

        public bool HitGrid => GridCell >= 0;

        public bool HitColumn => Column >= 0;

        public bool HitCell => Column >= 0 && Cell >= 0;
    }

    /// <summary>
    /// One column, flattened into what drawing actually needs. It exists because the
    /// board is drawn twice from two different sources — the authored columns being
    /// edited (read through <c>SerializedProperty</c>) and the read-only
    /// <see cref="LevelData"/> a variant produces — and neither one should have to know
    /// how the other is stored.
    /// <para>
    /// The cells are <see cref="CellDefinition"/> rather than a type of its own: an
    /// authored cell is already exactly "a colour id and whether it is hidden", and a
    /// parallel struct would be a second spelling of the same two fields.
    /// </para>
    /// </summary>
    public readonly struct ColumnSnapshot
    {
        public readonly ColumnKind Kind;

        public readonly int Capacity;

        /// <summary>Contents bottom-up: index 0 is the bottom cell.</summary>
        public readonly IReadOnlyList<CellDefinition> Cells;

        public readonly int ThawAfterCompletions;

        /// <summary>Covered only: the key colour id that opens this cover, 0 for none.</summary>
        public readonly int CoverKeyColourId;

        public ColumnSnapshot(
            ColumnKind kind,
            int capacity,
            IReadOnlyList<CellDefinition> cells,
            int thawAfterCompletions,
            int coverKeyColourId)
        {
            Kind = kind;
            Capacity = capacity;
            Cells = cells;
            ThawAfterCompletions = thawAfterCompletions;
            CoverKeyColourId = coverKeyColourId;
        }

        public int BlockCount => Cells == null ? 0 : Cells.Count;

        /// <summary>
        /// The rules' own column, as the drawer sees it — this is the path the variant
        /// preview takes, so what it shows is the board an attempt would really play.
        /// </summary>
        public static ColumnSnapshot FromColumnData(ColumnData column)
        {
            var cells = new CellDefinition[column.Cells.Count];

            for (int cell = 0; cell < cells.Length; cell++)
            {
                cells[cell].colourId = column.Cells[cell].Colour.Value;
                cells[cell].hidden = column.Cells[cell].Hidden;
            }

            return new ColumnSnapshot(
                column.Kind,
                column.Capacity,
                cells,
                column.ThawAfterCompletions,
                column.CoverKeyColour.IsNone ? 0 : column.CoverKeyColour.Value);
        }
    }

    /// <summary>
    /// Draws a board as a grid of columns and reports what was clicked. Two consumers:
    /// the editable authored board and the read-only variant preview, which is the only
    /// reason this is not simply part of the window.
    /// <para>
    /// It holds no colour of its own. Every swatch is read from the project's single
    /// <see cref="BlockSkinSet"/> (D-003, D-020) — a colour table typed into an editor
    /// tool would be a second place deciding what a colour looks like, in the one place
    /// nobody would think to look during a re-skin.
    /// </para>
    /// </summary>
    public static class LevelBoardDrawer
    {
        public const float CellWidth = 44f;
        public const float CellHeight = 28f;
        public const float HeaderHeight = 18f;
        public const float ColumnGap = 8f;
        public const float RowGap = 18f;

        private static readonly Color EmptyCell = new Color(0.16f, 0.15f, 0.20f);
        private static readonly Color EmptyGridCell = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color CellBorder = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color HiddenVeil = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color IceTint = new Color(0.55f, 0.85f, 1f, 0.22f);
        private static readonly Color CoverTint = new Color(0.92f, 0.83f, 0.62f, 0.30f);
        private static readonly Color MysteryTint = new Color(0.35f, 0.20f, 0.55f, 0.22f);
        private static readonly Color Selection = new Color(1f, 0.85f, 0.25f);

        /// <summary>A colour id with no row in the skin set: loud on purpose.</summary>
        private static readonly Color MissingColour = new Color(1f, 0f, 1f);

        private static GUIStyle cellLabel;
        private static GUIStyle headerLabel;

        /// <summary>
        /// How much room <see cref="Draw"/> needs — the whole declared grid, empty cells
        /// included, because an empty cell is a place you can put a column. Asked for before
        /// the rect is reserved, because IMGUI wants the size before it knows the content.
        /// </summary>
        public static Vector2 Size(IReadOnlyList<ColumnSnapshot> columns, int rows, int gridColumns)
        {
            int perRow = Mathf.Max(1, gridColumns);
            int rowCount = Mathf.Max(1, rows);
            int tallest = TallestColumn(columns);

            return new Vector2(
                perRow * (CellWidth + ColumnGap),
                rowCount * (HeaderHeight + tallest * CellHeight + RowGap));
        }

        /// <summary>
        /// Draws the layout grid: every cell of it, with a column in the cells a level placed
        /// one in and a hollow slot everywhere else. Returns what the mouse landed on — which
        /// includes an empty cell, because "put a column here" is a click like any other.
        /// <para>
        /// <paramref name="centreRows"/> switches between the two honest views of the same
        /// level. Off, cells are drawn where the grid says, which is what makes placing them
        /// possible. On, each row is centred on its own span the way the game lays it out
        /// (D-034), which is what the preview has to show or the window would be lying.
        /// </para>
        /// </summary>
        public static BoardHit Draw(
            Rect area,
            IReadOnlyList<ColumnSnapshot> columns,
            IReadOnlyList<int> cells,
            int rows,
            int gridColumns,
            BlockSkinSet skins,
            int selectedColumn,
            bool interactive,
            bool centreRows)
        {
            BoardHit hit = BoardHit.None;

            if (columns == null || cells == null)
            {
                return hit;
            }

            EnsureStyles();

            int perRow = Mathf.Max(1, gridColumns);
            int rowCount = Mathf.Max(1, rows);
            float bodyHeight = TallestColumn(columns) * CellHeight;

            for (int row = 0; row < rowCount; row++)
            {
                float offset = centreRows ? RowOffset(cells, perRow, row) : 0f;

                for (int column = 0; column < perRow; column++)
                {
                    int gridCell = row * perRow + column;
                    int slot = BoardLayout.SlotInCell(cells, gridCell);

                    if (slot >= columns.Count)
                    {
                        slot = BoardLayout.NoSlot;
                    }

                    if (centreRows && slot == BoardLayout.NoSlot)
                    {
                        continue;
                    }

                    float x = area.x + (column + offset) * (CellWidth + ColumnGap);
                    float y = area.y + row * (HeaderHeight + bodyHeight + RowGap);

                    var header = new Rect(x, y, CellWidth, HeaderHeight);
                    var body = new Rect(x, y + HeaderHeight, CellWidth, bodyHeight);

                    if (slot == BoardLayout.NoSlot)
                    {
                        DrawEmptyCell(body);

                        if (interactive && Clicked(body))
                        {
                            hit = new BoardHit(gridCell, BoardLayout.NoSlot, -1);
                        }

                        continue;
                    }

                    DrawColumn(header, body, columns[slot], slot, skins, slot == selectedColumn);

                    if (!interactive)
                    {
                        continue;
                    }

                    int cell = ClickedCell(body, columns[slot].Capacity);

                    if (cell >= 0)
                    {
                        hit = new BoardHit(gridCell, slot, cell);
                    }
                    else if (Clicked(header))
                    {
                        hit = new BoardHit(gridCell, slot, -1);
                    }
                }
            }

            return hit;
        }

        /// <summary>
        /// How far a row slides to sit centred on the grid — the editor's echo of the rule
        /// <c>BoardLayout</c> lays the real board out with (D-034). Half-cell offsets are
        /// expected: a three-wide row over a four-wide one lands between two grid columns.
        /// </summary>
        private static float RowOffset(IReadOnlyList<int> cells, int gridColumns, int row)
        {
            int first = int.MaxValue;
            int last = int.MinValue;

            for (int slot = 0; slot < cells.Count; slot++)
            {
                if (cells[slot] / gridColumns != row)
                {
                    continue;
                }

                int column = cells[slot] % gridColumns;
                first = Mathf.Min(first, column);
                last = Mathf.Max(last, column);
            }

            if (first > last)
            {
                return 0f;
            }

            return (gridColumns - 1) * 0.5f - (first + last) * 0.5f;
        }

        /// <summary>A cell nothing stands in: visibly a place, not a column.</summary>
        private static void DrawEmptyCell(Rect body)
        {
            var slot = new Rect(body.x, body.yMax - CellHeight, body.width, CellHeight);
            EditorGUI.DrawRect(slot, EmptyGridCell);
            DrawOutline(slot, CellBorder);
            DrawCentred(slot, "+", new Color(1f, 1f, 1f, 0.35f));
        }

        /// <summary>
        /// The colour a logical id wears, straight from the skin set. An id with no row
        /// draws magenta rather than something plausible — a level that ships with an
        /// unmapped colour would spawn invisible bricks (D-021).
        /// </summary>
        public static Color SwatchOf(BlockSkinSet skins, int colourId)
        {
            BlockSkin skin;

            if (skins != null && InRange(colourId) && skins.TryGetSkin(new BlockColourId(colourId), out skin))
            {
                return skin.UiColour;
            }

            return MissingColour;
        }

        /// <summary>
        /// The symbol a logical id wears, as the skin asset's own name (<c>Skin_Cat</c> →
        /// <c>Cat</c>). Shown so a transcriber can read the board the way the player will;
        /// the level still stores nothing but the id (D-003).
        /// </summary>
        public static string SymbolOf(BlockSkinSet skins, int colourId)
        {
            BlockSkin skin;

            if (skins == null || !InRange(colourId) || !skins.TryGetSkin(new BlockColourId(colourId), out skin))
            {
                return colourId.ToString();
            }

            const string prefix = "Skin_";
            return skin.name.StartsWith(prefix) ? skin.name.Substring(prefix.Length) : skin.name;
        }

        private static bool InRange(int colourId)
        {
            return colourId >= BlockColourId.MinId && colourId <= BlockColourId.MaxId;
        }

        private static int TallestColumn(IReadOnlyList<ColumnSnapshot> columns)
        {
            int tallest = 1;

            if (columns == null)
            {
                return tallest;
            }

            for (int index = 0; index < columns.Count; index++)
            {
                tallest = Mathf.Max(tallest, columns[index].Capacity);
            }

            return tallest;
        }

        private static void DrawColumn(
            Rect header,
            Rect body,
            ColumnSnapshot column,
            int index,
            BlockSkinSet skins,
            bool selected)
        {
            EnsureStyles();

            // Columns stand on the same floor whatever their capacity, exactly as they do
            // on screen, so a short column reads as short instead of as floating.
            var stack = new Rect(body.x, body.yMax - column.Capacity * CellHeight, body.width, column.Capacity * CellHeight);

            GUI.Label(header, index + " · " + KindLabel(column), headerLabel);

            for (int cell = 0; cell < column.Capacity; cell++)
            {
                Rect rect = CellRect(stack, cell);
                EditorGUI.DrawRect(rect, EmptyCell);

                if (cell < column.BlockCount)
                {
                    CellDefinition authored = column.Cells[cell];
                    Color swatch = SwatchOf(skins, authored.colourId);
                    EditorGUI.DrawRect(Inset(rect, 1f), swatch);

                    if (authored.hidden)
                    {
                        // The author sees what is under the '?'; the player does not. The
                        // colour is real data either way (D-011).
                        EditorGUI.DrawRect(Inset(rect, 1f), HiddenVeil);
                        DrawCentred(rect, "?", Color.white);
                    }
                    else
                    {
                        DrawCentred(rect, SymbolOf(skins, authored.colourId), Readable(swatch));
                    }
                }

                DrawOutline(rect, CellBorder);
            }

            Color tint = KindTint(column.Kind);

            if (tint.a > 0f)
            {
                EditorGUI.DrawRect(stack, tint);
            }

            if (column.Kind == ColumnKind.Covered && column.CoverKeyColourId != 0)
            {
                // The cover's key, drawn where the reference paints it: a stripe up the
                // left edge in the colour that opens it (reference §2).
                EditorGUI.DrawRect(new Rect(stack.x, stack.y, 4f, stack.height), SwatchOf(skins, column.CoverKeyColourId));
            }

            if (selected)
            {
                DrawOutline(new Rect(header.x, header.y, header.width, header.height + body.height), Selection);
            }
        }

        private static Rect CellRect(Rect stack, int cell)
        {
            return new Rect(stack.x, stack.yMax - (cell + 1) * CellHeight, stack.width, CellHeight);
        }

        private static int ClickedCell(Rect body, int capacity)
        {
            var stack = new Rect(body.x, body.yMax - capacity * CellHeight, body.width, capacity * CellHeight);

            for (int cell = 0; cell < capacity; cell++)
            {
                if (Clicked(CellRect(stack, cell)))
                {
                    return cell;
                }
            }

            return -1;
        }

        private static bool Clicked(Rect rect)
        {
            Event current = Event.current;

            if (current.type != EventType.MouseDown || current.button != 0 || !rect.Contains(current.mousePosition))
            {
                return false;
            }

            current.Use();
            return true;
        }

        private static string KindLabel(ColumnSnapshot column)
        {
            switch (column.Kind)
            {
                case ColumnKind.Ice:
                    return "Ice " + column.ThawAfterCompletions;

                case ColumnKind.Covered:
                    return "Cover";

                case ColumnKind.Mystery:
                    return "?";

                default:
                    return "-";
            }
        }

        private static Color KindTint(ColumnKind kind)
        {
            switch (kind)
            {
                case ColumnKind.Ice:
                    return IceTint;

                case ColumnKind.Covered:
                    return CoverTint;

                case ColumnKind.Mystery:
                    return MysteryTint;

                default:
                    return Color.clear;
            }
        }

        /// <summary>Black on a pale brick, white on a dark one, so every label stays readable.</summary>
        private static Color Readable(Color background)
        {
            float luminance = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
            return luminance > 0.55f ? Color.black : Color.white;
        }

        // The style's own text colour is set rather than GUI.color, which would only
        // tint the editor skin's grey and never reach white on a dark brick.
        private static void DrawCentred(Rect rect, string text, Color colour)
        {
            cellLabel.normal.textColor = colour;
            GUI.Label(rect, text, cellLabel);
        }

        private static void DrawOutline(Rect rect, Color colour)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), colour);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), colour);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), colour);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), colour);
        }

        private static Rect Inset(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount, rect.width - 2f * amount, rect.height - 2f * amount);
        }

        // GUIStyles cannot be built outside a GUI call, so they are made on first draw.
        private static void EnsureStyles()
        {
            if (cellLabel != null)
            {
                return;
            }

            cellLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            headerLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
#endif
