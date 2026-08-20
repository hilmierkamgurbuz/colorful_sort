#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

namespace ColorfulSort.Board
{
    /// <summary>What a search concluded about a board.</summary>
    public enum SolverVerdict
    {
        /// <summary>A sequence of legal moves reaches the win.</summary>
        Solvable,

        /// <summary>Every board reachable from this one was explored and none of them wins.</summary>
        Unsolvable,

        /// <summary>The search stopped early. This is ignorance, not a verdict.</summary>
        NotProven,
    }

    /// <summary>One move of a solution: lift from a column, drop on another.</summary>
    public readonly struct SolverMove
    {
        public readonly int From;

        public readonly int To;

        public SolverMove(int from, int to)
        {
            From = from;
            To = to;
        }

        public override string ToString()
        {
            return From + "->" + To;
        }
    }

    /// <summary>A verdict, what it cost, and — when there is one — the solution itself.</summary>
    public readonly struct SolverResult
    {
        public readonly SolverVerdict Verdict;

        /// <summary>How many distinct boards the search expanded.</summary>
        public readonly int StatesExplored;

        /// <summary>The winning move sequence, empty unless <see cref="Verdict"/> is Solvable.</summary>
        public readonly IReadOnlyList<SolverMove> Solution;

        /// <summary>The verdict in words, for whoever has to act on it.</summary>
        public readonly string Summary;

        public SolverResult(SolverVerdict verdict, int statesExplored, IReadOnlyList<SolverMove> solution, string summary)
        {
            Verdict = verdict;
            StatesExplored = statesExplored;
            Solution = solution;
            Summary = summary;
        }
    }

    /// <summary>
    /// Answers the question <c>LevelDefinition.Validate()</c> cannot: a board can be perfectly
    /// legal and still impossible. This is the level editor's second gate (D-010), and it is
    /// editor-only — the <c>#if</c> keeps it out of every build while leaving it where its
    /// tests can reach it (D-035).
    /// <para>
    /// It re-implements no rule. It drives a real <see cref="BoardSession"/> with
    /// <c>TryMove</c> and <c>Undo</c>, so the verdict is about the game rather than about a
    /// second rulebook that agreed with it on the day it was written.
    /// </para>
    /// <para>
    /// Hidden cells are searched with the colours the level authored, so the verdict reads
    /// "solvable with perfect information". That is the right gate for shipping a board — a
    /// level nobody can finish even *knowing* the answer is broken — and it is deliberately
    /// not a claim about what a player can deduce.
    /// </para>
    /// </summary>
    public static class BoardSolver
    {
        /// <summary>How many distinct boards a search may expand before it gives up.</summary>
        public const int DefaultStateBudget = 200000;

        /// <summary>
        /// The deepest a search may go, which is exactly how far <see cref="BoardSession.Undo"/>
        /// can take it back: the history drops its oldest entry past
        /// <see cref="BoardMoveHistory.MaxEntries"/>, so one move deeper and the search could no
        /// longer restore the board it explored from. A solution longer than this is reported as
        /// not proven, never as impossible. It also bounds the recursion.
        /// </summary>
        public const int MaxDepth = BoardMoveHistory.MaxEntries;

        private static readonly SolverMove[] NoMoves = new SolverMove[0];

        /// <summary>
        /// Searches the authored board for a win. Depth-first with a visited set, because the
        /// question is only whether a solution exists — the shortest one is a different, much
        /// more expensive question that nobody has asked.
        /// </summary>
        public static SolverResult Solve(LevelData level, int stateBudget = DefaultStateBudget)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (stateBudget < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(stateBudget), stateBudget, "A search explores at least one board.");
            }

            // Its own session, never a live one: a search must not be able to move a board
            // somebody is looking at. The seed is irrelevant because no move draws from the
            // RNG — only the Shuffle booster does (D-002) — and the authored board is what a
            // verdict is about, since every variant inherits it (D-014).
            var search = new Search(new BoardSession(level, 0, AttemptScramble.None), stateBudget);

            if (search.Session.IsWon)
            {
                return new SolverResult(SolverVerdict.Solvable, 0, NoMoves, "The board is already solved.");
            }

            search.Seen.Add(StateKey(search));

            if (Explore(search, 0))
            {
                var solution = search.Path.ToArray();
                return new SolverResult(
                    SolverVerdict.Solvable,
                    search.StatesExplored,
                    solution,
                    "Solvable in " + solution.Length + " move(s), found after expanding " + search.StatesExplored + " board(s).");
            }

            if (search.StoppedEarly)
            {
                return new SolverResult(
                    SolverVerdict.NotProven,
                    search.StatesExplored,
                    NoMoves,
                    "Not proven: the search stopped at its limit after " + search.StatesExplored +
                    " board(s) (budget " + stateBudget + ", depth " + MaxDepth + "). This is not a verdict — " +
                    "raise the budget, or simplify the board.");
            }

