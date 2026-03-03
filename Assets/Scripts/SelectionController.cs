using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Voxel.Core;

namespace Voxel
{
    public class SelectionController : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PlacedObjectRegistry registry;

        private VisualElement _selectionDetail;
        private Label _selectionName;
        private Button _locateButton;
        private Transform _selectedObject;
        private string _selectedEntryName;
        private ObjectPlacementController _placementController;

        private void Start()
        {
            if (worldBootstrap == null)
                worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
            if (registry == null && worldBootstrap != null)
                registry = worldBootstrap.PlacedObjectRegistry;
            _placementController = FindAnyObjectByType<ObjectPlacementController>();

            if (uiDocument?.rootVisualElement != null)
            {
                _selectionDetail = uiDocument.rootVisualElement.Q<VisualElement>("SelectionDetail");
                _selectionName = uiDocument.rootVisualElement.Q<Label>("SelectionName");
                _locateButton = uiDocument.rootVisualElement.Q<Button>("Locate");

                if (_locateButton != null)
                    _locateButton.clicked += OnLocateClicked;

                HideSelectionDetail();
            }
        }

        public void ClearSelection()
        {
            _selectedObject = null;
            _selectedEntryName = null;
            HideSelectionDetail();
        }

        private void Update()
        {
            if (worldBootstrap == null || registry == null) return;
            if (_placementController != null && _placementController.IsPlacementModeActive)
                return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                var cam = Camera.main;
                if (cam == null) return;

                Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (TryGetSelectableAtRay(ray, out Transform hitTransform, out string entryName))
                {
                    SelectObject(hitTransform, entryName);
                    return;
                }

                ClearSelection();
            }
        }

        private bool TryGetSelectableAtRay(Ray ray, out Transform hitTransform, out string entryName)
        {
            hitTransform = null;
            entryName = null;
            float closestDistance = float.MaxValue;

            foreach (var hit in Physics.RaycastAll(ray))
            {
                if (hit.distance < closestDistance && hit.distance > 0 &&
                    IsSelectablePlacedObject(hit.transform, out string name))
                {
                    closestDistance = hit.distance;
                    hitTransform = GetRootPlacedObject(hit.transform);
                    entryName = name;
                }
            }

            foreach (var parent in new[] { worldBootstrap.HousesParent, worldBootstrap.TreesParent })
            {
                if (parent == null) continue;
                string typeName = parent == worldBootstrap.HousesParent ? "House" : "Tree";
                var entry = registry.GetByName(typeName);
                if (entry == null || !entry.IsSelectable) continue;

                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    var bounds = GetBounds(child);
                    if (bounds.HasValue && bounds.Value.IntersectRay(ray, out float distance) &&
                        distance > 0 && distance < closestDistance)
                    {
                        closestDistance = distance;
                        hitTransform = child;
                        entryName = typeName;
                    }
                }
            }

            return hitTransform != null;
        }

        private Transform GetRootPlacedObject(Transform t)
        {
            if (t == null) return null;
            Transform root = t;
            while (t != null)
            {
                if (t.parent == worldBootstrap.HousesParent || t.parent == worldBootstrap.TreesParent)
                    root = t;
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
            if (t == null || worldBootstrap == null || registry == null) return false;

            Transform parent = t.parent;
            while (parent != null)
            {
                if (parent == worldBootstrap.HousesParent)
                {
                    entryName = "House";
                    break;
                }
                if (parent == worldBootstrap.TreesParent)
                {
                    entryName = "Tree";
                    break;
                }
                parent = parent.parent;
            }

            if (string.IsNullOrEmpty(entryName)) return false;

            var entry = registry.GetByName(entryName);
            return entry != null && entry.IsSelectable;
        }

        private void SelectObject(Transform obj, string entryName)
        {
            _selectedObject = obj;
            _selectedEntryName = entryName;
            ShowSelectionDetail(entryName);
        }

        private void ShowSelectionDetail(string name)
        {
            if (_selectionDetail != null)
            {
                _selectionDetail.RemoveFromClassList("hidden");
            }
            if (_selectionName != null)
            {
                _selectionName.text = name;
            }
        }

        private void HideSelectionDetail()
        {
            if (_selectionDetail != null)
            {
                _selectionDetail.AddToClassList("hidden");
            }
        }

        private void OnLocateClicked()
        {
            if (_selectedObject == null || worldBootstrap == null) return;

            Vector3 worldPos = _selectedObject.position;
            worldBootstrap.CenterCameraOnPosition(worldPos);
        }
    }
}
