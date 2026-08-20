using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ColorfulSort.View
{
    /// <summary>
    /// Turns a press into an event and nothing else. It does not know what a column is, does not
    /// hold a camera and does not decide whether a tap counts — that is <see cref="BoardView"/>'s,
    /// which owns the board's geometry and its state.
    /// <para>
    /// <c>Pointer.current</c> covers mouse and touch with one path, which is what the project
    /// needs: the editor is played with a mouse and the phone is not. Per frame this is one null
    /// check and one boolean; the position is only read on the frame something is pressed.
    /// </para>
    /// <para>
    /// A press that lands on UI is not a board press. The gear sits over the board and every
    /// popup covers it, so without this the tap that opens Pause also lifts a brick behind it.
    /// Asking <see cref="EventSystem"/> is not this system reaching into <c>UI</c> — the arrow
    /// in the blueprint is about <c>ColorfulSort.UI</c>, and nothing here references a type from
    /// it; <c>EventSystem</c> is the engine's input router, in the same category as
    /// <c>Camera</c>. It is a judgment call all the same, so it is recorded (D-037).
    /// </para>
    /// <para>
    /// The question is asked as a raycast at the exact press position rather than with
    /// <c>IsPointerOverGameObject()</c>, which answers for whichever pointer the UI module last
    /// processed and is documented as needing a pointer id to be right about touch. This form
    /// has no pointer identity to get wrong and no frame-ordering to lose against: it asks
    /// whether a raycastable graphic is at this point, which is precisely the question. One
    /// raycast per press is event frequency, and the buffers are fields so it allocates nothing.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardInput : MonoBehaviour
    {
        /// <summary>A press, in screen coordinates.</summary>
        public event Action<Vector2> Pressed;

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private PointerEventData uiProbe;

        private void Update()
        {
            Pointer pointer = Pointer.current;

            if (pointer == null || !pointer.press.wasPressedThisFrame)
            {
                return;
            }

            Vector2 position = pointer.position.ReadValue();

            if (OverUi(position))
            {
                return;
            }

            Action<Vector2> pressed = Pressed;

            if (pressed != null)
            {
                pressed(position);
            }
        }

        /// <summary>
        /// Whether any raycast-target graphic covers this point. Note what that makes the HUD
        /// responsible for: a full-screen panel left as a raycast target would swallow every
        /// tap on the board. Only the things meant to be pressed are targets.
        /// </summary>
        private bool OverUi(Vector2 screenPosition)
        {
            EventSystem events = EventSystem.current;

            if (events == null)
            {
                // No EventSystem means no UI is listening at all, so nothing can be covering
                // the board. Normal when the Game scene is entered without Boot.
                return false;
            }

            if (uiProbe == null)
            {
                uiProbe = new PointerEventData(events);
            }

            uiProbe.position = screenPosition;
            uiHits.Clear();
            events.RaycastAll(uiProbe, uiHits);

            return uiHits.Count > 0;
        }
    }
}
