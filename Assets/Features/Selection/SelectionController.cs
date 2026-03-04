using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Voxel.Pure;

namespace Voxel
{
    public class SelectionController : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PlacedObjectRegistry registry;
        [SerializeField] private ObjectPlacementController placementController;
        [SerializeField] private RemovalController removalController;

        [Header("Outline")]
        [SerializeField] private Color hoverOutlineColor = new Color(0.2f, 0.5f, 0.9f, 1f);
        [SerializeField] private Color selectedOutlineColor = new Color(0.4f, 0.7f, 1f, 1f);
        [SerializeField, Tooltip("Outline thickness in pixels")] private float hoverOutlineWidth = 3f;
        [SerializeField, Tooltip("Outline thickness in pixels")] private float selectedOutlineWidth = 6f;

        private VisualElement _selectionDetail;
        private VisualElement _inventorySection;
        private VisualElement _debugSection;
        private Label _selectionName;
        private Button _locateButton;
        private Button _clearInventoryButton;
        private Transform _selectedObject;
        private string _selectedEntryName;
        private Transform _hoveredObject;
        private Transform _lastHoveredObject;
        private Transform _lastSelectedObject;
        private ObjectPlacementController _placementController;
        private RemovalController _removalController;

        private SelectionOutlineRenderer _outlineRenderer;
        private SelectionRaycaster _raycaster;
        private IBuildingInventory _cachedInventory;
        private Camera _cachedCamera;
        private Func<bool> _escapeHandler;

        private void Start()
        {
            if (worldBootstrap == null)
                worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
            if (registry == null && worldBootstrap != null)
                registry = worldBootstrap.PlacedObjectRegistry;
            _placementController = placementController ?? FindAnyObjectByType<ObjectPlacementController>();
            _removalController = removalController ?? FindAnyObjectByType<RemovalController>();

            _outlineRenderer = new SelectionOutlineRenderer();
            _raycaster = new SelectionRaycaster(worldBootstrap, registry);

            if (uiDocument?.rootVisualElement != null)
            {
                _selectionDetail = uiDocument.rootVisualElement.Q<VisualElement>("SelectionDetail");
                _selectionName = uiDocument.rootVisualElement.Q<Label>("SelectionName");
                _inventorySection = uiDocument.rootVisualElement.Q<VisualElement>("InventorySection");
                _locateButton = uiDocument.rootVisualElement.Q<Button>("Locate");
                _debugSection = uiDocument.rootVisualElement.Q<VisualElement>("DebugSection");
                _clearInventoryButton = uiDocument.rootVisualElement.Q<Button>("ClearInventory");

                if (_locateButton != null)
                    _locateButton.clicked += OnLocateClicked;
                if (_clearInventoryButton != null)
                    _clearInventoryButton.clicked += OnClearInventoryClicked;

                _cachedCamera = Camera.main;
                UIPanelUtils.UpdateDebugControlsVisibility(uiDocument.rootVisualElement, worldBootstrap != null && worldBootstrap.ShowDebugControls);
                HideSelectionDetail();
            }

            _escapeHandler = TryClearSelection;
            EscapeHandler.Instance?.Register(EscapeHandler.PriorityDeselect, _escapeHandler);
        }

        private bool TryClearSelection()
        {
            if (_selectedObject == null) return false;
            ClearSelection();
            return true;
        }

        public Transform SelectedObject => _selectedObject;
        public string SelectedEntryName => _selectedEntryName;

        public void RefreshSelectionDisplay()
        {
            RefreshInventoryDisplay();
        }

        public void ClearSelection()
        {
            UnsubscribeFromInventory();
            if (_selectedObject != null)
            {
                _outlineRenderer?.ClearHighlight(_selectedObject);
                _selectedObject = null;
            }
            _selectedEntryName = null;
            _cachedInventory = null;
            HideSelectionDetail();
        }

        private void UnsubscribeFromInventory()
        {
            if (_cachedInventory != null)
            {
                _cachedInventory.InventoryChanged -= OnInventoryChanged;
            }
        }

        private void OnInventoryChanged()
        {
            RefreshInventoryDisplay();
        }

        private void ClearHoverHighlight()
        {
            if (_hoveredObject != null && _hoveredObject != _selectedObject)
                _outlineRenderer?.ClearHighlight(_hoveredObject);
            _hoveredObject = null;
        }

        private void UpdateHighlights()
        {
            if (_placementController != null && _placementController.IsPlacementModeActive)
                return;
            if (_removalController != null && _removalController.IsRemovalModeActive)
                return;

            if (_lastHoveredObject != _hoveredObject && _lastHoveredObject != _selectedObject)
                _outlineRenderer?.ClearHighlight(_lastHoveredObject);
            if (_lastSelectedObject != _selectedObject)
                _outlineRenderer?.ClearHighlight(_lastSelectedObject);

            bool IsExcluded(Transform t) => _raycaster != null && _raycaster.IsEntryParentOrWorldRoot(t);

            _outlineRenderer?.ApplyHighlight(_selectedObject, selectedOutlineColor, selectedOutlineWidth, IsExcluded);
            if (_hoveredObject != null && _hoveredObject != _selectedObject)
                _outlineRenderer?.ApplyHighlight(_hoveredObject, hoverOutlineColor, hoverOutlineWidth, IsExcluded);

            _lastHoveredObject = _hoveredObject;
            _lastSelectedObject = _selectedObject;
        }

