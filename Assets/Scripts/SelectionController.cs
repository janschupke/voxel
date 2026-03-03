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
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    var hitTransform = hit.transform;
                    if (IsSelectablePlacedObject(hitTransform, out string entryName))
                    {
                        SelectObject(hitTransform, entryName);
                        return;
                    }
                }

                ClearSelection();
            }
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
