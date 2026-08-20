using UnityEngine;

namespace ColorfulSort.Core
{
    /// <summary>
    /// The composition root, on the persistent <c>--Systems--</c> object in the Boot
    /// scene. It constructs the services, hands them what they need, and survives every
    /// scene load; <c>DontDestroyOnLoad</c> only works on a root object, which is why the
    /// component sits on the root itself rather than on a child of it.
    /// <para>
    /// One static entry point, and no registry behind it: <c>UI</c>, <c>Meta</c> and
    /// <c>BoardView</c> are the three consumers the blueprint names, and they reach a
    /// service as <c>GameRoot.Instance.Save</c>. No interfaces, no service dictionary,
    /// no runtime lookups — every reference here is constructed or serialized.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        [Tooltip("What the app asks of the display, from Data/Config/. Assigned by Tools > Colorful Sort > Bootstrap Scenes.")]
        [SerializeField]
        private DisplayConfig display;

        /// <summary>The live root. Null only before Boot's first frame.</summary>
        public static GameRoot Instance { get; private set; }

        /// <summary>The save file's only writer.</summary>
        public SaveService Save { get; private set; }

        /// <summary>Screen scene loading. Boot stays put; Menu and Game swap over it.</summary>
        public SceneFlowService Scenes { get; private set; }

        /// <summary>
        /// Sound, persisted. The toggle is <c>Core</c>'s data (fingerprint.md → Data
        /// authorities), so the setter lives here and not in the Settings popup that
        /// flips it; the audio service that will read it arrives with the first clip.
        /// </summary>
        public bool SoundOn
        {
            get => Save.Data.soundOn;
            set => SetToggle(ref Save.Data.soundOn, value);
        }

        /// <summary>Vibration, persisted. Same ownership as <see cref="SoundOn"/>.</summary>
        public bool VibrationOn
        {
            get => Save.Data.vibrationOn;
            set => SetToggle(ref Save.Data.vibrationOn, value);
        }

        /// <summary>
        /// Music, persisted. Its own flag rather than a second meaning for
        /// <see cref="SoundOn"/>, because the Pause popup offers them as two buttons and a
        /// player who mutes the music keeps the taps audible. Same ownership as the other two.
        /// </summary>
        public bool MusicOn
        {
            get => Save.Data.musicOn;
            set => SetToggle(ref Save.Data.musicOn, value);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A second Boot was loaded (an editor mistake, or Boot entered twice).
                // The first root keeps the save file; this one leaves quietly.
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            ApplyDisplay();

            Save = new SaveService(Application.persistentDataPath);
            SaveLoadOutcome outcome = Save.Load();

            Scenes = new SceneFlowService();

            Debug.Log("[Core] Boot: save " + outcome + " at " + Save.FilePath);
        }

        /// <summary>
        /// Asks the display for the frame rate the config authored. Runs before the save loads,
        /// because it is the cheapest thing here and the first frame is drawn either way.
        /// <para>
        /// The cap is a ceiling, not a promise: the display's refresh rate is the real limit, so 120
        /// on a 60 Hz phone is 60. It is worth saying out loud in the log for exactly that reason —
        /// "I asked for 120" and "you are getting 120" are different sentences, and only the profiler
        /// can say the second one.
        /// </para>
        /// <para>
        /// A missing config is the half-wired project rather than a state to design for, and it is
        /// said out loud instead of defaulted: leaving <c>targetFrameRate</c> alone is not neutral,
        /// it is a choice of the platform's own rate, which on mobile is 30 (D-100).
        /// </para>
        /// </summary>
        private void ApplyDisplay()
        {
            if (display == null)
            {
                Debug.LogError(
                    "[Core] " + name + " has no display config, so the frame rate is whatever the platform " +
                    "picks — 30 fps on mobile. Assign it with Tools > Colorful Sort > Bootstrap Scenes.",
                    this);
                return;
            }

            string error;

            if (!display.Validate(out error))
            {
                Debug.LogError("[Core] " + display.name + " is not usable, so the frame rate is left alone: " + error, display);
                return;
            }

            if (!display.CapsFrameRate)
            {
                Debug.Log("[Core] " + display.name + " asks for no frame-rate cap; the platform's own rate stands.");
                return;
            }

            Application.targetFrameRate = display.TargetFrameRate;

            Debug.Log("[Core] Frame rate capped at " + display.TargetFrameRate +
                      " fps. It is a ceiling: a 60 Hz screen still gives 60.");
        }

        private void Start()
        {
            Scenes.ShowMenu();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save.Flush();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            // On mobile, losing focus is the last reliable moment before the process can
            // be killed without another callback.
            if (!focused)
            {
                Save.Flush();
            }
        }

        private void OnApplicationQuit()
        {
            Save.Flush();
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Save.Flush();
            Instance = null;
        }

        private void SetToggle(ref bool field, bool value)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Save.MarkDirty();
        }
    }
}