            return new SolverResult(
                SolverVerdict.Unsolvable,
                search.StatesExplored,
                NoMoves,
                "Unsolvable: every one of the " + search.StatesExplored + " board(s) reachable from this one was explored and none of them wins.");
        }

        /// <summary>
        /// One node of the search: try every legal move, and recurse into the boards nobody has
        /// stood on before. The move is applied through the session and undone on the way out,
        /// so the board the caller handed in is the board it gets back.
        /// </summary>
        private static bool Explore(Search search, int depth)
        {
            BoardState state = search.Session.State;
            int columns = state.ColumnCount;

            for (int from = 0; from < columns; from++)
            {
                for (int to = 0; to < columns; to++)
                {
                    if (to == from || !search.Session.CanMove(from, to))
                    {
                        continue;
                    }

                    if (IsRedundantEmptyTarget(state, from, to))
                    {
                        continue;
                    }

                    if (!search.Session.TryMove(from, to))
                    {
                        // CanMove said yes, so this cannot happen; if it ever does, the two
                        // disagree and that is a rules bug worth crashing on rather than skipping.
                        throw new InvalidOperationException(
                            "CanMove(" + from + "," + to + ") allowed a move that TryMove refused.");
                    }

                    search.Path.Add(new SolverMove(from, to));

                    if (search.Session.IsWon)
                    {
                        return true;
                    }

                    if (Continue(search, depth))
                    {
                        search.StatesExplored++;

                        if (Explore(search, depth + 1))
                        {
                            return true;
                        }
                    }

                    search.Path.RemoveAt(search.Path.Count - 1);
                    search.Session.Undo();
                }
            }

            return false;
        }

        /// <summary>
        /// Whether the board just reached is worth expanding: fresh, within the depth the undo
        /// history can walk back, and inside the budget. The two limits are recorded as
        /// "stopped early", because a search that ran out of room has not proved anything.
        /// </summary>
        private static bool Continue(Search search, int depth)
        {
            if (!search.Seen.Add(StateKey(search)))
            {
                return false;
            }

            if (depth + 1 >= MaxDepth)
            {
                search.StoppedEarly = true;
                return false;
            }

            if (search.StatesExplored >= search.StateBudget)
            {
                search.StoppedEarly = true;
                return false;
            }

            return true;
        }

        /// <summary>
        /// The only reduction beyond legality, and it is a symmetry rather than a heuristic:
        /// two empty columns of the same kind and capacity are interchangeable, so the boards
        /// that result from moving into either differ by a relabelling and cannot differ in
        /// whether they can be solved. Everything else is left to the search — a prune that is
        /// merely probably safe would let this tool call a good level impossible.
        /// </summary>
        private static bool IsRedundantEmptyTarget(BoardState state, int from, int to)
        {
            BoardColumn target = state[to];

            if (!target.IsEmpty)
            {
                return false;
            }

            for (int other = 0; other < to; other++)
            {
                if (other == from)
                {
                    continue;
                }

                BoardColumn candidate = state[other];

                if (candidate.IsEmpty &&
                    !candidate.IsLocked &&
                    candidate.Kind == target.Kind &&
                    candidate.Capacity == target.Capacity)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// An exact key, not a hash. A collision would mark a board nobody has explored as
        /// seen, which is precisely how a search reports a solvable level as impossible.
        /// <para>
        /// It carries what the rules can branch on later: the contents and their hidden flags,
        /// each column's lock, and which colours have <em>ever</em> been completed — two boards
        /// can hold identical bricks and still differ in how many ice columns are still owed a
        /// thaw (D-009).
        /// </para>
        /// </summary>
        private static string StateKey(Search search)
        {
            BoardState state = search.Session.State;
            StringBuilder key = search.Key;
            key.Length = 0;

            for (int index = 0; index < state.ColumnCount; index++)
            {
                BoardColumn column = state[index];

                key.Append((int)column.Kind).Append('/').Append(column.Capacity).Append(column.IsLocked ? 'L' : 'u').Append(':');

                for (int cell = 0; cell < column.Count; cell++)
                {
                    key.Append(column.ColourAt(cell).Value);

                    if (column.IsHiddenAt(cell))
                    {
                        key.Append('?');
                    }

                    key.Append(',');
                }

                key.Append('|');
            }

            key.Append('#');

            for (int colour = BlockColourId.MinId; colour <= BlockColourId.MaxId; colour++)
            {
                key.Append(state.HasEverCompleted(new BlockColourId(colour)) ? '1' : '0');
            }

            return key.ToString();
        }

        /// <summary>
        /// One search's mutable state. A class rather than ref parameters because every one of
        /// these is shared by the whole recursion, and threading six of them through it would
        /// hide what the search actually does.
        /// </summary>
        private sealed class Search
        {
            internal readonly BoardSession Session;

            internal readonly int StateBudget;

            internal readonly HashSet<string> Seen = new HashSet<string>();

            internal readonly List<SolverMove> Path = new List<SolverMove>();

            /// <summary>Reused so the key of every board does not allocate a builder of its own.</summary>
            internal readonly StringBuilder Key = new StringBuilder(256);

            internal int StatesExplored;

            internal bool StoppedEarly;

            internal Search(BoardSession session, int stateBudget)
            {
                Session = session;
                StateBudget = stateBudget;
            }
        }
    }
}
#endif
