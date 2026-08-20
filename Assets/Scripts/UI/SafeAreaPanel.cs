using UnityEngine;

namespace ColorfulSort.UI
{
    /// <summary>
    /// Pins a <c>RectTransform</c> to <see cref="Screen.safeArea"/>, so nothing this panel
    /// holds ends up under a notch, a punch-hole or a gesture bar.
    /// <para>
    /// It exists because <c>rules/ui.md</c> refuses the alternative: positioning UI by
    /// trusting where a background image happens to put it. The background is authored at
    /// 1080×1920 and a 19.5:9 or 20:9 phone crops it, so a button placed "where the art
    /// says" drifts off the panel on exactly the devices this game ships to.
    /// </para>
    /// <para>
    /// It does not poll. The safe area changes when the screen does, and the engine already
    /// says when that happened: <c>OnRectTransformDimensionsChange</c> fires on a resolution
    /// or orientation change, and <c>OnEnable</c> covers the first frame. Reading
    /// <c>Screen.safeArea</c> in <c>Update</c> would be an engine query at frame frequency
    /// for a value that changes approximately never.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        private RectTransform panel;

        private Rect appliedArea;

        private int appliedWidth;

        private int appliedHeight;

        private void OnEnable()
        {
            panel = (RectTransform)transform;
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            // Fires while the object is being built too, before OnEnable has run.
            if (panel == null)
            {
                return;
            }

            Apply();
        }

        private void Apply()
        {
            int width = Screen.width;
            int height = Screen.height;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            Rect area = Screen.safeArea;

            // Writing the anchors changes this rect's dimensions, which calls back into
            // OnRectTransformDimensionsChange. This comparison is what stops that from being
            // an infinite loop: the second pass finds nothing to change and leaves.
            if (area == appliedArea && width == appliedWidth && height == appliedHeight)
            {
                return;
            }

            appliedArea = area;
            appliedWidth = width;
            appliedHeight = height;

            // Anchors are fractions of the parent, so the panel keeps following the safe area
            // without anything having to run again on the next resolution.
            Vector2 min = new Vector2(area.xMin / width, area.yMin / height);
            Vector2 max = new Vector2(area.xMax / width, area.yMax / height);

            panel.anchorMin = min;
            panel.anchorMax = max;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }
    }
}