        private void Update()
        {
            if (worldBootstrap == null || registry == null || _raycaster == null) return;
            if (_placementController != null && _placementController.IsPlacementModeActive)
            {
                ClearHoverHighlight();
                return;
            }
            if (_removalController != null && _removalController.IsRemovalModeActive)
            {
                ClearHoverHighlight();
                return;
            }

            if (_cachedCamera == null) _cachedCamera = Camera.main;
            var cam = _cachedCamera;
            if (cam != null && Mouse.current != null)
            {
                Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                             UIPanelUtils.IsPointerOverBlockingUI(uiDocument);

                if (!overUI && _raycaster.TryGetSelectableAtRay(ray, out Transform hitTransform, out _))
                    _hoveredObject = hitTransform;
                else
                    _hoveredObject = null;
            }
            else
            {
                _hoveredObject = null;
            }

            UpdateHighlights();

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                             UIPanelUtils.IsPointerOverBlockingUI(uiDocument);
                if (overUI) return;

                if (cam == null) return;

                Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (_raycaster.TryGetSelectableAtRay(ray, out Transform hitTransform, out string entryName))
                {
                    SelectObject(hitTransform, entryName);
                    return;
                }

                ClearSelection();
            }
        }

        private void SelectObject(Transform obj, string entryName)
        {
            UnsubscribeFromInventory();
            _selectedObject = obj;
            _selectedEntryName = entryName;
            _cachedInventory = obj != null ? obj.GetComponent<BuildingInventory>() as IBuildingInventory : null;
            if (_cachedInventory != null)
                _cachedInventory.InventoryChanged += OnInventoryChanged;
            ShowSelectionDetail(entryName);
        }

        private void ShowSelectionDetail(string name)
        {
            if (_selectionDetail != null)
                _selectionDetail.RemoveFromClassList("hidden");
            if (_selectionName != null)
                _selectionName.text = name;
            if (_inventorySection != null)
            {
                if (name == "Warehouse")
                    _inventorySection.AddToClassList("hidden");
                else
                    _inventorySection.RemoveFromClassList("hidden");
            }
            RefreshInventoryDisplay();
            UpdateClearInventoryButtonVisibility();
        }

        private void RefreshInventoryDisplay()
        {
            if (_inventorySection == null) return;

            _inventorySection.Clear();
            var inventory = _cachedInventory;
            IItemRegistry itemRegistry = worldBootstrap?.ItemRegistry;
            if (inventory != null && itemRegistry != null)
            {
                var capacityLabel = new Label($"{inventory.GetTotalCount()}/{inventory.MaxCapacity}");
                capacityLabel.AddToClassList("inventory-count");
                _inventorySection.Add(capacityLabel);

                foreach (var (item, count) in inventory.GetAllItems())
                {
                    var def = itemRegistry.GetDefinition(item);
                    if (def == null) continue;

                    var row = new VisualElement();
                    row.AddToClassList("inventory-row");

                    var icon = new VisualElement();
                    icon.AddToClassList("inventory-icon");
                    if (def.Sprite != null)
                        icon.style.backgroundImage = new StyleBackground(def.Sprite);

                    var countLabel = new Label(count.ToString());
                    countLabel.AddToClassList("inventory-count");

                    row.Add(icon);
                    row.Add(countLabel);
                    _inventorySection.Add(row);
                }
            }
        }

        private void HideSelectionDetail()
        {
            if (_selectionDetail != null)
                _selectionDetail.AddToClassList("hidden");
        }

        private void OnLocateClicked()
        {
            if (_selectedObject == null || worldBootstrap == null) return;
            worldBootstrap.CenterCameraOnPosition(_selectedObject.position);
        }

        private void OnClearInventoryClicked()
        {
            if (_cachedInventory == null) return;
            _cachedInventory.ClearInventory();
        }

        private void UpdateClearInventoryButtonVisibility()
        {
            if (_clearInventoryButton == null) return;
            bool show = worldBootstrap != null && worldBootstrap.ShowDebugControls && _cachedInventory != null;
            _clearInventoryButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateDebugControlsVisibility()
        {
            var root = uiDocument?.rootVisualElement;
            if (root == null) return;
            UIPanelUtils.UpdateDebugControlsVisibility(root, worldBootstrap != null && worldBootstrap.ShowDebugControls);
            UpdateClearInventoryButtonVisibility();
        }

        private void OnDestroy()
        {
            if (_escapeHandler != null)
                EscapeHandler.Instance?.Unregister(_escapeHandler);
        }
    }
}
