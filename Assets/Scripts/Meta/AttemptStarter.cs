using System;
using ColorfulSort.Board;
using ColorfulSort.Content;
using ColorfulSort.Core;
using ColorfulSort.View;
using UnityEngine;

namespace ColorfulSort.Meta
{
    /// <summary>
    /// Turns "which level, which variant, which attempt" into a <see cref="BoardSession"/> and
    /// hands it to the view. It is the seam <c>Meta</c> owns: progression decides what the
    /// player is about to play, and the view is told rather than asked.
    /// <para>
    /// All three answers now come from progression rather than from the Inspector.
    /// <see cref="Progression"/> supplies the level ordinal and the attempt ordinal from the
    /// save file, <see cref="ProgressionConfig"/> supplies the variant count, and the database
    /// turns the ordinal into a level. There is deliberately no serialized "play this level"
    /// override: it would be a second answer to which level is current, and the level editor
    /// is where a specific board gets previewed.
    /// </para>
    /// <para>
    /// It also owns what a win <em>means</em>. `Board` says the level was solved; marking it
    /// cleared, moving the ordinal on, paying for it and deciding there is no next level are
    /// progression policy, so they happen here and the popup only asks (rules/ui.md).
    /// </para>
    /// <para>
    /// And it is where a booster meets its price. The three commands cost a charge again
    /// (D-091 reopens what D-043 closed): the charge is taken <em>after</em> `Board` accepts
    /// the mutation, so a booster the rules refused is free, and buying more is one more
    /// command through this same seam. `UI` reads what is left and sends the command; it never
    /// spends anything itself (rules/ui.md).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttemptStarter : MonoBehaviour
    {
        [SerializeField]
        private BoardView board;

        [Tooltip("The levels in play order. Progression tracks a position in this list, not a level number.")]
        [SerializeField]
        private LevelDatabase database;

        [SerializeField]
        private ProgressionConfig progressionConfig;

        private Progression progression;

        /// <summary>
        /// The purse and the booster charges, rebuilt beside <see cref="progression"/> and for
        /// the same reason (D-088): it owns nothing of its own — every field it answers for
        /// lives in the save — so building it per call costs an allocation and buys an answer
        /// that is always the live file's.
        /// </summary>
        private PlayerEconomy economy;

        /// <summary>The attempt on screen. The boosters are commands against exactly this one.</summary>
        private BoardSession session;

        /// <summary>
        /// Whether the last win moved the player on. Kept because the popup asks for the next
        /// level after the ordinal has already advanced, and "was there one" cannot be
        /// re-derived at that point without asking the question backwards.
        /// </summary>
        private bool advancedOnLastWin;

        /// <summary>The board is won and the view has not finished showing why yet.</summary>
        private bool wonPending;

        /// <summary>
        /// Raised once an attempt is on screen, carrying the level it was built from. This is
        /// how <c>UI</c> learns what to put on the plaque without reaching into the view or
        /// holding a level reference of its own.
        /// </summary>
        public event Action<LevelDefinition> AttemptOpened;

        /// <summary>
        /// The level was solved, and progression has already been updated by the time this is
        /// raised. `UI` subscribes here rather than to `BoardSession` (D-038).
        /// </summary>
        public event Action AttemptWon;

        /// <summary>
        /// The board has no legal move left. It costs nothing — there are no lives and a fail
        /// is not charged for (D-042); the economy D-091 brought back prices boosters, not
        /// failure.
        /// </summary>
        public event Action AttemptDeadlocked;

        /// <summary>
        /// Coins or charges moved. `UI` re-reads the purse and the booster bar from this, the
        /// way it re-reads legality from <see cref="BoardChanged"/> — nothing polls a balance
        /// (rules/ui.md names a coin counter in `Update` as the example of the defect).
        /// </summary>
        public event Action EconomyChanged;

        /// <summary>
        /// The level currently on screen, or null if none opened. It exists for the subscriber
        /// that arrives after <see cref="AttemptOpened"/> has already been raised.
        /// </summary>
        public LevelDefinition OpenedLevel { get; private set; }

        /// <summary>
        /// The board changed in a way a button might care about — a tap, an undo, a booster.
        /// `UI` refreshes which boosters are available from this rather than asking every frame
        /// (rules/ui.md).
        /// </summary>
        public event Action BoardChanged;

        /// <summary>Whether there is a move to take back. False on a fresh board.</summary>
        public bool CanUndo => session != null && session.CanUndo;

        /// <summary>Whether the board can take another column. False at the 16-column ceiling.</summary>
        public bool CanAddColumn => session != null && session.CanAddColumn;

        /// <summary>Whether there are at least two movable cells to rearrange.</summary>
        public bool CanShuffle => session != null && session.CanShuffle;

        /// <summary>How many coins the player has. Read by the HUD, written by nothing outside `Meta`.</summary>
        public int Coins
        {
            get
            {
                PlayerEconomy purse = EnsureEconomy();
                return purse == null ? 0 : purse.Coins;
            }
        }

        /// <summary>
        /// What the last win paid, in coins. Zero for a level that was already cleared — the
        /// reward is for finishing a level, not for finishing it again — and zero is also what
        /// tells the win screen there is nothing to fly across (see <see cref="RaiseWon"/>).
        /// </summary>
        public int LastWinAward { get; private set; }

        /// <summary>How many times this booster can still be used before it has to be bought.</summary>
        public int ChargesOf(BoosterId booster)
        {
            PlayerEconomy purse = EnsureEconomy();
            return purse == null ? 0 : purse.ChargesOf(booster);
        }

        /// <summary>
        /// What a pack of this booster holds and costs, for the popup that offers it. It comes
        /// from config through this seam rather than from the asset directly, so `UI` keeps
        /// exactly one line into `Meta` and holds no config reference of its own.
        /// </summary>
        public BoosterOffer OfferFor(BoosterId booster)
        {
            return progressionConfig == null ? null : progressionConfig.OfferFor(booster);
        }

        /// <summary>Whether the purse covers this booster's pack right now.</summary>
        public bool CanBuy(BoosterId booster)
        {
            BoosterOffer offer = OfferFor(booster);
            PlayerEconomy purse = EnsureEconomy();

            return offer != null && purse != null && purse.CanAfford(offer.Price);
        }

        /// <summary>
        /// Buys a pack of charges at the authored price. The popup asks; this decides, because
        /// what a booster costs and whether it can be afforded are progression's answers and a
        /// popup that checked them would be `UI` deciding policy (rules/ui.md).
        /// </summary>
        /// <returns>Whether the purchase went through.</returns>
        public bool TryBuyBooster(BoosterId booster)
        {
            BoosterOffer offer = OfferFor(booster);

            if (offer == null)
            {
                Debug.LogError("[Meta] " + (progressionConfig == null ? "The progression config" : progressionConfig.name) +
                               " prices no " + booster + " pack, so there is nothing to sell.", this);
                return false;
            }

            PlayerEconomy purse = EnsureEconomy();

            if (purse == null || !purse.TryBuy(booster, offer.Charges, offer.Price))
            {
                return false;
            }

            Debug.Log("[Meta] Bought " + offer.Charges + " " + booster + " charge(s) for " + offer.Price +
                      " coins; " + purse.Coins + " left.");

            RaiseEconomyChanged();
            return true;
        }

        /// <summary>
        /// The three boosters, as commands. Each one costs a charge again — D-091 reopened the
        /// economy D-043 had closed — and the charge is taken only once `Board` has accepted
        /// the mutation, so a booster the rules refused is never paid for. Each redraws the
        /// board afterwards, because a booster's result is a new board rather than an animation
        /// of the old one (D-044).
        /// </summary>
        public void Undo()
        {
            Spend(BoosterId.Undo);
        }

        public void AddColumn()
        {
            Spend(BoosterId.AddColumn);
        }

        public void Shuffle()
        {
            Spend(BoosterId.Shuffle);
        }

        /// <summary>
        /// One booster, end to end: is there a charge, does the board take it, and only then is
        /// the charge spent. The order is the whole rule — checking the charge first stops a
        /// player with none from using one, and spending last stops a refused mutation from
        /// costing anything.
        /// </summary>
        private void Spend(BoosterId booster)
        {
            if (session == null)
            {
                return;
            }

            PlayerEconomy purse = EnsureEconomy();

            if (purse == null)
            {
                return;
            }

            if (purse.ChargesOf(booster) <= 0)
            {
                // Not an error: the button offers the shop at zero, so this is only reachable
                // by a command sent from somewhere that did not look first.
                Debug.Log("[Meta] " + booster + " has no charges left; the bar offers the shop instead.");
                return;
            }

            if (!Apply(booster))
            {
                return;
            }

            purse.TrySpendCharge(booster);
            board.Resync();
            RaiseEconomyChanged();
        }

        /// <summary>
        /// The mutation itself, which is all `Board` ever hears about a booster: no charge, no
        /// price, no popup — three method calls the rules and their tests already prove.
        /// </summary>
        private bool Apply(BoosterId booster)
        {
            switch (booster)
            {
                case BoosterId.Undo:
                    return session.Undo();

                case BoosterId.AddColumn:
                    return session.TryAddColumn();

                case BoosterId.Shuffle:
                    return session.TryShuffle();

                default:
                    return false;
            }
        }

        private void Start()
        {
            StartAttempt();
        }

        /// <summary>
        /// Deals the current level again. Unlike before progression existed, this is a genuinely
        /// new attempt: the play count has moved, so the seed and the variant have too (D-017).
        /// </summary>
        public void Restart()
        {
            StartAttempt();
        }

        /// <summary>
        /// What the Win popup's button asks for. If the win advanced the ordinal there is a
        /// next level and it opens; if it did not, the database ends here and the player goes
        /// back to the menu. The popup makes neither decision.
        /// </summary>
        public void PlayNext()
        {
            if (advancedOnLastWin)
            {
                StartAttempt();
                return;
            }

            GameRoot root = GameRoot.Instance;

            if (root == null)
            {
                Debug.LogWarning("[Meta] There is no next level and no GameRoot to leave through; " +
                                 "this scene was entered without Boot.", this);
                return;
            }

            Debug.Log("[Meta] The database ends at ordinal " + (progression == null ? -1 : progression.CurrentOrdinal) +
                      ", so there is no next level; returning to the menu.");
            root.Scenes.ShowMenu();
        }

        public void StartAttempt()
        {
            if (board == null || database == null || progressionConfig == null)
            {
                Debug.LogError("[Meta] " + name + " has no board view, level database or progression config; " +
                               "run Tools > Colorful Sort > Wire Game Scene.", this);
                return;
            }

            if (!progressionConfig.Validate(out string configError))
            {
                Debug.LogError("[Meta] " + progressionConfig.name + " is not authored yet: " + configError, progressionConfig);
                return;
            }

            Progression player = EnsureProgression();

            if (player == null)
            {
                return;
            }

            LevelDefinition level = database.ByOrdinal(player.CurrentOrdinal);

            if (level == null)
            {
                Debug.LogError("[Meta] " + database.name + " holds no level at ordinal " + player.CurrentOrdinal +
                               " (it has " + database.Count + "). Rebuild it in Tools > Colorful Sort > Level Editor.", database);
                return;
            }

            try
            {
                LevelData data = level.ToLevelData();

                // Read before writing: the play is recorded only once the board is actually up,
                // so a level that refuses to open does not consume an attempt and silently
                // change the board the next honest one deals.
                int attemptOrdinal = player.AttemptOrdinal;
                int variantIndex = attemptOrdinal % progressionConfig.VariantCount;

                AttemptScramble scramble = AttemptScramble.ForVariant(data, variantIndex);

                // The ordinal, not the plaque number: fingerprint.md defines the seed as
                // f(level ordinal, attempt ordinal) and the save carries exactly those two.
                int seed = AttemptSeedSource.For(player.CurrentOrdinal, attemptOrdinal);
                var session = new BoardSession(data, seed, scramble);

                // Subscribed to *this* session, every time. A restart builds a new one, so
                // forwarding that hung off the first session would fire once and never again —
                // invisible until somebody replays a level. The listeners on this component
                // stay attached across all of them, which is the point of forwarding at all.
                session.Won += WonLater;
                session.Deadlocked += RaiseDeadlocked;

                // The view's own "I have caught up" — resubscribed here with the session because a
                // restart replaces neither, and subscribing twice would announce a win twice.
                board.BoardShown -= OnBoardShown;
                board.BoardShown += OnBoardShown;
                wonPending = false;

                // Every mutation, whoever ordered it: a tap the view committed, a booster, an
                // undo. It is what keeps the booster buttons' enabled state true without any of
                // them polling.
                session.MoveApplied += OnBoardMutated;
                session.MoveUndone += OnBoardMutated;

                // The placement is the level's, not the attempt's: it belongs to the slots, and
                // the attempt only decides which authored column stands in each of them (D-033).
                board.Open(session, level.LayoutRows, level.LayoutColumns, level.LayoutCells());

                player.RecordAttemptStarted();

                Debug.Log("[Meta] Attempt " + attemptOrdinal + " of ordinal " + player.CurrentOrdinal +
                          " (level " + level.LevelIndex + "), variant " + variantIndex + ", seed " + seed + ": " +
                          session.State.ColumnCount + " column(s), " + data.BlockCount + " block(s).");

                // Kept only once the board is actually up. A booster fired against a session
                // whose board failed to draw would mutate something nobody can see.
                this.session = session;

                // Announced only once the board is actually up, so a subscriber never renders
                // a plaque for an attempt that then failed to open.
                OpenedLevel = level;

                Action<LevelDefinition> opened = AttemptOpened;

                if (opened != null)
                {
                    opened(level);
                }
            }
            catch (Exception refused)
            {
                // The rules refuse illegal levels by design (D-013 and the LevelData contract),
                // and their message names the exact cell. Repeating it here is what makes a
                // hand-authored fixture debuggable.
                Debug.LogError("[Meta] " + level.name + " cannot be opened: " + refused.Message, level);
            }
        }

        /// <summary>
        /// Progression, built once against the live save. Null means the scene was entered
        /// without Boot, so there is no save to progress through — normal while working in the
        /// editor, and worth saying rather than crashing.
        /// </summary>
        /// <summary>
        /// Progression over the save, built fresh every time it is asked for.
        /// <para>
        /// It used to be cached, and that is what pinned the player to level 1: `Progression` takes
        /// the level <em>count</em> as a number, so an instance made when the database held one level
        /// went on believing there was one after three more were authored — and `HasNext` stayed
        /// false. It owns nothing of its own; it reads and writes the save. So building it per call
        /// costs an allocation and buys a count that is always the database's real one (D-088).
        /// </para>
        /// </summary>
        private Progression EnsureProgression()
        {
            GameRoot root = GameRoot.Instance;

            if (root == null)
            {
                Debug.LogWarning("[Meta] " + name + " cannot read progression: there is no GameRoot, " +
                                 "so this scene was entered without Boot. Press Play from Boot.unity.", this);
                return null;
            }

            progression = new Progression(root.Save.Data, database.Count, root.Save.MarkDirty);
            return progression;
        }

        /// <summary>
        /// The purse over the live save, built fresh for the same reason progression is: it
        /// holds no state of its own, so a stale instance would only ever be a way for the save
        /// and the screen to disagree.
        /// <para>
        /// Building it is also what <em>seeds</em> a first-time player — the constructor gives
        /// the starting coins and the three charges to a save whose booster list is empty — so
        /// the first read of a balance is what creates it. That is deliberate: nothing has to
        /// remember to call an Initialise, and a save file written before this feature existed
        /// gets its charges the first time anything asks.
        /// </para>
        /// </summary>
        private PlayerEconomy EnsureEconomy()
        {
            GameRoot root = GameRoot.Instance;

            if (root == null || progressionConfig == null)
            {
                // Silent on purpose: StartAttempt has already said, loudly and once, that this
                // scene was entered without Boot. A warning per booster read would bury it.
                return null;
            }

            economy = new PlayerEconomy(root.Save.Data, progressionConfig.Seed, root.Save.MarkDirty);
            return economy;
        }

        private void RaiseEconomyChanged()
        {
            Action changed = EconomyChanged;

            if (changed != null)
            {
                changed();
            }
        }

        private void OnBoardMutated(BoardMove move)
        {
            Action changed = BoardChanged;

            if (changed != null)
            {
                changed();
            }
        }

        /// <summary>
        /// The board is solved — but the bricks that solved it are still in the air, because
        /// <c>BoardSession.Won</c> fires the instant a move is legal. So the win is remembered and
        /// announced when the view says it has caught up, which is D-076's split applied to the win
        /// itself: `Board` is right immediately, the *player* is told when there is something to see
        /// (D-088).
        /// </summary>
        private void WonLater()
        {
            wonPending = true;
        }

        /// <summary>
        /// The view has finished drawing whatever it was drawing. If a win was waiting on that, this
        /// is the moment for it — and only once, since a resync can arrive on the same board again.
        /// </summary>
        private void OnBoardShown()
        {
            if (!wonPending)
            {
                return;
            }

            wonPending = false;
            RaiseWon();
        }

        private void RaiseWon()
        {
            // Asked before the level is marked, because completing it is what makes the answer
            // "yes, cleared" — and the reward is for the first clear only. A cleared level can
            // be replayed (the last one indefinitely), and paying for every win would make that
            // replay a coin tap rather than a victory lap.
            bool firstClear = progression != null && !progression.IsCleared(progression.CurrentOrdinal);

            // Progression is updated before anyone is told, so a subscriber that asks what the
            // next level is gets the answer that is already true.
            advancedOnLastWin = progression != null && progression.CompleteCurrentLevel();

            Award(firstClear);

            Debug.Log("[Meta] Level " + (OpenedLevel == null ? -1 : OpenedLevel.LevelIndex) + " solved" +
                      (LastWinAward > 0 ? " for " + LastWinAward + " coins" : " again, so it pays nothing") +
                      (advancedOnLastWin ? "; moving to ordinal " + progression.CurrentOrdinal + "." : "; the database ends here."));

            Action won = AttemptWon;

            if (won != null)
            {
                won();
            }
        }

        /// <summary>
        /// Pays for a level that was just cleared for the first time. The coins are banked
        /// <em>here</em>, the moment the level is finished — the flight the player watches on
        /// the win screen is `UI` catching up with a number that is already true, which is the
        /// same split D-088 made for the win itself.
        /// </summary>
        private void Award(bool firstClear)
        {
            LastWinAward = 0;

            if (!firstClear || progressionConfig == null)
            {
                return;
            }

            PlayerEconomy purse = EnsureEconomy();

            if (purse == null || progressionConfig.CoinsPerLevelCleared <= 0)
            {
                return;
            }

            LastWinAward = progressionConfig.CoinsPerLevelCleared;
            purse.Award(LastWinAward);
            RaiseEconomyChanged();
        }

        private void RaiseDeadlocked()
        {
            Debug.LogWarning("[Meta] Level " + (OpenedLevel == null ? -1 : OpenedLevel.LevelIndex) +
                             " has no legal move left.");

            Action deadlocked = AttemptDeadlocked;

            if (deadlocked != null)
            {
                deadlocked();
            }
        }
    }
}
