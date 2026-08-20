using System;
using UnityEngine;

namespace ColorfulSort.UI
{
    /// <summary>
    /// What every popup has in common: it can ask to be closed, and it is told when it goes
    /// up and when it comes down. Nothing more — a popup does not destroy itself, does not
    /// decide what is on top of it and does not know whether anything else is open.
    /// <para>
    /// A base class rather than an interface, and a base class rather than four unrelated
    /// components, because <c>abstraction-level.md</c> asks for the count: the blueprint
    /// names four popups (Pause, Settings, Win, Fail), so the repetition is real and already
    /// planned. It stays this thin for the same reason — there is no evidence yet for
    /// animation hooks, results, or a return value, so none are here.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public abstract class Popup : MonoBehaviour
    {
        /// <summary>
        /// Raised when this popup wants to go away. The host is the only subscriber, and the
        /// host is what actually takes it down: a popup that destroyed itself would leave the
        /// stack holding a dead entry, which is the bug this split exists to prevent.
        /// </summary>
        public event Action<Popup> CloseRequested;

        /// <summary>Ask to be closed. Wired to the close button, and to whatever else means "done".</summary>
        public void Close()
        {
            Action<Popup> requested = CloseRequested;

            if (requested != null)
            {
                requested(this);
                return;
            }

            // No host means this popup was placed in a scene by hand rather than opened.
            // Saying so is better than silently doing nothing to a button the player pressed.
            Debug.LogWarning("[UI] " + name + " was closed but nothing is hosting it, so it stays on screen.", this);
        }

        /// <summary>Called by the host once the popup is on screen.</summary>
        protected internal virtual void OnOpened()
        {
        }

        /// <summary>Called by the host immediately before the popup is destroyed.</summary>
        protected internal virtual void OnClosing()
        {
        }
    }
}
