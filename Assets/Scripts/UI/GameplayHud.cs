using ColorfulSort.Content;
using ColorfulSort.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorfulSort.UI
{
    /// <summary>
    /// The gameplay screen's furniture: the level plaque with its difficulty label, the gear
    /// that opens Pause, and the booster bar (reference §4). Deliberately no coin counter —
    /// the only balance in the game is in the booster shop, where it can be spent (D-092).
    /// <para>
    /// It owns which popup opens when — Pause, Win, Fail and the booster shop — which is why an
    /// empty booster raises an event here instead of instantiating its own popup, and why the
    /// win popup is handed the style config it needs to say what the level paid.
    /// </para>
    /// <para>
    /// It renders on an event and never polls. The attempt is opened once, says so once, and
    /// this writes two labels — a plaque that read <c>Meta</c> every frame for a number that
    /// changes once per level would be the defect <c>rules/ui.md</c> names by example. There
    /// is no <c>Update</c> in this file.
    /// </para>
    /// <para>
    /// It is a scene object rather than a prefab: one HUD, one scene, placed at authoring
    /// time, which is exactly the case <c>scene-structure.md</c> answers with "scene object".
    /// The popup it opens <em>is</em> a prefab, because the host spawns it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayHud : MonoBehaviour
    {
        [Tooltip("The attempt seam in Meta. It says which level is being played, and it is what a restart goes through.")]
        [SerializeField]
        private AttemptStarter attempt;

        [Tooltip("Where the difficulty words and the plaque format live, so no text sits in C#.")]
        [SerializeField]
        private UiStyleConfig style;

        [SerializeField]
        private TMP_Text levelLabel;

        [SerializeField]
        private TMP_Text difficultyLabel;

        [SerializeField]
        private Button gearButton;

        [SerializeField]
        private PausePopup pausePopupPrefab;

        [SerializeField]
        private WinPopup winPopupPrefab;

        [SerializeField]
        private FailPopup failPopupPrefab;

        [Header("Boosters")]
        [Tooltip("The popup an empty booster's plus opens. One prefab; which booster it sells is chosen at runtime.")]
        [SerializeField]
        private BoosterShopPopup boosterShopPopupPrefab;

        [SerializeField]
        private BoosterButton addColumnButton;

        [SerializeField]
        private BoosterButton undoButton;

        [SerializeField]
        private BoosterButton shuffleButton;

        private void OnEnable()
        {
            if (attempt == null || style == null || gearButton == null)
            {
                Debug.LogError("[UI] " + name + " is not wired; run Tools > Colorful Sort > Build UI.", this);
                return;
            }

            if (!style.Validate(out string error))
            {
                Debug.LogError("[UI] " + style.name + " is not authored yet: " + error, style);
            }

            gearButton.onClick.AddListener(OpenPause);
            attempt.AttemptOpened += Render;

            // The two endings come from Meta, not from Board: the arrow UI → Meta already
            // exists, and forwarding them there means a restart's new session cannot leave
            // this screen listening to a dead one (D-038).
            attempt.AttemptWon += ShowWin;
            attempt.AttemptDeadlocked += ShowFail;

            // The bar acts on the attempt, and re-reads what is available whenever the board
            // moves — a tap, a booster, an undo. Nothing polls.
            BindBoosters();
            attempt.BoardChanged += RefreshBoosters;

            // The purse moves for reasons the board knows nothing about — a win pays, a purchase
            // spends — so it is its own event and its own render (rules/ui.md).
            attempt.EconomyChanged += RenderEconomy;
            RenderEconomy();

            // Subscribing is enough when the HUD and the attempt start together — every
            // OnEnable runs before any Start. It is not enough if this object was enabled
            // later, and then the attempt already happened and has nothing left to announce.
            if (attempt.OpenedLevel != null)
            {
                Render(attempt.OpenedLevel);
            }
        }

        private void OnDisable()
        {
            if (gearButton != null)
            {
                gearButton.onClick.RemoveListener(OpenPause);
            }

            if (attempt != null)
            {
                attempt.AttemptOpened -= Render;
                attempt.AttemptWon -= ShowWin;
                attempt.AttemptDeadlocked -= ShowFail;
                attempt.BoardChanged -= RefreshBoosters;
                attempt.EconomyChanged -= RenderEconomy;
            }

            Unhook(addColumnButton);
            Unhook(undoButton);
            Unhook(shuffleButton);
        }

        private void BindBoosters()
        {
            Hook(addColumnButton);
            Hook(undoButton);
            Hook(shuffleButton);
        }

        /// <summary>
        /// Binds one booster button and listens for its empty press. The shop is opened here and
        /// not by the button, because which popup opens when is this screen's job — the same
        /// reason Pause, Win and Fail are all instantiated in this file.
        /// </summary>
        private void Hook(BoosterButton bar)
        {
            if (bar == null)
            {
                return;
            }

            bar.ShopRequested -= OpenBoosterShop;
            bar.ShopRequested += OpenBoosterShop;
            bar.Bind(attempt);
        }

        private void Unhook(BoosterButton bar)
        {
            if (bar != null)
            {
                bar.ShopRequested -= OpenBoosterShop;
            }
        }

        private void RefreshBoosters()
        {
            if (addColumnButton != null)
            {
                addColumnButton.Refresh();
            }

            if (undoButton != null)
            {
                undoButton.Refresh();
            }

            if (shuffleButton != null)
            {
                shuffleButton.Refresh();
            }
        }

        /// <summary>
        /// The purse changed, so the bar re-reads its charges — a purchase is the case that
        /// needs it, since buying happens in a popup and moves nothing on the board. There is no
        /// balance on this screen to redraw: the only counter in the game is in the shop (D-092).
        /// </summary>
        private void RenderEconomy()
        {
            RefreshBoosters();
        }

        /// <summary>
        /// A booster ran out and was pressed. The bar hands over which booster and the picture it
        /// is wearing; everything else the popup shows it asks `Meta` and the style config for.
        /// </summary>
        private void OpenBoosterShop(BoosterButton bar)
        {
            if (bar == null || boosterShopPopupPrefab == null)
            {
                Debug.LogError("[UI] " + name + " has no booster shop prefab; run Tools > Colorful Sort > Build UI.", this);
                return;
            }

            PopupHost host = Host("the booster shop");

            if (host == null)
            {
                return;
            }

            BoosterShopPopup popup = host.Open(boosterShopPopupPrefab);

            if (popup != null)
            {
                popup.Bind(attempt, style, bar.Booster, bar.Icon);
            }
        }

        /// <summary>
        /// The plaque, derived from the authority rather than stored: `Content` owns the
        /// level's index and its authored difficulty, and this turns them into the two words
        /// the player reads.
        /// </summary>
        private void Render(LevelDefinition level)
        {
            if (levelLabel != null)
            {
                levelLabel.text = style.PlaqueFor(level.LevelIndex);
            }

            if (difficultyLabel != null)
            {
                difficultyLabel.text = style.LabelFor(level.Difficulty);
            }

            // A new attempt has nothing to undo, and its column count may differ from the last
            // one's, so the bar is re-read here as well as on every board change.
            RefreshBoosters();
        }

        private void OpenPause()
        {
            PopupHost host = Host("Pause");

            if (host == null)
            {
                return;
            }

            PausePopup popup = host.Open(pausePopupPrefab);

            if (popup != null)
            {
                // The popup lives under Boot's persistent canvas and this HUD lives in the
                // Game scene, so the restart target travels by hand. Nothing searches.
                popup.Bind(attempt);
            }
        }

        /// <summary>
        /// The level was solved. It can arrive while the last bricks are still dropping — the
        /// move is committed in `Board` the instant it is legal and the tween is what the
        /// player watches afterwards (D-031) — so this popup is correct and early. Waiting for
        /// the board to settle would need a signal `BoardView` does not offer and `UI` cannot
        /// ask for without a new dependency arrow.
        /// </summary>
        private void ShowWin()
        {
            PopupHost host = Host("the win popup");

            if (host == null)
            {
                return;
            }

            WinPopup popup = host.Open(winPopupPrefab);

            if (popup != null)
            {
                // The style config travels with the attempt because the popup shows what the
                // level paid, and how a coin amount is written is this screen's asset to know.
                popup.Bind(attempt, style);
            }
        }

        private void ShowFail()
        {
            PopupHost host = Host("the fail popup");

            if (host == null)
            {
                return;
            }

            FailPopup popup = host.Open(failPopupPrefab);

            if (popup != null)
            {
                popup.Bind(attempt);
            }
        }

        private PopupHost Host(string what)
        {
            PopupHost host = PopupHost.Instance;

            if (host == null)
            {
                Debug.LogWarning("[UI] There is no popup host, so " + what + " cannot open: " +
                                 "this scene was entered without Boot.", this);
            }

            return host;
        }
    }
}
