using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Voxel
{
    /// <summary>
    /// Centralized manager for exclusive overlay panels. Only one panel can be visible at a time.
    /// Opening a panel closes any other. Extensible for future UI panels and hotkey integration.
    /// </summary>
    public class PanelManager : MonoBehaviour
    {
        public static PanelManager Instance { get; private set; }

        public const string PanelInventory = "inventory";
        public const string PanelHotkeys = "hotkeys";
        public const string PanelLog = "log";

        private readonly Dictionary<string, PanelEntry> _panels = new();

        private struct PanelEntry
        {
            public VisualElement Element;
            public Action OnShow;
            public Action OnHide;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Register a panel. onShow/onHide are optional callbacks (e.g. for refresh).</summary>
        public void RegisterPanel(string id, VisualElement element, Action onShow = null, Action onHide = null)
        {
            if (string.IsNullOrEmpty(id) || element == null) return;
            _panels[id] = new PanelEntry { Element = element, OnShow = onShow, OnHide = onHide };
        }

        /// <summary>Opens the panel and closes all others.</summary>
        public void OpenPanel(string id)
        {
            if (!_panels.TryGetValue(id, out var entry)) return;

            foreach (var kvp in _panels)
            {
                var el = kvp.Value.Element;
                if (el == null) continue;
                if (kvp.Key == id)
                {
                    el.RemoveFromClassList("hidden");
                    entry.OnShow?.Invoke();
                }
                else
                {
                    if (!el.ClassListContains("hidden"))
                        kvp.Value.OnHide?.Invoke();
                    el.AddToClassList("hidden");
                }
            }
        }

        /// <summary>Closes the panel.</summary>
        public void ClosePanel(string id)
        {
            if (!_panels.TryGetValue(id, out var entry)) return;
            if (entry.Element == null) return;
            if (!entry.Element.ClassListContains("hidden"))
                entry.OnHide?.Invoke();
            entry.Element.AddToClassList("hidden");
        }

        /// <summary>Toggles the panel: if visible, close; else close others and open.</summary>
        public void TogglePanel(string id)
        {
            if (IsPanelOpen(id))
                ClosePanel(id);
            else
                OpenPanel(id);
        }

        /// <summary>Returns true if the panel is visible.</summary>
        public bool IsPanelOpen(string id)
        {
            if (!_panels.TryGetValue(id, out var entry) || entry.Element == null)
                return false;
            return !entry.Element.ClassListContains("hidden");
        }

        /// <summary>Closes all panels. Returns true if any was open.</summary>
        public bool CloseAll()
        {
            bool anyOpen = false;
            foreach (var kvp in _panels)
            {
                var el = kvp.Value.Element;
                if (el == null) continue;
                if (!el.ClassListContains("hidden"))
                {
                    anyOpen = true;
                    kvp.Value.OnHide?.Invoke();
                }
                el.AddToClassList("hidden");
            }
            return anyOpen;
        }
    }
}
