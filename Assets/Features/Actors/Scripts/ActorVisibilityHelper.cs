using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// Helper for actor visibility (show/hide renderers based on state).
    /// Extracted from ActorBehavior.
    /// </summary>
    public static class ActorVisibilityHelper
    {
        public static void UpdateVisibility(Renderer[] renderers, bool visible)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r != null && r.enabled != visible)
                    r.enabled = visible;
            }
        }
    }
}
