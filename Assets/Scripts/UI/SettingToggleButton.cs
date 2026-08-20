using ColorfulSort.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ColorfulSort.UI
{
    /// <summary>
    /// One round on/off button — sound, music or vibration — as the Pause and Settings popups
    /// both show it. One component for every setting and both popups, which is why
    /// <c>rules/ui.md</c> asks for it to be shared rather than written twice.
    /// <para>
    /// It holds no state. The flag lives in the save file and its single writer is
    /// <c>GameRoot</c> (fingerprint.md → Data authorities); this button sends a write and
    /// then re-reads the property it just set. That is deliberate: a local <c>bool</c>
    /// mirroring the setting would be a second copy of it, and two copies of one flag is how
    /// a settings screen ends up disagreeing with the game.
    /// </para>
    /// <para>
    /// The state is drawn as a <em>pair of icons</em>, one per state, because the art pack
    /// ships the crossed-out variant as its own sprite. The earlier design drew one icon and
    /// laid a diagonal bar over it, which made "off" a composite of two graphics that had to
    /// line up; a second sprite cannot misalign with itself.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingToggleButton : MonoBehaviour
    {
        /// <summary>
        /// Which persisted toggle this button drives. The numbers are explicit and only ever
        /// appended to: they are serialized in every popup prefab, so inserting a value in the
        /// middle would silently turn one existing button into another setting's button.
        /// </summary>
        public enum Setting
        {
            Sound = 0,
            Vibration = 1,
            Music = 2,
        }

        [SerializeField]
        private Setting setting;

        [SerializeField]
        private Button button;

        [Tooltip("The icon shown while the setting is on.")]
        [SerializeField]
        private Image onIcon;

        [Tooltip("The crossed-out icon shown while the setting is off.")]
        [SerializeField]
        private Image offIcon;

        private bool reportedMissingRoot;

        private void OnEnable()
        {
            if (button == null || onIcon == null || offIcon == null)
            {
                Debug.LogError("[UI] " + name + " needs a button and both state icons; assign On Icon and Off Icon.", this);
                return;
            }

            button.onClick.AddListener(Toggle);
            Render();
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(Toggle);
            }
        }

        private void Toggle()
        {
            GameRoot root = Root();

            if (root == null)
            {
                return;
            }

            switch (setting)
            {
                case Setting.Sound:
                    root.SoundOn = !root.SoundOn;
                    break;

                case Setting.Vibration:
                    root.VibrationOn = !root.VibrationOn;
                    break;

                case Setting.Music:
                    root.MusicOn = !root.MusicOn;
                    break;
            }

            Render();
        }

        private void Render()
        {
            GameRoot root = Root();

            if (root == null)
            {
                return;
            }

            // Two `enabled` flips rather than two `SetActive` calls: this runs on every click
            // and on every open, and disabling a graphic costs no hierarchy or layout work.
            bool on = IsOn(root);
            onIcon.enabled = on;
            offIcon.enabled = !on;
        }

        private bool IsOn(GameRoot root)
        {
            switch (setting)
            {
                case Setting.Vibration:
                    return root.VibrationOn;

                case Setting.Music:
                    return root.MusicOn;

                default:
                    return root.SoundOn;
            }
        }

        /// <summary>
        /// The composition root, or null with one explanation. Null means the Game scene was
        /// entered directly instead of through Boot — normal while working in the editor, and
        /// worth saying once rather than on every click.
        /// </summary>
        private GameRoot Root()
        {
            GameRoot root = GameRoot.Instance;

            if (root == null && !reportedMissingRoot)
            {
                reportedMissingRoot = true;
                Debug.LogWarning("[UI] " + name + " cannot read its setting: there is no GameRoot, " +
                                 "so this scene was entered without Boot. Play from Boot to reach the save file.", this);
            }

            return root;
        }
    }
}
