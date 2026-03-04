namespace Voxel
{
    /// <summary>
    /// Implemented by components that can block world zoom when the pointer is over UI (e.g. MessageLog, overlay panels).
    /// Used by TopDownCamera to avoid zooming when scrolling over scrollable UI.
    /// </summary>
    public interface IZoomBlocker
    {
        bool ShouldBlockZoom();
    }
}
