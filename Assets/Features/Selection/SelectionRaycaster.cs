using UnityEngine;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Raycasts to find selectable placed objects. Extracted from SelectionController.
    /// </summary>
    public class SelectionRaycaster
    {
        private const int RaycastBufferSize = 32;
        private readonly RaycastHit[] _raycastBuffer = new RaycastHit[RaycastBufferSize];

        private readonly WorldBootstrap _worldBootstrap;
        private readonly IPlacedObjectRegistry _registry;

        public SelectionRaycaster(WorldBootstrap worldBootstrap, IPlacedObjectRegistry registry)
        {
            _worldBootstrap = worldBootstrap;
            _registry = registry;
        }

        public bool TryGetSelectableAtRay(Ray ray, out Transform hitTransform, out string entryName)
        {
            hitTransform = null;
            entryName = null;
            float closestDistance = float.MaxValue;

            int hitCount = Physics.RaycastNonAlloc(ray, _raycastBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _raycastBuffer[i];
                if (hit.distance < closestDistance && hit.distance > 0 &&
                    IsSelectablePlacedObject(hit.transform, out string name))
                {
                    closestDistance = hit.distance;
                    hitTransform = GetRootPlacedObject(hit.transform);
                    entryName = name;
                }
            }

            if (_worldBootstrap != null && _registry?.Entries != null)
            {
                foreach (var entry in _registry.Entries)
                {
                    if (entry == null || !entry.IsSelectable) continue;
                    var parent = _worldBootstrap.GetParentByEntryName(entry.Name);
                    if (parent == null) continue;

                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var child = parent.GetChild(i);
                        var bounds = GetBounds(child);
                        if (bounds.HasValue && bounds.Value.IntersectRay(ray, out float distance) &&
                            distance > 0 && distance < closestDistance)
                        {
                            closestDistance = distance;
                            hitTransform = child;
                            entryName = entry.Name;
                        }
                    }
                }
            }

            return hitTransform != null;
        }

        private Transform GetRootPlacedObject(Transform t)
        {
            if (t == null || _worldBootstrap == null || _registry?.Entries == null) return null;
            Transform root = t;
            while (t != null)
            {
                if (_registry.Entries != null)
                {
                    foreach (var entry in _registry.Entries)
                    {
                        if (entry == null) continue;
                        var parent = _worldBootstrap.GetParentByEntryName(entry.Name);
                        if (t.parent == parent)
                        {
                            root = t;
                            break;
                        }
                    }
                }
                t = t.parent;
            }
            return root;
        }

        private static Bounds? GetBounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return null;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private bool IsSelectablePlacedObject(Transform t, out string entryName)
        {
            entryName = null;
            if (t == null || _worldBootstrap == null || _registry?.Entries == null) return false;

            Transform parent = t.parent;
            while (parent != null)
            {
                if (_registry.Entries != null)
                {
                    foreach (var entry in _registry.Entries)
                    {
                        if (entry == null || !entry.IsSelectable) continue;
                        if (_worldBootstrap.GetParentByEntryName(entry.Name) == parent)
                        {
                            entryName = entry.Name;
                            return true;
                        }
                    }
                }
                parent = parent.parent;
            }
            return false;
        }

        public bool IsEntryParentOrWorldRoot(Transform t)
        {
            if (t == null || _worldBootstrap == null || _registry?.Entries == null) return false;
            if (t == _worldBootstrap.transform) return true;
            foreach (var entry in _registry.Entries)
            {
                if (entry == null) continue;
                var parent = _worldBootstrap.GetParentByEntryName(entry.Name);
                if (parent != null && t == parent) return true;
            }
            return false;
        }
    }
}
