#if UNITY_EDITOR
using System.Collections.Generic;
using ColorfulSort.Content;
using ColorfulSort.Meta;
using ColorfulSort.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorfulSort.EditorTools
{
    /// <summary>
    /// Builds the UI foundation: the shared text material, the Pause popup prefab, Boot's
    /// persistent popup canvas, and the gameplay HUD in the Game scene.
    /// <para>
    /// A script rather than a click-through for the same reasons the board factory is one.
    /// Three of the things it sets are invisible when wrong and expensive to find: the canvas
    /// scaler's reference resolution and match value (rules/ui.md — 1080×1920, 0.5), the
    /// event system's input module (this project runs the new input backend only, so
    /// <c>StandaloneInputModule</c> is compiled out and picking it leaves every button
    /// silently dead), and which graphics are raycast targets — a HUD panel left as a target
    /// swallows every tap meant for the board.
    /// </para>
    /// <para>
    /// Non-destructive, like <c>BoardViewPrefabFactory</c>: an existing prefab, material or
    /// config asset is left exactly as it is, and in a scene only <em>empty</em> reference
    /// slots are filled. Delete a thing to have it rebuilt. That is what makes the authored
    /// asset the authority and this script only its first draft.
    /// </para>
    /// </summary>
    public static class UiFactory
    {
        private const string PrefabFolder = "Assets/Prefabs/UI";
        private const string UiSprites = "Assets/Art/Sprites/UI";

        /// <summary>
        /// The second UI set, which is where the state icons live: it ships every toggle icon
        /// as an on/off pair, and the first pack has no crossed-out variant and no music icon
        /// at all.
        /// </summary>
        private const string FmUiSprites = "Assets/Art/Sprites/fm_ui";
        private const string StyleConfigAsset = "Assets/Data/Config/UiStyleConfig.asset";

        private const string LevelDatabaseAsset = "Assets/Data/Levels/LevelDatabase.asset";
        private const string TextMaterialAsset = "Assets/Art/Materials/UI/Text_Reference.mat";
        private const string PausePopupAsset = PrefabFolder + "/Popup_Pause.prefab";
        private const string WinPopupAsset = PrefabFolder + "/Popup_Win.prefab";
        private const string FailPopupAsset = PrefabFolder + "/Popup_Fail.prefab";
        private const string BoosterShopPopupAsset = PrefabFolder + "/Popup_BoosterShop.prefab";
        private const string BoosterButtonAsset = PrefabFolder + "/BoosterButton.prefab";
        private const string CoinFlyerAsset = PrefabFolder + "/CoinFlyer.prefab";

        /// <summary>The icon child every booster button carries, and how big it is inside the 220 px shell.</summary>
        private const string BoosterIconName = "Icon";

        private const float BoosterIconSize = 110f;

        /// <summary>
        /// The two badges a booster wears, one at a time: the red disc counting what is left, and
        /// the green plus that means "empty, and this opens the shop". They are one object each
        /// rather than one object with two sprites, because a count needs a label under it and a
        /// plus does not — and `BoosterButton` switching two objects is one line either way.
        /// </summary>
        private const string CountBadgeName = "CountBadge";

        private const string PlusBadgeName = "PlusBadge";

        private const float BadgeSize = 96f;

        /// <summary>Bottom-right of the 220 px shell, where the reference hangs both badges.</summary>
        private static readonly Vector2 BadgeCorner = new Vector2(78f, -78f);

        /// <summary>
        /// Which icon each booster wears. It is a list rather than three calls because the same list
        /// answers a second question: whether a sprite already on a booster is one this tool put
        /// there and may replace, or somebody's own choice that must be left alone.
        /// <para>
        /// The first entry is also the prefab's default, so a booster is never a blank shell.
        /// </para>
        /// </summary>
        private static readonly BoosterIcon[] BoosterIcons =
        {
            new BoosterIcon("AddColumn", "boost-stack"),
            new BoosterIcon("Undo", "boost-undo"),
            new BoosterIcon("Shuffle", "boost-shuffle"),
        };

        private const string BootScene = "Assets/Scenes/Boot.unity";
        private const string GameScene = "Assets/Scenes/Game.unity";
        private const string MenuScene = "Assets/Scenes/Menu.unity";

        /// <summary>rules/ui.md: portrait 1080×1920, and 0.5 so neither axis wins a crop.</summary>
        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        // Screen UI sits at 100+ (the art pack's sorting order). Popups are a second canvas
        // above the HUD so "on top" is a property of the canvas, not of who was added first.
        private const int HudSortingOrder = 100;
        private const int PopupSortingOrder = 200;

        [MenuItem("Tools/Colorful Sort/Build UI")]
        public static void BuildUi()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "UI");
            EnsureFolder("Assets/Art", "Materials");
            EnsureFolder("Assets/Art/Materials", "UI");

            UiStyleConfig style = EnsureStyleConfig();

            if (!style.Validate(out string styleError))
            {
                Debug.LogError("[Colorful Sort] " + StyleConfigAsset + " is not authored yet, so no text can be styled: " +
                               styleError + " Fill it in and run this again.", style);
                return;
            }

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;

            if (font == null)
            {
                Debug.LogError("[Colorful Sort] There is no TextMeshPro font asset in this project. " +
                               "Run Window > TextMeshPro > Import TMP Essential Resources first — until then every label is blank.");
                return;
            }

            Material textMaterial = EnsureTextMaterial(font, style);

            if (textMaterial == null)
            {
                return;
            }

            BuildPausePopup(font, textMaterial, style);
            BuildWinPopup(font, textMaterial, style);
            EnsureWinReward(font, textMaterial, style);
            BuildFailPopup(font, textMaterial, style);
            BuildBoosterShopPopup(font, textMaterial, style);
            EnsureBoosterShopScrim();
            EnsureBoosterShopWiring();
            BuildBoosterButton(font, textMaterial, style);
            RetireCoinFlyer();
            WireBootScene();
            WireGameScene(style, font, textMaterial);
            WireMenuScene(style, font, textMaterial);

            // Each pass opens its scene in Single mode, so without this the tool ends with the
            // last one it touched still open — and pressing Play there runs a screen scene on
            // its own, with no GameRoot, no popup host and no EventSystem, all three of which
            // live in Boot. A dead Play button with nothing in the console is the symptom, and
            // it is the tool's fault, not the player's.
            ReturnToBoot();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------ assets

        /// <summary>
        /// The style asset, created empty-but-seeded if it is missing and never touched if it
        /// is there. The colours and the words it starts with are the reference's, not a
        /// preference (reference §4 and §6); the outline and shadow *shape* numbers are a
        /// starting point, and the asset is where they get tuned.
        /// </summary>
        private static UiStyleConfig EnsureStyleConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UiStyleConfig>(StyleConfigAsset);

            if (existing != null)
            {
                return existing;
            }

            UiStyleConfig created = ScriptableObject.CreateInstance<UiStyleConfig>();
            var serialized = new SerializedObject(created);

            serialized.FindProperty("textFill").colorValue = Html("#FFF6D6");
            serialized.FindProperty("textOutline").colorValue = Html("#4237A1");
            serialized.FindProperty("textShadow").colorValue = Html("#2A2835", 0.6f);
            serialized.FindProperty("outlineWidth").floatValue = 0.2f;
            serialized.FindProperty("shadowOffset").vector2Value = new Vector2(0.4f, -0.4f);
            serialized.FindProperty("shadowSoftness").floatValue = 0.25f;
            serialized.FindProperty("levelPlaqueFormat").stringValue = "Level {0}";
            serialized.FindProperty("menuLevelFormat").stringValue = "LEVEL {0}";
            serialized.FindProperty("menuNoLevelText").stringValue = "No levels yet";
            serialized.FindProperty("difficultyNormal").stringValue = "Normal";
            serialized.FindProperty("difficultyHard").stringValue = "Hard";
            serialized.FindProperty("difficultySuperHard").stringValue = "Super Hard";

            // The shop's copy: what each booster is called and the one line saying what it does.
            // Seeded rather than left blank because Validate refuses an unauthored asset, and a
            // freshly created config that immediately fails its own validation is a tool leaving
            // a trap rather than a starting point. Every one of these is the user's to rewrite.
            serialized.FindProperty("boosterTitleAddColumn").stringValue = "Extra Tube";
            serialized.FindProperty("boosterTitleUndo").stringValue = "Undo";
            serialized.FindProperty("boosterTitleShuffle").stringValue = "Shuffle";
            serialized.FindProperty("boosterBlurbAddColumn").stringValue = "Adds an empty slot!";
            serialized.FindProperty("boosterBlurbUndo").stringValue = "Takes your last move back!";
            serialized.FindProperty("boosterBlurbShuffle").stringValue = "Rearranges every loose block!";
            serialized.FindProperty("boosterBuyFormat").stringValue = "Get +{0}";
            serialized.FindProperty("coinAmountFormat").stringValue = "{0}";

            // The plus sign lives in the string, not in the code that writes it: "what a win
            // says it paid" is copy, and copy is this asset's (D-092).
            serialized.FindProperty("coinRewardFormat").stringValue = "+{0}";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(created, StyleConfigAsset);

            Debug.Log("[Colorful Sort] Created " + StyleConfigAsset + ". Its colours and words are the reference's; " +
                      "the outline width, shadow offset and softness are a starting point — tune them there, not in code.", created);

            return created;
        }

        /// <summary>
        /// One material for every label in the game, generated from the style asset the same
        /// way a brick's material is generated from its skin's colour (D-020). The alternative
        /// — letting each label override its own material — clones the material per component
        /// and quietly turns one draw call into dozens.
        /// </summary>
        private static Material EnsureTextMaterial(TMP_FontAsset font, UiStyleConfig style)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialAsset);

            if (existing != null)
            {
                return existing;
            }

            if (font.material == null)
            {
                Debug.LogError("[Colorful Sort] " + font.name + " has no material, so no text style can be built from it.", font);
                return null;
            }

            var material = new Material(font.material) { name = "Text_Reference" };

            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, style.TextOutline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, style.OutlineWidth);

            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, style.TextShadow);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, style.ShadowOffset.x);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, style.ShadowOffset.y);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, style.ShadowSoftness);

            AssetDatabase.CreateAsset(material, TextMaterialAsset);
            Debug.Log("[Colorful Sort] Created " + TextMaterialAsset + " from " + StyleConfigAsset + ".", material);

            return material;
        }

        // ------------------------------------------------------------ prefab

        /// <summary>
        /// The Pause popup, built once. Its static copy — the title and the two button words —
        /// is baked here into the prefab, because copy that never changes is exactly what
        /// `data-source.md` says to bake, and a second copy in the config asset would be a
        /// second authority. Edit the words in the prefab.
        /// </summary>
        private static void BuildPausePopup(TMP_FontAsset font, Material textMaterial, UiStyleConfig style)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PausePopupAsset) != null)
            {
                return;
            }

            RectTransform root = NewRect("Popup_Pause", null);
            Stretch(root);
            PausePopup popup = root.gameObject.AddComponent<PausePopup>();

            Image body = NewImage("Body", root, LoadSprite("Popups/popup_body_9slice"), Image.Type.Sliced, false);
            Place(body.rectTransform, Vector2.zero, new Vector2(880f, 980f));

            Image header = NewImage("Header", body.rectTransform, LoadSprite("Popups/popup_header_9slice"), Image.Type.Sliced, false);
            Place(header.rectTransform, new Vector2(0f, 460f), new Vector2(800f, 190f));

            TextMeshProUGUI title = NewLabel("Title", header.rectTransform, "Pause", 72f, font, textMaterial, style.TextFill);
            Stretch(title.rectTransform);

            Button close = NewButton("Close", body.rectTransform, "Buttons/close_shell", new Vector2(380f, 460f), new Vector2(130f, 130f));
            Image closeIcon = NewImage("Icon", close.image.rectTransform, LoadSprite("Icons/close"), Image.Type.Simple, false);
            Place(closeIcon.rectTransform, Vector2.zero, new Vector2(70f, 70f));

            // The row holds the three *toggles* and nothing else. Restart used to sit in it and
            // was the odd one out — a command with no on/off state, which is why it was the one
            // round button whose off-state graphic was built and then thrown away.
            RectTransform row = NewRect("Row", body.rectTransform);
            Place(row, new Vector2(0f, 270f), new Vector2(600f, 170f));

            ToggleButton(row, "Music", "icon_music", -190f, SettingToggleButton.Setting.Music);
            ToggleButton(row, "Sound", "icon_sound", 0f, SettingToggleButton.Setting.Sound);
            ToggleButton(row, "Vibration", "icon_vibrate", 190f, SettingToggleButton.Setting.Vibration);

            Button restart = WideButton(body.rectTransform, "Restart", "Buttons/wide_lavender", "Restart", 80f, font, textMaterial, style.TextFill);
            Button continueButton = WideButton(body.rectTransform, "Continue", "Buttons/wide_green", "Continue", -110f, font, textMaterial, style.TextFill);
            Button quitButton = WideButton(body.rectTransform, "Quit", "Buttons/wide_red", "Quit", -300f, font, textMaterial, style.TextFill);

            TextMeshProUGUI playerId = NewLabel("PlayerId", body.rectTransform, string.Empty, 30f, font, textMaterial, style.TextFill);
            Place(playerId.rectTransform, new Vector2(0f, -430f), new Vector2(700f, 50f));

            var serialized = new SerializedObject(popup);
            serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
            serialized.FindProperty("restartButton").objectReferenceValue = restart;
            serialized.FindProperty("quitButton").objectReferenceValue = quitButton;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.FindProperty("playerIdLabel").objectReferenceValue = playerId;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PausePopupAsset);
            Object.DestroyImmediate(root.gameObject);

            Debug.Log("[Colorful Sort] Created " + PausePopupAsset + ".");
        }

        /// <summary>
        /// The panel every popup is: body, header, title. Extracted at the third caller rather
        /// than the first — Pause built its own before there was anything to share it with, and
        /// it keeps that until a task has reason to open it again.
        /// </summary>
        private static RectTransform NewPopupRoot(string popupName, string title, Vector2 bodySize,
            TMP_FontAsset font, Material textMaterial, Color fill, out Image body)
        {
            RectTransform root = NewRect(popupName, null);
            Stretch(root);

            body = NewImage("Body", root, LoadSprite("Popups/popup_body_9slice"), Image.Type.Sliced, false);
            Place(body.rectTransform, Vector2.zero, bodySize);

            // The header overhangs the body's top edge, as the reference panels do.
            Image header = NewImage("Header", body.rectTransform, LoadSprite("Popups/popup_header_9slice"), Image.Type.Sliced, false);
            Place(header.rectTransform, new Vector2(0f, bodySize.y * 0.5f - 30f), new Vector2(bodySize.x - 80f, 190f));

            TextMeshProUGUI titleLabel = NewLabel("Title", header.rectTransform, title, 68f, font, textMaterial, fill);
            Stretch(titleLabel.rectTransform);

            return root;
        }

        /// <summary>
        /// The solved-level popup. One button and no close cross: dismissing it would leave the
        /// player on a finished board. The word "Continue" is baked here into the prefab, like
        /// every other piece of copy that never changes.
        /// </summary>
        private static void BuildWinPopup(TMP_FontAsset font, Material textMaterial, UiStyleConfig style)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WinPopupAsset) != null)
            {
                return;
            }

            RectTransform root = NewPopupRoot("Popup_Win", "Level Complete", new Vector2(880f, 620f),
                font, textMaterial, style.TextFill, out Image body);

            WinPopup popup = root.gameObject.AddComponent<WinPopup>();
            Button continueButton = WideButton(body.rectTransform, "Continue", "Buttons/wide_green", "Continue",
                -160f, font, textMaterial, style.TextFill);

            var serialized = new SerializedObject(popup);
            serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root.gameObject, WinPopupAsset);
            Object.DestroyImmediate(root.gameObject);

            Debug.Log("[Colorful Sort] Created " + WinPopupAsset + ".");
        }

        /// <summary>
        /// The no-legal-move popup: retry or leave. It spends nothing — what a deadlock costs
        /// is OPEN-3 and belongs to Meta's economy in task 5.
        /// </summary>
        private static void BuildFailPopup(TMP_FontAsset font, Material textMaterial, UiStyleConfig style)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FailPopupAsset) != null)
            {
                return;
            }

            RectTransform root = NewPopupRoot("Popup_Fail", "No Moves Left", new Vector2(880f, 720f),
                font, textMaterial, style.TextFill, out Image body);

            FailPopup popup = root.gameObject.AddComponent<FailPopup>();
            Button retryButton = WideButton(body.rectTransform, "Retry", "Buttons/wide_green", "Retry",
                -110f, font, textMaterial, style.TextFill);
            Button quitButton = WideButton(body.rectTransform, "Quit", "Buttons/wide_red", "Quit",
                -300f, font, textMaterial, style.TextFill);

            var serialized = new SerializedObject(popup);
            serialized.FindProperty("retryButton").objectReferenceValue = retryButton;
            serialized.FindProperty("quitButton").objectReferenceValue = quitButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root.gameObject, FailPopupAsset);
            Object.DestroyImmediate(root.gameObject);

            Debug.Log("[Colorful Sort] Created " + FailPopupAsset + ".");
        }

        /// <summary>
        /// The popup behind an empty booster's plus: icon, one line of copy, and a pack to buy.
        /// One prefab for all three boosters — the title, the blurb and the button's wording are
        /// written at runtime from <see cref="UiStyleConfig"/>, so what is baked here is only the
        /// furniture (D-091).
        /// <para>
        /// The authored strings below are what Prefab Mode shows; `BoosterShopPopup.Bind`
        /// overwrites all three the moment it opens. They are not a second authority — they are
        /// what stops the prefab looking empty on the editing canvas, the same role the menu's
        /// authored button text plays (D-086).
        /// </para>
        /// </summary>
        private static void BuildBoosterShopPopup(TMP_FontAsset font, Material textMaterial, UiStyleConfig style)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BoosterShopPopupAsset) != null)
            {
                return;
            }

            RectTransform root = NewPopupRoot("Popup_BoosterShop", "Booster", new Vector2(880f, 1080f),
                font, textMaterial, style.TextFill, out Image body);

            BoosterShopPopup popup = root.gameObject.AddComponent<BoosterShopPopup>();

            // The cross is the pack's close shell, in the corner Pause already puts it in.
            Button close = NewButton("Close", body.rectTransform, "Buttons/close_shell", new Vector2(380f, 510f), new Vector2(130f, 130f));
            Image closeIcon = NewImage("Icon", close.image.rectTransform, LoadSprite("Icons/close"), Image.Type.Simple, false);
            Place(closeIcon.rectTransform, Vector2.zero, new Vector2(70f, 70f));

            Sprite frameSprite = LoadFmSprite("Panels/panel_box_container");
            Image frame = NewImage("IconFrame", body.rectTransform, frameSprite, SliceFor(frameSprite), false);
            Place(frame.rectTransform, new Vector2(0f, 190f), new Vector2(380f, 380f));

            // Dressed with the first booster's icon so the prefab is never a blank square; Bind
            // replaces it with whichever button was pressed, which is the same icon the bar wears.
            Image icon = NewImage("Icon", frame.rectTransform, LoadSprite(BoosterIcons[0].Path), Image.Type.Simple, false);
            Place(icon.rectTransform, Vector2.zero, new Vector2(240f, 240f));

            TextMeshProUGUI blurb = NewLabel("Blurb", body.rectTransform, "What this booster does", 44f, font, textMaterial, style.TextFill);
            Place(blurb.rectTransform, new Vector2(0f, -70f), new Vector2(760f, 100f));

            Button buy = NewButton("Buy", body.rectTransform, "Buttons/wide_green", new Vector2(0f, -300f), new Vector2(640f, 210f));

            TextMeshProUGUI caption = NewLabel("Caption", buy.image.rectTransform, "Get +3", 60f, font, textMaterial, style.TextFill);
            Place(caption.rectTransform, new Vector2(0f, 40f), new Vector2(600f, 90f));

            Image priceCoin = NewImage("Coin", buy.image.rectTransform, LoadSprite("HUD/coin"), Image.Type.Simple, false);
            Place(priceCoin.rectTransform, new Vector2(-70f, -50f), new Vector2(60f, 60f));

            TextMeshProUGUI price = NewLabel("Price", buy.image.rectTransform, "750", 46f, font, textMaterial, style.TextFill);
            Place(price.rectTransform, new Vector2(30f, -50f), new Vector2(220f, 70f));

            var serialized = new SerializedObject(popup);
            serialized.FindProperty("titleLabel").objectReferenceValue = Find<TextMeshProUGUI>(body.rectTransform.Find("Header"), "Title");
            serialized.FindProperty("blurbLabel").objectReferenceValue = blurb;
            serialized.FindProperty("iconImage").objectReferenceValue = icon;
            serialized.FindProperty("buyButton").objectReferenceValue = buy;
            serialized.FindProperty("buyLabel").objectReferenceValue = caption;
            serialized.FindProperty("priceLabel").objectReferenceValue = price;
            serialized.FindProperty("closeButton").objectReferenceValue = close;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root.gameObject, BoosterShopPopupAsset);
            Object.DestroyImmediate(root.gameObject);

            Debug.Log("[Colorful Sort] Created " + BoosterShopPopupAsset + ".");
        }

        /// <summary>
        /// Gives the booster shop its own darkening layer, inside the prefab, behind everything
        /// else it carries.
        /// <para>
        /// <c>PopupHost</c> already puts a scrim under whatever is on top, and that one is not
        /// going anywhere — it darkens every other popup and it is what stops a tap reaching the
        /// board (D-037). This is a <em>second</em> one, asked for on the shop specifically, so
        /// the two stack and the shop sits behind roughly 0.8 rather than 0.55. Stating that is
        /// the point: it is a look, chosen, not an accident of two layers nobody counted.
        /// </para>
        /// <para>
        /// First sibling, so the panel and the coin pill both draw over it, and added only when
        /// absent — the alpha is then the user's, like every other tuned value this tool leaves
        /// alone (D-053).
        /// </para>
        /// </summary>
        private static void EnsureBoosterShopScrim()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BoosterShopPopupAsset) == null)
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(BoosterShopPopupAsset);

            try
            {
                var root = (RectTransform)contents.transform;

                if (root.Find("Scrim") != null)
                {
                    return;
                }

                // No sprite: a plain tinted rect, the same thing Boot's scrim is. A raycast
                // target, so a tap outside the panel lands on this rather than on whatever the
                // popup happens to be covering.
                Image scrim = NewImage("Scrim", root, null, Image.Type.Simple, true);
                scrim.color = new Color(0f, 0f, 0f, 0.55f);
                Stretch(scrim.rectTransform);
                scrim.rectTransform.SetAsFirstSibling();

                PrefabUtility.SaveAsPrefabAsset(contents, BoosterShopPopupAsset);
                Debug.Log("[Colorful Sort] " + BoosterShopPopupAsset + " now carries its own scrim, " +
                          "on top of the popup host's — the two stack, so the shop sits behind about 0.8 (D-093).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Fills whichever of the shop popup's reference slots are empty, on a prefab this tool
        /// did not just build.
        /// <para>
        /// It exists because of a real failure: rearranging the popup by hand — deleting the
        /// header the title used to live in — left <c>titleLabel</c> pointing at nothing, and a
        /// null label is silently skipped, so every booster's popup showed the prefab's authored
        /// placeholder instead of its name. The component's guard was right; what was missing was
        /// anything able to notice an empty slot after the build pass had already returned. That
        /// is the same lesson as the booster icon and the base plate, at the ninth time of asking
        /// (D-068, D-071).
        /// </para>
        /// <para>
        /// Children are looked up <em>anywhere</em> under the popup rather than by their original
        /// parent, precisely because the layout has been rearranged: a search that insisted on
        /// `Body/Header/Title` would fail for exactly the edit that caused this. And only empty
        /// slots are written, so a renamed close button or a hand-picked icon is never
        /// second-guessed.
        /// </para>
        /// </summary>
        private static void EnsureBoosterShopWiring()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BoosterShopPopupAsset) == null)
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(BoosterShopPopupAsset);

            try
            {
                var popup = contents.GetComponent<BoosterShopPopup>();

                if (popup == null)
                {
                    return;
                }

                Transform root = contents.transform;
                var notes = new List<string>();
                var serialized = new SerializedObject(popup);

                FillIfEmpty(serialized, "titleLabel", FindDeep<TextMeshProUGUI>(root, "Title"), notes);
                FillIfEmpty(serialized, "blurbLabel", FindDeep<TextMeshProUGUI>(root, "Blurb"), notes);
                FillIfEmpty(serialized, "priceLabel", FindDeep<TextMeshProUGUI>(root, "Price"), notes);
                FillIfEmpty(serialized, "buyButton", FindDeep<Button>(root, "Buy"), notes);
                FillIfEmpty(serialized, "coinHud", FindDeep<CoinHud>(root, "CoinPill"), notes);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                // Nothing was empty, so nothing was written — and a prefab saved for no change is
                // a file the user's version control has to explain.
                if (notes.Count == 0)
                {
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, BoosterShopPopupAsset);
                Debug.Log("[Colorful Sort] " + BoosterShopPopupAsset + ": " + string.Join(", ", notes) + ".");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// The first descendant with this name that carries a <typeparamref name="T"/>, at any
        /// depth. <see cref="Find{T}"/> beside it looks at direct children only, which is the
        /// right tool when the parent is known and the wrong one after somebody has moved things.
        /// </summary>
        private static T FindDeep<T>(Transform root, string childName) where T : Component
        {
            foreach (T candidate in root.GetComponentsInChildren<T>(true))
            {
                if (candidate.name == childName)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Deletes the flying-coin prefab an earlier version of this tool made. The flight was
        /// dropped for a written reward instead (D-092), and a prefab nothing instantiates is
        /// worse than no prefab — it looks like a feature somebody forgot to wire.
        /// <para>
        /// Retiring by name, the same shape the plume used to retire the trail: it runs once,
        /// finds nothing on every later run, and costs no branch anywhere else.
        /// </para>
        /// </summary>
        private static void RetireCoinFlyer()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CoinFlyerAsset) == null)
            {
                return;
            }

            AssetDatabase.DeleteAsset(CoinFlyerAsset);
            Debug.Log("[Colorful Sort] Deleted " + CoinFlyerAsset + "; the win says what it paid instead of throwing coins (D-092).");
        }

        /// <summary>
        /// Gives the win popup the row that says what the level paid: the coin, and the amount
        /// beside it. Ensured rather than created for the reason the booster icon is — the popup
        /// was built several tasks ago and a pass that only dresses what it makes would never
        /// reach it (D-068, D-071).
        /// <para>
        /// It also retires <c>RewardAnchor</c>, the empty rect the coins used to fly out of. An
        /// anchor for a flight that no longer exists is exactly the leftover the plume taught
        /// this tool to clean up rather than leave lying about.
        /// </para>
        /// </summary>
        private static void EnsureWinReward(TMP_FontAsset font, Material textMaterial, UiStyleConfig style)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WinPopupAsset) == null)
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(WinPopupAsset);

            try
            {
                var popup = contents.GetComponent<WinPopup>();

                if (popup == null)
                {
                    return;
                }

                Transform body = contents.transform.Find("Body");
                RectTransform parent = body == null ? (RectTransform)contents.transform : (RectTransform)body;
                bool changed = false;

                Transform retired = parent.Find("RewardAnchor");

                if (retired != null)
                {
                    Object.DestroyImmediate(retired.gameObject);
                    changed = true;
                }

                Transform row = parent.Find("Reward");

                if (row == null)
                {
                    // Above the button, where the reference puts it. The exact place is the
                    // user's — this only has to put it somewhere visible on the first run.
                    RectTransform created = NewRect("Reward", parent);
                    Place(created, new Vector2(0f, 60f), new Vector2(340f, 110f));

                    Image coin = NewImage("Coin", created, LoadSprite("HUD/coin"), Image.Type.Simple, false);
                    Place(coin.rectTransform, new Vector2(-90f, 0f), new Vector2(95f, 95f));

                    // "+20" is what Prefab Mode shows; Bind writes the real award, formatted by
                    // the style config, the moment the popup opens.
                    TextMeshProUGUI amount = NewLabel("Amount", created, "+20", 60f, font, textMaterial, style.TextFill);
                    Place(amount.rectTransform, new Vector2(45f, 0f), new Vector2(200f, 95f));

                    row = created;
                    changed = true;
                }

                var notes = new List<string>();
                var serialized = new SerializedObject(popup);
                FillIfEmpty(serialized, "rewardRow", row.gameObject, notes);
                FillIfEmpty(serialized, "rewardLabel", Find<TextMeshProUGUI>(row, "Amount"), notes);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (!changed && notes.Count == 0)
                {
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, WinPopupAsset);
                Debug.Log("[Colorful Sort] " + WinPopupAsset + " now says what the level paid" +
                          (retired == null ? "." : ", and its old coin-flight anchor is gone."));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// The booster button, built once and instanced three times in the Game scene — which is
        /// what the blueprint's prefab inventory already plans.
        /// <para>
        /// It used to carry a word instead of an icon, because the pack shipped none. The three icons
        /// exist now, so the prefab gains an `Icon` child and the wiring pass gives each instance the
        /// one that matches its booster (D-068).
        /// </para>
        /// </summary>
        private static void BuildBoosterButton(TMP_FontAsset font, Material textMaterial, UiStyleConfig style)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BoosterButtonAsset) == null)
            {
                CreateBoosterButton(font, textMaterial, style);
            }

            // Both passes run every time, on a prefab this tool has just made and on one three
            // tasks old. Creating and repairing used to be one branch, and the branch is exactly
            // what left an existing button unable to gain anything new (D-068, D-071).
            EnsureBoosterIcon();
            EnsureBoosterBadges(font, textMaterial, style);
        }

        private static void CreateBoosterButton(TMP_FontAsset font, Material textMaterial, UiStyleConfig style)
        {
            Button button = NewButton("BoosterButton", null, "Buttons/square_green", Vector2.zero, new Vector2(220f, 220f));
            RectTransform root = button.image.rectTransform;

            TextMeshProUGUI label = NewLabel("Label", root, "Booster", 40f, font, textMaterial, style.TextFill);
            Stretch(label.rectTransform);

            BoosterButton booster = root.gameObject.AddComponent<BoosterButton>();

            var serialized = new SerializedObject(booster);
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root.gameObject, BoosterButtonAsset);
            Object.DestroyImmediate(root.gameObject);

            Debug.Log("[Colorful Sort] Created " + BoosterButtonAsset + ".");
        }

        /// <summary>
        /// Gives the booster prefab its icon child if it has none. Ensured rather than created, like
        /// the base plate and the glow's material before it: a pass that only dresses what it builds
        /// cannot dress a prefab that already exists, which is how this button stayed blank through
        /// three runs of the tool (D-068).
        /// <para>
        /// A child added to the prefab reaches its instances, so the three boosters in the scene gain
        /// the object here and only their *sprite* is set per instance afterwards.
        /// </para>
        /// </summary>
        private static void EnsureBoosterIcon()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BoosterButtonAsset) == null)
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(BoosterButtonAsset);

            try
            {
                Transform existing = contents.transform.Find(BoosterIconName);
                Image icon = existing == null ? null : existing.GetComponent<Image>();
                bool changed = false;

                if (icon == null)
                {
                    icon = NewImage(BoosterIconName, (RectTransform)contents.transform, null, Image.Type.Simple, false);
                    Place(icon.rectTransform, Vector2.zero, new Vector2(BoosterIconSize, BoosterIconSize));
                    changed = true;
                }

                // The object existing was not enough. It was added carrying no sprite, and an Image
                // with no sprite draws nothing at all — so this method reported success three runs
                // in a row while the three buttons stayed blank on screen (D-070). Ensuring the
                // *content* and not just the object is the same lesson as the base plate and the
                // glow's material; this is the fourth time it has cost a playtest.
                if (icon.sprite == null)
                {
                    Sprite fallback = LoadSprite(BoosterIcons[0].Path);

                    if (fallback != null)
                    {
                        icon.sprite = fallback;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, BoosterButtonAsset);
                Debug.Log("[Colorful Sort] " + BoosterButtonAsset + " now carries an icon wearing " +
                          (icon.sprite == null ? "nothing yet — its sprite is missing from the pack" : icon.sprite.name) +
                          "; the wiring pass gives each booster its own.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Gives the booster prefab its two badges: the red disc that counts what is left, and the
        /// green plus that replaces it at zero. Both live on the prefab and therefore on all three
        /// instances — which one is visible is `BoosterButton`'s to decide at runtime, since it is
        /// the only thing that knows how many charges are left (D-091).
        /// <para>
        /// The red disc is the pack's close shell, as the user asked: it is the same drawing the
        /// reference hangs a number on, and the pack ships no second red disc. The plus already
        /// exists in the HUD set.
        /// </para>
        /// <para>
        /// Ensured, never created-only, for the fifth time in this project: this prefab predates
        /// the badges by four tasks, so a pass that dressed only what it built would leave every
        /// booster countless (D-068, D-071).
        /// </para>
        /// </summary>
        private static void EnsureBoosterBadges(TMP_FontAsset font, Material textMaterial, UiStyleConfig style)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BoosterButtonAsset) == null)
            {
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(BoosterButtonAsset);

            try
            {
                var booster = contents.GetComponent<BoosterButton>();

                if (booster == null)
                {
                    return;
                }

                var root = (RectTransform)contents.transform;
                bool changed = false;

                Image count = Find<Image>(root, CountBadgeName);

                if (count == null)
                {
                    Sprite shell = LoadSprite("Buttons/close_shell_normal");
                    count = NewImage(CountBadgeName, root, shell, SliceFor(shell), false);
                    Place(count.rectTransform, BadgeCorner, new Vector2(BadgeSize, BadgeSize));
                    changed = true;
                }

                TextMeshProUGUI countLabel = Find<TextMeshProUGUI>(count.transform, "Count");

                if (countLabel == null)
                {
                    // The authored "3" is what Prefab Mode shows; Refresh overwrites it with the
                    // real count the moment the bar is bound.
                    countLabel = NewLabel("Count", count.rectTransform, "3", 46f, font, textMaterial, style.TextFill);
                    Stretch(countLabel.rectTransform);
                    changed = true;
                }

                Image plus = Find<Image>(root, PlusBadgeName);

                if (plus == null)
                {
                    plus = NewImage(PlusBadgeName, root, LoadSprite("HUD/plus"), Image.Type.Simple, false);

                    // Off in the prefab, because a booster starts with charges. Refresh turns it
                    // on the moment one runs out, and a prefab that shipped both badges visible
                    // would show a plus over a count for the first frame of every level.
                    plus.gameObject.SetActive(false);
                    changed = true;
                }

                // Every run, not only at creation: the two badges are the same badge wearing two
                // drawings, so the count is the one that gets tuned and the plus follows it
                // (D-092). That direction is the decision — a plus nudged on its own is
                // overwritten here, which is the price of the two never drifting apart.
                changed |= Mirror(count.rectTransform, plus.rectTransform);

                var notes = new List<string>();
                var serialized = new SerializedObject(booster);
                FillIfEmpty(serialized, "icon", Find<Image>(root, BoosterIconName), notes);
                FillIfEmpty(serialized, "countBadge", count.gameObject, notes);
                FillIfEmpty(serialized, "countLabel", countLabel, notes);
                FillIfEmpty(serialized, "plusBadge", plus.gameObject, notes);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (!changed && notes.Count == 0)
                {
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, BoosterButtonAsset);
                Debug.Log("[Colorful Sort] " + BoosterButtonAsset + " now carries its count and plus badges" +
                          (notes.Count == 0 ? "." : ": " + string.Join(", ", notes) + "."));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Copies one rect onto another: anchors, pivot, position, size, scale and rotation. It is
        /// how the plus badge ends up in exactly the place the count badge was tuned into, without
        /// either number being written down anywhere in this tool.
        /// </summary>
        /// <returns>Whether anything actually moved, so a caller can skip a pointless prefab save.</returns>
        private static bool Mirror(RectTransform from, RectTransform to)
        {
            if (from == null || to == null)
            {
                return false;
            }

            bool same = to.anchorMin == from.anchorMin
                        && to.anchorMax == from.anchorMax
                        && to.pivot == from.pivot
                        && to.anchoredPosition == from.anchoredPosition
                        && to.sizeDelta == from.sizeDelta
                        && to.localScale == from.localScale
                        && to.localRotation == from.localRotation;

            if (same)
            {
                return false;
            }

            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
            to.localScale = from.localScale;
            to.localRotation = from.localRotation;
            return true;
        }

        /// <summary>
        /// Puts the right icon on each booster. The sprite is the only per-instance difference — one
        /// prefab, three overrides — and an icon the user has already changed is left alone.
        /// </summary>
        private static void DressBoosters(Transform bar, List<string> notes)
        {
            foreach (BoosterIcon wanted in BoosterIcons)
            {
                DressBooster(bar, wanted, notes);
            }
        }

        private static void DressBooster(Transform bar, BoosterIcon wanted, List<string> notes)
        {
            Transform booster = bar.Find(wanted.Booster);

            if (booster == null)
            {
                notes.Add("no " + wanted.Booster + " under the booster bar, so it has no icon");
                return;
            }

            Transform iconObject = booster.Find(BoosterIconName);

            if (iconObject == null)
            {
                notes.Add(wanted.Booster + " has no icon child yet — run Build UI again once the prefab is saved");
                return;
            }

            var icon = iconObject.GetComponent<Image>();
            Sprite sprite = LoadSprite(wanted.Path);

            if (icon == null || sprite == null || icon.sprite == sprite)
            {
                return;
            }

            // An icon this tool put there is replaceable — the prefab's default is one of the three,
            // and replacing it is how each booster ends up wearing its own. Anything else is a choice
            // somebody made in the editor and it stays, which is the same rule the HUD's layout gets.
            // Filling only an *empty* slot, which is what this used to do, cannot survive the prefab
            // having a sensible default: every booster would keep the default for ever.
            if (icon.sprite != null && !OwnedIcon(icon.sprite))
            {
                notes.Add(wanted.Booster + " keeps " + icon.sprite.name + ", which this tool did not put there");
                return;
            }

            icon.sprite = sprite;
            notes.Add(wanted.Booster + " now wears " + sprite.name);
        }

        /// <summary>
        /// Puts each instance's plus badge exactly where its count badge is. The prefab pass does
        /// the same thing one level up; this one exists because a badge tuned on the *instance*
        /// is an override, and an override the prefab cannot see is the version the player looks
        /// at (D-092).
        /// </summary>
        private static void MirrorBadges(Transform bar, List<string> notes)
        {
            foreach (BoosterIcon booster in BoosterIcons)
            {
                Transform instance = bar.Find(booster.Booster);

                if (instance == null)
                {
                    continue;
                }

                if (Mirror(Find<RectTransform>(instance, CountBadgeName), Find<RectTransform>(instance, PlusBadgeName)))
                {
                    notes.Add(booster.Booster + "'s plus badge now sits exactly where its count badge does");
                }
            }
        }

        /// <summary>
        /// Whether a sprite is one of the three booster icons. Compared by name rather than by
        /// loading each one: the name is the file's own stem, so this asks "is this the shuffle
        /// icon?" without three asset loads and without caring which folder it came from.
        /// </summary>
        private static bool OwnedIcon(Sprite sprite)
        {
            foreach (BoosterIcon owned in BoosterIcons)
            {
                if (sprite.name == owned.Icon)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>One booster and the icon it wears.</summary>
        private readonly struct BoosterIcon
        {
            public BoosterIcon(string booster, string icon)
            {
                Booster = booster;
                Icon = icon;
            }

            /// <summary>The object under the booster bar, which is also the prefab instance's name.</summary>
            public string Booster { get; }

            /// <summary>The sprite's file name under <c>UI/Icons/</c>, which is also the sprite's name.</summary>
            public string Icon { get; }

            /// <summary>Where <see cref="LoadSprite"/> finds it.</summary>
            public string Path
            {
                get { return "Icons/" + Icon; }
            }
        }

        /// <summary>
        /// One round toggle: the settings shell, both state icons stacked on it, and the
        /// component that shows exactly one of them. <paramref name="iconStem"/> is the pair's
        /// shared prefix in the second UI set — `icon_sound` becomes `icon_sound_on_white` and
        /// `icon_sound_off_white`, so the two halves of a state cannot be given different icons
        /// by a typo here.
        /// </summary>
        private static Button ToggleButton(RectTransform row, string name, string iconStem, float x,
            SettingToggleButton.Setting setting)
        {
            Button button = NewButton(name, row, "Settings/settings_shell", new Vector2(x, 0f), new Vector2(170f, 170f));

            Image onIcon = StateIcon(button, name + "_on", iconStem + "_on_white");
            Image offIcon = StateIcon(button, name + "_off", iconStem + "_off_white");

            SettingToggleButton toggle = button.gameObject.AddComponent<SettingToggleButton>();
            var serialized = new SerializedObject(toggle);
            // `intValue`, not `enumValueIndex`: the index is a position in the enum's list and
            // the value is what lands in the prefab. They agree only while the numbers run
            // 0,1,2 — and that enum is explicitly numbered precisely because it may not stay
            // contiguous, so writing the position here would be a trap set for a later edit.
            serialized.FindProperty("setting").intValue = (int)setting;
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("onIcon").objectReferenceValue = onIcon;
            serialized.FindProperty("offIcon").objectReferenceValue = offIcon;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return button;
        }

        private static Image StateIcon(Button button, string name, string iconPath)
        {
            Image icon = NewImage(name, button.image.rectTransform, LoadFmSprite("Icons/" + iconPath), Image.Type.Simple, false);
            Place(icon.rectTransform, Vector2.zero, new Vector2(90f, 90f));
            return icon;
        }

        private static Button WideButton(RectTransform parent, string name, string family, string caption, float y,
            TMP_FontAsset font, Material textMaterial, Color fill)
        {
            Button button = NewButton(name, parent, family, new Vector2(0f, y), new Vector2(700f, 170f));
            TextMeshProUGUI label = NewLabel("Label", button.image.rectTransform, caption, 56f, font, textMaterial, fill);
            Stretch(label.rectTransform);
            return button;
        }

        // ------------------------------------------------------------ scenes

        /// <summary>
        /// Boot gains the persistent popup canvas and the project's single EventSystem. Both
        /// go on a root object, because <c>DontDestroyOnLoad</c> keeps roots only — the same
        /// constraint that put <c>GameRoot</c> on <c>--Systems--</c> itself.
        /// </summary>
        private static void WireBootScene()
        {
            if (!Open(BootScene, out Scene scene))
            {
                return;
            }

            var notes = new List<string>();
            GameObject uiObject = EnsureRoot(scene, "--UI--", notes);

            // Adding the Canvas is what turns this object's Transform into a RectTransform,
            // so the rect is read *after* that call — a Transform captured before it is a
            // destroyed component by the time anything parents to it.
            EnsureCanvas(uiObject, PopupSortingOrder);
            var uiRoot = (RectTransform)uiObject.transform;

            PopupHost host = Require<PopupHost>(uiObject, notes, "PopupHost on --UI--");

            Transform scrimTransform = uiRoot.Find("Scrim");
            Image scrim;

            if (scrimTransform == null)
            {
                // No sprite: a plain tinted rect. It exists to be raycast, which is also what
                // stops a tap on a popup from reaching the board underneath it (D-037).
                scrim = NewImage("Scrim", uiRoot, null, Image.Type.Simple, true);
                scrim.color = new Color(0f, 0f, 0f, 0.55f);
                Stretch(scrim.rectTransform);
                notes.Add("added the scrim");
            }
            else
            {
                scrim = scrimTransform.GetComponent<Image>();
            }

            if (uiRoot.Find("EventSystem") == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(uiRoot, false);
                notes.Add("added the EventSystem (InputSystemUIInputModule — this project has no legacy input backend)");
            }

            var serialized = new SerializedObject(host);
            FillIfEmpty(serialized, "stack", uiRoot, notes);
            FillIfEmpty(serialized, "scrim", scrim, notes);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            RetireCoinFlight(uiRoot, notes);

            Save(scene, BootScene, notes);
        }

        /// <summary>
        /// Takes the coin flight's layer back off Boot. The flight was replaced by a line of text
        /// on the win popup (D-092), and its script is already gone — so this looks the object up
        /// by <em>name</em> rather than by component, which is the only way to find a GameObject
        /// whose script no longer exists.
        /// </summary>
        private static void RetireCoinFlight(RectTransform uiRoot, List<string> notes)
        {
            Transform layer = uiRoot.Find("CoinFlight");

            if (layer == null)
            {
                return;
            }

            Object.DestroyImmediate(layer.gameObject);
            notes.Add("removed the coin flight layer — the win says what it paid instead (D-092)");
        }

        /// <summary>
        /// The Game scene gains its HUD under the <c>--UI--</c> root the bootstrapper already
        /// made. Everything in it hangs off a safe-area panel, so the plaque and the gear stay
        /// clear of a notch on the phones this ships to.
        /// </summary>
        private static void WireGameScene(UiStyleConfig style, TMP_FontAsset font, Material textMaterial)
        {
            if (!Open(GameScene, out Scene scene))
            {
                return;
            }

            var notes = new List<string>();
            GameObject uiObject = EnsureRoot(scene, "--UI--", notes);
            EnsureCanvas(uiObject, HudSortingOrder);
            var uiRoot = (RectTransform)uiObject.transform;

            GameplayHud hud = uiRoot.GetComponentInChildren<GameplayHud>(true);

            if (hud == null)
            {
                hud = NewHudRoot(uiRoot);
                notes.Add("added --UI--/SafeArea/Hud");
            }

            // Part by part, each created only if it is absent. An all-or-nothing build is what
            // left the booster bar unbuilt the first time this ran: the HUD already existed, so
            // everything added to it since was skipped. A tool that cannot repair what an older
            // version of itself made is a tool that only works once.
            EnsureHudParts(hud, style, font, textMaterial, notes);

            // The wiring pass runs whether the HUD was just built or was already there. An
            // existing-but-half-wired HUD used to be unreachable: this method returned the
            // moment it saw one, so the one slot the first run left empty could never be
            // filled by running the tool again. Layout stays yours either way — FillIfEmpty
            // only ever writes into a slot that is empty.
            WireHud(hud, scene, notes);

            Save(scene, GameScene, notes);
        }

        private static GameplayHud NewHudRoot(RectTransform uiRoot)
        {
            RectTransform safeArea = NewRect("SafeArea", uiRoot);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaPanel>();

            RectTransform hudRoot = NewRect("Hud", safeArea);
            Stretch(hudRoot);
            return hudRoot.gameObject.AddComponent<GameplayHud>();
        }

        /// <summary>
        /// Adds whichever of the HUD's parts are missing and leaves the rest exactly as they
        /// are — including wherever the user has since moved them.
        /// </summary>
        private static void EnsureHudParts(GameplayHud hud, UiStyleConfig style, TMP_FontAsset font, Material textMaterial, List<string> notes)
        {
            var hudRoot = (RectTransform)hud.transform;

            if (hudRoot.Find("Plaque") == null)
            {
                // The plaque is decoration, so it is not a raycast target. If it were, it would
                // sit over the board and eat the taps under it (see BoardInput.OverUi).
                Image plaque = NewImage("Plaque", hudRoot, LoadSprite("HUD/hud_pill_9slice"), Image.Type.Sliced, false);
                Anchor(plaque.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -40f), new Vector2(520f, 170f));

                TextMeshProUGUI levelLabel = NewLabel("LevelLabel", plaque.rectTransform, string.Empty, 54f, font, textMaterial, style.TextFill);
                Anchor(levelLabel.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

                TextMeshProUGUI difficultyLabel = NewLabel("DifficultyLabel", plaque.rectTransform, string.Empty, 34f, font, textMaterial, style.TextFill);
                Anchor(difficultyLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

                notes.Add("added the level plaque");
            }

            // The pill used to live here. It belongs to the booster shop now — the one screen
            // where a balance means something — so this hands it over and takes it off the HUD
            // (D-092). Whatever the user tuned travels with it, because the object itself moves.
            MoveCoinPillIntoShop(hudRoot, font, textMaterial, style, notes);

            if (hudRoot.Find("Gear") == null)
            {
                Button gear = NewButton("Gear", hudRoot, "Settings/settings_shell", Vector2.zero, new Vector2(150f, 150f));
                Anchor(gear.image.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-40f, -40f), new Vector2(150f, 150f));
                Image gearIcon = NewImage("Icon", gear.image.rectTransform, LoadSprite("Settings/gear"), Image.Type.Simple, false);
                Place(gearIcon.rectTransform, Vector2.zero, new Vector2(85f, 85f));

                notes.Add("added the gear");
            }

            if (hudRoot.Find("BoosterBar") == null)
            {
                // Along the bottom, as the reference does (§3, left to right: add column, undo,
                // shuffle). The bar itself carries no graphic, so it is not a raycast target and
                // does not swallow taps meant for the board.
                RectTransform bar = NewRect("BoosterBar", hudRoot);
                Anchor(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 60f), new Vector2(760f, 240f));

                SpawnBooster(bar, "AddColumn", BoosterId.AddColumn, "+ Col", -250f);
                SpawnBooster(bar, "Undo", BoosterId.Undo, "Undo", 0f);
                SpawnBooster(bar, "Shuffle", BoosterId.Shuffle, "Shuffle", 250f);

                notes.Add("added the booster bar");
            }

            // Outside the branch on purpose: a bar that already existed needs dressing too, and that
            // is the whole lesson of this pass (D-068).
            Transform existingBar = hudRoot.Find("BoosterBar");

            if (existingBar != null)
            {
                DressBoosters(existingBar, notes);
                MirrorBadges(existingBar, notes);
            }
        }

        /// <summary>
        /// Moves the coin pill out of the gameplay HUD and into the booster shop popup, once.
        /// <para>
        /// A <em>move</em> and not a rebuild, because the one in the scene is the one the user
        /// tuned — its transform, its font, its children. Copying the object carries all of that
        /// across; building a fresh one would quietly throw the tuning away and look like the
        /// tool had worked. If there is nothing to move (a project that never had the HUD pill),
        /// a plain one is built inside the popup instead.
        /// </para>
        /// <para>
        /// It lands on the popup's <em>root</em>, not inside its body panel: the reference hangs
        /// the pill above the panel, over the dimmed screen, and the root is what spans the
        /// screen. Its anchoring comes with it, so it appears where it already was — one
        /// safe-area inset higher, which is a nudge in the prefab and not a formula here.
        /// </para>
        /// </summary>
        private static void MoveCoinPillIntoShop(RectTransform hudRoot, TMP_FontAsset font, Material textMaterial,
            UiStyleConfig style, List<string> notes)
        {
            GameObject shop = AssetDatabase.LoadAssetAtPath<GameObject>(BoosterShopPopupAsset);

            if (shop == null)
            {
                return;
            }

            Transform scenePill = hudRoot.Find("CoinPill");
            GameObject contents = PrefabUtility.LoadPrefabContents(BoosterShopPopupAsset);

            try
            {
                var root = (RectTransform)contents.transform;
                Transform pill = root.Find("CoinPill");
                bool changed = false;

                if (pill == null)
                {
                    if (scenePill != null)
                    {
                        // Instantiate remaps the references inside the copy, so the CoinHud on it
                        // keeps pointing at *its* Amount label rather than at the scene's.
                        GameObject copy = Object.Instantiate(scenePill.gameObject, root, false);
                        copy.name = "CoinPill";
                        pill = copy.transform;
                        notes.Add("moved the tuned coin pill into " + BoosterShopPopupAsset);
                    }
                    else
                    {
                        pill = NewShopCoinPill(root, font, textMaterial, style).transform;
                        notes.Add("built a coin pill inside " + BoosterShopPopupAsset);
                    }

                    changed = true;
                }

                var pillNotes = new List<string>();
                CoinHud hud = WireCoinHud(pill, pillNotes);

                var serialized = new SerializedObject(contents.GetComponent<BoosterShopPopup>());
                FillIfEmpty(serialized, "coinHud", hud, pillNotes);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (changed || pillNotes.Count > 0)
                {
                    notes.AddRange(pillNotes);
                    PrefabUtility.SaveAsPrefabAsset(contents, BoosterShopPopupAsset);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            if (scenePill != null)
            {
                Object.DestroyImmediate(scenePill.gameObject);
                notes.Add("took the coin pill off the gameplay HUD");
            }
        }

        /// <summary>
        /// A coin pill from nothing: the second UI set's toast panel, the pack's coin, and the
        /// balance beside it. Only reached by a project that has no pill to move.
        /// </summary>
        private static Image NewShopCoinPill(RectTransform parent, TMP_FontAsset font, Material textMaterial, UiStyleConfig style)
        {
            Sprite pillSprite = LoadFmSprite("Panels/panel_toast");
            Image pill = NewImage("CoinPill", parent, pillSprite, SliceFor(pillSprite), false);
            Anchor(pill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -300f), new Vector2(340f, 130f));

            Image coin = NewImage("Coin", pill.rectTransform, LoadSprite("HUD/coin"), Image.Type.Simple, false);
            Anchor(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(20f, 0f), new Vector2(110f, 110f));

            TextMeshProUGUI amount = NewLabel("Amount", pill.rectTransform, "0", 52f, font, textMaterial, style.TextFill);
            Place(amount.rectTransform, new Vector2(40f, 0f), new Vector2(220f, 90f));

            pill.gameObject.AddComponent<CoinHud>();
            return pill;
        }

        /// <summary>
        /// Fills the coin pill's own slots: the format it writes a balance with, and the label it
        /// writes into. Found by name, so a pill the user has re-laid-out still wires up.
        /// </summary>
        private static CoinHud WireCoinHud(Transform pill, List<string> notes)
        {
            var hud = pill.GetComponent<CoinHud>();

            if (hud == null)
            {
                hud = pill.gameObject.AddComponent<CoinHud>();
                notes.Add("added CoinHud to the coin pill");
            }

            var serialized = new SerializedObject(hud);
            FillIfEmpty(serialized, "style", AssetDatabase.LoadAssetAtPath<UiStyleConfig>(StyleConfigAsset), notes);
            FillIfEmpty(serialized, "amountLabel", Find<TextMeshProUGUI>(pill, "Amount"), notes);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        private static void SpawnBooster(RectTransform bar, string name, BoosterId kind, string caption, float x)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoosterButtonAsset);

            if (prefab == null)
            {
                Debug.LogError("[Colorful Sort] " + BoosterButtonAsset + " is missing, so the booster bar cannot be built.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, bar);
            instance.name = name;

            Place((RectTransform)instance.transform, new Vector2(x, 0f), new Vector2(220f, 220f));

            var serialized = new SerializedObject(instance.GetComponent<BoosterButton>());
            // `intValue`, not `enumValueIndex`, for the reason the settings toggle already
            // states: the index is a position in the enum's list and the value is what lands in
            // the prefab. `BoosterId` is explicitly numbered precisely because it may not stay
            // contiguous, so writing the position here would be a trap set for a later edit.
            serialized.FindProperty("booster").intValue = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var label = instance.GetComponentInChildren<TextMeshProUGUI>();

            if (label != null)
            {
                label.text = caption;
            }
        }

        /// <summary>
        /// Fills the HUD's empty slots, whatever built it and whenever. The parts are found by
        /// name rather than carried over from the building code, so this works on a HUD that
        /// has been hand-laid-out since — and the style config is re-loaded from disk rather
        /// than passed in, because the reference that reached here across two scene opens is
        /// exactly the one that arrived null once already.
        /// </summary>
        private static void WireHud(GameplayHud hud, Scene scene, List<string> notes)
        {
            Transform plaque = hud.transform.Find("Plaque");

            var serialized = new SerializedObject(hud);
            FillIfEmpty(serialized, "attempt", FindAttemptStarter(scene), notes);
            FillIfEmpty(serialized, "style", AssetDatabase.LoadAssetAtPath<UiStyleConfig>(StyleConfigAsset), notes);
            FillIfEmpty(serialized, "levelLabel", Find<TextMeshProUGUI>(plaque, "LevelLabel"), notes);
            FillIfEmpty(serialized, "difficultyLabel", Find<TextMeshProUGUI>(plaque, "DifficultyLabel"), notes);
            FillIfEmpty(serialized, "gearButton", Find<Button>(hud.transform, "Gear"), notes);
            FillIfEmpty(serialized, "pausePopupPrefab", LoadPopup<PausePopup>(PausePopupAsset), notes);
            FillIfEmpty(serialized, "winPopupPrefab", LoadPopup<WinPopup>(WinPopupAsset), notes);
            FillIfEmpty(serialized, "failPopupPrefab", LoadPopup<FailPopup>(FailPopupAsset), notes);
            FillIfEmpty(serialized, "boosterShopPopupPrefab", LoadPopup<BoosterShopPopup>(BoosterShopPopupAsset), notes);

            Transform bar = hud.transform.Find("BoosterBar");
            FillIfEmpty(serialized, "addColumnButton", Find<BoosterButton>(bar, "AddColumn"), notes);
            FillIfEmpty(serialized, "undoButton", Find<BoosterButton>(bar, "Undo"), notes);
            FillIfEmpty(serialized, "shuffleButton", Find<BoosterButton>(bar, "Shuffle"), notes);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Find<T>(Transform parent, string childName) where T : Component
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(childName);
            return child == null ? null : child.GetComponent<T>();
        }

        /// <summary>
        /// The Menu scene's one button. It is small because it is the whole route: Boot carries
        /// the popup host and the project's single EventSystem, and until something loads the
        /// Game scene over Boot, neither is reachable — which is why the gear did nothing and no
        /// popup could open.
        /// </summary>
        private static void WireMenuScene(UiStyleConfig style, TMP_FontAsset font, Material textMaterial)
        {
            if (!Open(MenuScene, out Scene scene))
            {
                return;
            }

            var notes = new List<string>();
            GameObject uiObject = EnsureRoot(scene, "--UI--", notes);
            EnsureCanvas(uiObject, HudSortingOrder);
            var uiRoot = (RectTransform)uiObject.transform;

            MainMenu menu = uiRoot.GetComponentInChildren<MainMenu>(true);

            if (menu == null)
            {
                menu = BuildMenu(uiRoot, style, font, textMaterial);
                notes.Add("built the menu (one Play button) under --UI--/SafeArea");
            }

            var serialized = new SerializedObject(menu);
            Button play = Find<Button>(menu.transform, "Play");
            FillIfEmpty(serialized, "playButton", play, notes);
            FillIfEmpty(serialized, "style", AssetDatabase.LoadAssetAtPath<UiStyleConfig>(StyleConfigAsset), notes);
            FillIfEmpty(serialized, "database", AssetDatabase.LoadAssetAtPath<LevelDatabase>(LevelDatabaseAsset), notes);

            // The label under the button, not a label called "Play": the button names the level it
            // opens now, so the text is the menu's to write and the name in the hierarchy is the only
            // thing left that still says Play (D-086).
            FillIfEmpty(
                serialized,
                "playLabel",
                play == null ? null : play.GetComponentInChildren<TextMeshProUGUI>(true),
                notes);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            Save(scene, MenuScene, notes);
        }

        private static MainMenu BuildMenu(RectTransform uiRoot, UiStyleConfig style, TMP_FontAsset font, Material textMaterial)
        {
            RectTransform safeArea = NewRect("SafeArea", uiRoot);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaPanel>();

            RectTransform menuRoot = NewRect("Menu", safeArea);
            Stretch(menuRoot);
            MainMenu menu = menuRoot.gameObject.AddComponent<MainMenu>();

            // The pack's big level button. Its text is written by MainMenu from the style config the
            // moment the screen comes up, so what is authored here is only what shows in Prefab Mode
            // before anything has run (D-086).
            Button play = NewButton("Play", menuRoot, "Buttons/level_button", Vector2.zero, new Vector2(760f, 280f));
            TextMeshProUGUI label = NewLabel("Label", play.image.rectTransform, style.MenuNoLevel, 76f, font, textMaterial, style.TextFill);
            Stretch(label.rectTransform);

            return menu;
        }

        private static AttemptStarter FindAttemptStarter(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                AttemptStarter starter = root.GetComponentInChildren<AttemptStarter>(true);

                if (starter != null)
                {
                    return starter;
                }
            }

            return null;
        }

        private static T LoadPopup<T>(string assetPath) where T : Popup
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            return prefab == null ? null : prefab.GetComponent<T>();
        }

        /// <summary>
        /// Puts this project's canvas contract on an object: Overlay, and the scaler settings
        /// `rules/ui.md` fixes (1080×1920, match 0.5). <c>internal</c> rather than private so
        /// the prefab-editing environment is built from the same two numbers every screen
        /// scales by, instead of a copy of them that can drift.
        /// </summary>
        internal static Canvas EnsureCanvas(GameObject target, int sortingOrder)
        {
            Canvas canvas = target.GetComponent<Canvas>();

            if (canvas == null)
            {
                canvas = target.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = target.GetComponent<CanvasScaler>();

            if (scaler == null)
            {
                scaler = target.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (target.GetComponent<GraphicRaycaster>() == null)
            {
                target.AddComponent<GraphicRaycaster>();
            }

            return canvas;
        }

        // ------------------------------------------------------------ building blocks

        private static RectTransform NewRect(string name, Transform parent)
        {
            var created = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)created.transform;

            if (parent != null)
            {
                rect.SetParent(parent, false);
            }

            return rect;
        }

        private static Image NewImage(string name, RectTransform parent, Sprite sprite, Image.Type type, bool raycastTarget)
        {
            RectTransform rect = NewRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static TextMeshProUGUI NewLabel(string name, RectTransform parent, string text, float size,
            TMP_FontAsset font, Material material, Color fill)
        {
            RectTransform rect = NewRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();

            label.font = font;
            label.fontSharedMaterial = material;
            label.text = text;
            label.fontSize = size;
            label.color = fill;
            label.alignment = TextAlignmentOptions.Center;

            // A label is never the thing you press; the button under it is.
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// A button in the art pack's three states. `Sprite Swap` rather than a colour tint is
        /// what rules/ui.md asks for, and it is the only transition that uses the `_pressed`
        /// and `_disabled` files the pack ships.
        /// </summary>
        private static Button NewButton(string name, RectTransform parent, string family, Vector2 position, Vector2 size)
        {
            Sprite normal = LoadSprite(family + "_normal");

            // The pack slices what stretches and draws the rest whole; a `Sliced` image on a
            // borderless sprite renders nothing but a warning. The sprite itself says which
            // one it is, so nothing here has to keep a second list of that.
            Image.Type type = normal != null && normal.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;

            Image image = NewImage(name, parent, normal, type, true);
            Place(image.rectTransform, position, size);

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                pressedSprite = LoadSprite(family + "_pressed"),
                selectedSprite = LoadSprite(family + "_normal"),
                disabledSprite = LoadSprite(family + "_disabled"),
            };

            return button;
        }

        /// <summary>
        /// How a sprite wants to be drawn: sliced when it carries a nine-slice border, whole when
        /// it does not. The sprite is the authority, exactly as it is for a button's shell — a
        /// `Sliced` image on a borderless sprite draws nothing but a console warning, and keeping
        /// a second list of which is which is how those two get out of step.
        /// </summary>
        private static Image.Type SliceFor(Sprite sprite)
        {
            return sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Centre-anchored placement, which is how everything inside a popup is laid out.</summary>
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;

            // A stretched axis takes its size from the parent, so only a fixed one is set.
            if (!Mathf.Approximately(anchorMin.x, anchorMax.x) || !Mathf.Approximately(anchorMin.y, anchorMax.y))
            {
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                return;
            }

            rect.sizeDelta = size;
        }

        private static Sprite LoadSprite(string relativePath)
        {
            return LoadSpriteFrom(UiSprites, relativePath);
        }

        /// <summary>The same load, out of the second UI set (<see cref="FmUiSprites"/>).</summary>
        private static Sprite LoadFmSprite(string relativePath)
        {
            return LoadSpriteFrom(FmUiSprites, relativePath);
        }

        private static Sprite LoadSpriteFrom(string root, string relativePath)
        {
            string path = root + "/" + relativePath + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                Debug.LogError("[Colorful Sort] " + path + " is not a sprite (or is missing). " +
                               "Run Tools > Colorful Sort > Apply Art Import Settings first.");
            }

            return sprite;
        }

        // ------------------------------------------------------------ scene plumbing

        /// <summary>
        /// Leaves Boot open, because Boot is the only scene it makes sense to press Play in:
        /// it carries <c>GameRoot</c>, the popup host and the project's single
        /// <c>EventSystem</c>, and every screen scene is loaded over it.
        /// </summary>
        private static void ReturnToBoot()
        {
            if (SceneManager.GetActiveScene().path == BootScene)
            {
                return;
            }

            EditorSceneManager.OpenScene(BootScene, OpenSceneMode.Single);
            Debug.Log("[Colorful Sort] Reopened " + BootScene + " — press Play here, not in a screen scene.");
        }

        private static bool Open(string scenePath, out Scene scene)
        {
            scene = default;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[Colorful Sort] Cancelled: there are unsaved scene changes.");
                return false;
            }

            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            return scene.IsValid();
        }

        private static void Save(Scene scene, string scenePath, List<string> notes)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Colorful Sort] " + scenePath + ": " +
                      (notes.Count == 0 ? "nothing to do, already wired." : string.Join("; ", notes.ToArray()) + "."));
        }

        /// <summary>
        /// The root object, found or made. It returns the GameObject rather than its
        /// Transform on purpose: adding a Canvas to it replaces that Transform with a
        /// RectTransform, and a caller holding the old one would be holding a destroyed
        /// component.
        /// </summary>
        private static GameObject EnsureRoot(Scene scene, string rootName, List<string> notes)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return root;
                }
            }

            var created = new GameObject(rootName);
            notes.Add("added the " + rootName + " root");
            return created;
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
                notes.Add(propertyName + " left empty (nothing to put in it yet)");
                return;
            }

            property.objectReferenceValue = value;
            notes.Add("wired " + propertyName);
        }

        private static Color Html(string hex, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color colour))
            {
                return Color.magenta;
            }

            colour.a = alpha;
            return colour;
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
