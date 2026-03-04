using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Voxel
{
    /// <summary>
    /// Populates and toggles the Hotkeys panel from HotkeyManager bindings. No hardcoded key list.
    /// </summary>
    public class HotkeysPanelController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private VisualElement _hotkeysPanel;
        private ScrollView _hotkeysPanelList;
        private Func<bool> _escapeHandler;
        private Func<bool> _f1Handler;
        private Func<bool> _hHandler;

        private void Start()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument?.rootVisualElement == null) return;

            _hotkeysPanel = uiDocument.rootVisualElement.Q<VisualElement>("HotkeysPanel");
            _hotkeysPanelList = uiDocument.rootVisualElement.Q<ScrollView>("HotkeysPanelList");

            var hotkeysButton = uiDocument.rootVisualElement.Q<Button>("Hotkeys");
            if (hotkeysButton != null)
                hotkeysButton.clicked += ToggleHotkeysPanel;

            if (HotkeyManager.Instance != null)
            {
                _escapeHandler = TryCloseHotkeysPanel;
                HotkeyManager.Instance.Register(Key.Escape, "ESC", "Close hotkeys panel", null, _escapeHandler, HotkeyManager.PriorityUiOverlay + 1);

                _f1Handler = TryOpenHotkeysPanel;
                HotkeyManager.Instance.Register(Key.F1, "F1", "Show hotkeys", null, _f1Handler);

                _hHandler = ToggleHotkeysPanelHandler;
                HotkeyManager.Instance.Register(Key.H, "H", "Toggle hotkeys panel", null, _hHandler);
            }
        }

        private bool TryCloseHotkeysPanel()
        {
            if (_hotkeysPanel == null || _hotkeysPanel.ClassListContains("hidden"))
                return false;
            _hotkeysPanel.AddToClassList("hidden");
            return true;
        }

        private bool TryOpenHotkeysPanel()
        {
            if (_hotkeysPanel == null || !_hotkeysPanel.ClassListContains("hidden"))
                return false;
            _hotkeysPanel.RemoveFromClassList("hidden");
            RefreshBindings();
            return true;
        }

        private void OnDestroy()
        {
            if (_escapeHandler != null)
                HotkeyManager.Instance?.Unregister(_escapeHandler);
            if (_f1Handler != null)
                HotkeyManager.Instance?.Unregister(_f1Handler);
            if (_hHandler != null)
                HotkeyManager.Instance?.Unregister(_hHandler);
        }

        private bool ToggleHotkeysPanelHandler()
        {
            ToggleHotkeysPanel();
            return true;
        }

        private void ToggleHotkeysPanel()
        {
            if (_hotkeysPanel == null) return;
            bool isHidden = _hotkeysPanel.ClassListContains("hidden");
            if (isHidden)
            {
                _hotkeysPanel.RemoveFromClassList("hidden");
                RefreshBindings();
            }
            else
            {
                _hotkeysPanel.AddToClassList("hidden");
            }
        }

        private void RefreshBindings()
        {
            if (_hotkeysPanelList == null || HotkeyManager.Instance == null) return;

            _hotkeysPanelList.Clear();
            IReadOnlyList<HotkeyDisplayInfo> bindings = HotkeyManager.Instance.GetAllBindings();

            foreach (var b in bindings)
            {
                var row = new VisualElement();
                row.AddToClassList("hotkey-row");

                var badge = new Label(b.DisplayKey);
                badge.AddToClassList("hotkey-badge");
                row.Add(badge);

                var desc = new Label(b.Description);
                desc.AddToClassList("hotkey-description");
                row.Add(desc);

                if (!string.IsNullOrEmpty(b.Context))
                {
                    var ctx = new Label($"({b.Context})");
                    ctx.AddToClassList("hotkey-context");
                    row.Add(ctx);
                }

                _hotkeysPanelList.Add(row);
            }
        }
    }
}
