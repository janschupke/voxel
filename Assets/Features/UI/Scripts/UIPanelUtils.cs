using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Voxel
{
    /// <summary>Utilities for UI Toolkit panel interaction (e.g. blocking world clicks when over UI).</summary>
    public static class UIPanelUtils
    {
        private const string BlocksZoomClass = "blocks-zoom";

        /// <summary>Returns true if the pointer is over TopPanel, Sidebar, or a visible overlay panel (Menu, Inventory, Hotkeys, Log), blocking world interaction.</summary>
        public static bool IsPointerOverBlockingUI(UIDocument doc)
        {
            if (doc == null || doc.rootVisualElement?.panel == null) return false;
            if (Mouse.current == null) return false;

            var screenPos = Mouse.current.position.ReadValue();
            screenPos.y = Screen.height - screenPos.y;
            var panelPos = RuntimePanelUtils.ScreenToPanel(doc.rootVisualElement.panel, screenPos);
            var picked = doc.rootVisualElement.panel.Pick(panelPos);
            if (picked == null) return false;

            for (var el = picked; el != null; el = el.parent)
            {
                if (el.name == "Locate" || el.name == "SelectionDetail")
                    return true;
            }

            var topPanel = doc.rootVisualElement.Q("TopPanel");
            var sidebar = doc.rootVisualElement.Q("Sidebar");
            var menuPanel = doc.rootVisualElement.Q("MenuPanel");
            var inventoryPanel = doc.rootVisualElement.Q("InventoryPanel");
            var hotkeysPanel = doc.rootVisualElement.Q("HotkeysPanel");
            var logPanel = doc.rootVisualElement.Q("LogPanel");
            return (topPanel != null && (picked == topPanel || topPanel.Contains(picked))) ||
                   (sidebar != null && (picked == sidebar || sidebar.Contains(picked))) ||
                   (menuPanel != null && !menuPanel.ClassListContains("hidden") &&
                    (picked == menuPanel || menuPanel.Contains(picked))) ||
                   (inventoryPanel != null && !inventoryPanel.ClassListContains("hidden") &&
                    (picked == inventoryPanel || inventoryPanel.Contains(picked))) ||
                   (hotkeysPanel != null && !hotkeysPanel.ClassListContains("hidden") &&
                    (picked == hotkeysPanel || hotkeysPanel.Contains(picked))) ||
                   (logPanel != null && !logPanel.ClassListContains("hidden") &&
                    (picked == logPanel || logPanel.Contains(picked)));
        }

        /// <summary>Returns true if the pointer is over UI that should block world zoom (e.g. MessageLog, overlay panels). Uses USS class "blocks-zoom".</summary>
        public static bool IsPointerOverZoomBlockingUI(UIDocument doc)
        {
            if (doc == null || doc.rootVisualElement?.panel == null) return false;
            if (Mouse.current == null) return false;

            var screenPos = Mouse.current.position.ReadValue();
            screenPos.y = Screen.height - screenPos.y;
            var panelPos = RuntimePanelUtils.ScreenToPanel(doc.rootVisualElement.panel, screenPos);
            var picked = doc.rootVisualElement.panel.Pick(panelPos);
            if (picked == null) return false;

            for (var el = picked; el != null; el = el.parent)
            {
                if (el.ClassListContains(BlocksZoomClass))
                    return true;
            }
            return false;
        }

        /// <summary>Shows or hides elements with class "debug-only" based on show flag.</summary>
        public static void UpdateDebugControlsVisibility(VisualElement root, bool show)
        {
            if (root == null) return;
            foreach (var el in root.Query(className: "debug-only").ToList())
            {
                if (show)
                    el.RemoveFromClassList("hidden");
                else
                    el.AddToClassList("hidden");
            }
        }
    }
}
