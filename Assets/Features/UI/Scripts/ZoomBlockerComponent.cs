using UnityEngine;
using UnityEngine.UIElements;

namespace Voxel
{
    /// <summary>
    /// Implements IZoomBlocker using UIPanelUtils. Add to the same GameObject as UIDocument.
    /// Assign to TopDownCamera's zoomBlocker field to disable world zoom when scrolling over MessageLog or overlay panels.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ZoomBlockerComponent : MonoBehaviour, IZoomBlocker
    {
        [SerializeField] private UIDocument uiDocument;

        private UIDocument _cachedDoc;

        public bool ShouldBlockZoom()
        {
            var doc = uiDocument != null ? uiDocument : _cachedDoc;
            if (doc == null)
            {
                doc = GetComponent<UIDocument>();
                _cachedDoc = doc;
            }
            return doc != null && UIPanelUtils.IsPointerOverZoomBlockingUI(doc);
        }
    }
}
