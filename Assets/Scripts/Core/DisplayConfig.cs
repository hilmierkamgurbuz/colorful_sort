using UnityEngine;

namespace ColorfulSort.Core
{
    /// <summary>
    /// What the app asks of the display. One number so far: how many frames a second the game is
    /// allowed to draw.
    /// <para>
    /// It is an asset under <c>Data/Config/</c> rather than a constant in <see cref="GameRoot"/>,
    /// because a frame-rate cap is the most ordinary performance dial there is and
    /// <c>.claude/rules/data.md</c> is explicit: "a number a designer might want to change lives
    /// here, not in a serialized field default". The alternative — a `const` at the boot site — was
    /// one line and needed no wiring, and was refused because the no-numbers-in-code invariant holds
    /// in every phase (D-100).
    /// </para>
    /// <para>
    /// Unity offers no project setting for this: <c>Application.targetFrameRate</c> is code-only, so
    /// *somewhere* has to say the number out loud. This is that somewhere, and it is data.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "DisplayConfig", menuName = "Colorful Sort/Display Config")]
    public sealed class DisplayConfig : ScriptableObject
    {
        /// <summary>
        /// What <c>Application.targetFrameRate</c> means when nobody has set it. Unity's own default
        /// is -1, "the platform's own rate", which on mobile is 30 — so leaving this alone is not a
        /// neutral choice, it is a choice of 30.
        /// </summary>
        public const int PlatformDefault = -1;

        [Tooltip("Frames per second the game may draw. A CEILING, not a promise: on a 60 Hz screen 120 still gives 60, because the display's refresh rate is the real limit.")]
        [SerializeField]
        private int targetFrameRate;

        /// <summary>
        /// The ceiling handed to <c>Application.targetFrameRate</c>, or
        /// <see cref="PlatformDefault"/> when this asset leaves it unauthored.
        /// <para>
        /// A ceiling and not a target: asking for 120 on a 60 Hz phone yields 60. What it does buy on
        /// a 120 Hz phone is the frames; what it costs is battery and heat, which is why it is a
        /// number somebody can turn down without touching code.
        /// </para>
        /// </summary>
        public int TargetFrameRate => targetFrameRate <= 0 ? PlatformDefault : targetFrameRate;

        /// <summary>
        /// Whether this asset actually asks for anything. False means "leave the platform alone",
        /// which is a legal authoring choice and the reason <see cref="Validate"/> does not refuse
        /// zero.
        /// </summary>
        public bool CapsFrameRate => targetFrameRate > 0;

        /// <summary>
        /// Zero is deliberately legal — it means "leave the platform's own rate alone", which is what
        /// an asset written before this field existed carries and a look nobody can be surprised by.
        /// A *negative* rate is refused, because it is neither a cap nor a hand-off: it would reach
        /// the engine as an arbitrary number and mean nothing.
        /// </summary>
        public bool Validate(out string error)
        {
            if (targetFrameRate < 0)
            {
                error = "The target frame rate is " + targetFrameRate +
                        "; it is a positive ceiling, or 0 to leave the platform's own rate alone.";
                return false;
            }

            error = null;
            return true;
        }

        private void OnValidate()
        {
            string error;

            if (!Validate(out error))
            {
                Debug.LogWarning("[" + name + "] " + error, this);
            }
        }
    }
}
