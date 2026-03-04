using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Voxel
{
    /// <summary>Utilities for UI Toolkit panel interaction (e.g. blocking world clicks when over UI).</summary>
    public static class UIPanelUtils
    {
        /// <summary>Returns true if the pointer is over TopPanel or Sidebar, blocking world interaction.</summary>
        public static bool IsPointerOverBlockingUI(UIDocument doc)
        {
            if (doc == null || doc.rootVisualElement?.panel == null) return false;
            if (Mouse.current == null) return false;

            var screenPos = Mouse.current.position.ReadValue();
            screenPos.y = Screen.height - screenPos.y;
            var panelPos = RuntimePanelUtils.ScreenToPanel(doc.rootVisualElement.panel, screenPos);
            var picked = doc.rootVisualElement.panel.Pick(panelPos);
            if (picked == null) return false;

            var topPanel = doc.rootVisualElement.Q("TopPanel");
            var sidebar = doc.rootVisualElement.Q("Sidebar");
            return (topPanel != null && (picked == topPanel || topPanel.Contains(picked))) ||
                   (sidebar != null && (picked == sidebar || sidebar.Contains(picked)));
        }
    }
}
