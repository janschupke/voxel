using System;
using System.Collections.Generic;
using System.Linq;
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
        private VisualElement _selectionIcon;
        private VisualElement _inventorySection;
        private VisualElement _debugSection;
        private Label _selectionName;
        private Button _locateButton;
        private Button _clearInventoryButton;
        private Transform _selectedObject;
        private Transform _locateTarget;
        private string _selectedEntryName;
        private Transform _hoveredObject;
        private Transform _lastHoveredObject;
        private Transform _lastSelectedObject;
        private ObjectPlacementController _placementController;
        private RemovalController _removalController;

        private SelectionOutlineRenderer _outlineRenderer;
        private SelectionRaycaster _raycaster;
        private IBuildingInventory _cachedInventory;
        private BuildingProduction _cachedProduction;
        private VisualElement _recipeListSection;
        private Camera _cachedCamera;
        private Func<bool> _escapeHandler;

        private void Awake()
        {
            WorldBootstrap.WorldReady += OnWorldReady;
        }

        private void OnWorldReady(WorldBootstrap wb)
        {
            WorldBootstrap.WorldReady -= OnWorldReady;
            worldBootstrap = wb;
            if (registry == null && wb != null)
                registry = wb.PlacedObjectRegistry;
            _outlineRenderer = new SelectionOutlineRenderer();
            _raycaster = new SelectionRaycaster(wb, registry);
            if (uiDocument?.rootVisualElement != null)
                UIPanelUtils.UpdateDebugControlsVisibility(uiDocument.rootVisualElement, wb != null && wb.ShowDebugControls);
        }

        private void Start()
        {
            _placementController = placementController ?? FindAnyObjectByType<ObjectPlacementController>();
            _removalController = removalController ?? FindAnyObjectByType<RemovalController>();

            if (uiDocument?.rootVisualElement != null)
            {
                _selectionDetail = uiDocument.rootVisualElement.Q<VisualElement>("SelectionDetail");
                _selectionIcon = uiDocument.rootVisualElement.Q<VisualElement>("SelectionIcon");
                _selectionName = uiDocument.rootVisualElement.Q<Label>("SelectionName");
                _inventorySection = uiDocument.rootVisualElement.Q<VisualElement>("InventorySection");
                _recipeListSection = uiDocument.rootVisualElement.Q<VisualElement>("RecipeListSection");
                _locateButton = uiDocument.rootVisualElement.Q<Button>("Locate");
                _debugSection = uiDocument.rootVisualElement.Q<VisualElement>("DebugSection");
                _clearInventoryButton = uiDocument.rootVisualElement.Q<Button>("ClearInventory");

                if (_locateButton != null)
                    _locateButton.clicked += OnLocateClicked;
                if (_clearInventoryButton != null)
                    _clearInventoryButton.clicked += OnClearInventoryClicked;

                _cachedCamera = Camera.main;
                if (worldBootstrap != null)
                    UIPanelUtils.UpdateDebugControlsVisibility(uiDocument.rootVisualElement, worldBootstrap.ShowDebugControls);
                HideSelectionDetail();
            }

            _escapeHandler = TryClearSelection;
            HotkeyManager.Instance?.Register(Key.Escape, "ESC", "Deselect", null, _escapeHandler, HotkeyManager.PriorityDeselect);
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
            if (_recipeListSection != null && !_recipeListSection.ClassListContains("hidden"))
                RefreshRecipeListDisplay();
        }

        public void ClearSelection()
        {
            UnsubscribeFromInventory();
            if (_selectedObject != null)
            {
                _outlineRenderer?.ClearHighlight(_selectedObject);
                _selectedObject = null;
            }
            _locateTarget = null;
            _selectedEntryName = null;
            _cachedInventory = null;
            _cachedProduction = null;
            HideSelectionDetail();
        }

        private void UnsubscribeFromInventory()
        {
            if (_cachedInventory != null)
            {
                _cachedInventory.InventoryChanged -= OnInventoryChanged;
            }
            if (_cachedProduction != null)
            {
                _cachedProduction.StateChanged -= OnProductionStateChanged;
            }
        }

        private void OnInventoryChanged()
        {
            RefreshInventoryDisplay();
            if (_recipeListSection != null && !_recipeListSection.ClassListContains("hidden"))
                RefreshRecipeListDisplay();
        }

        private void OnProductionStateChanged()
        {
            if (_selectedObject == null) return;
            RefreshRecipeListDisplay();
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

                if (overUI)
                    _hoveredObject = null;
                else if (_raycaster.TryGetSelectableAtRay(ray, out Transform hitTransform, out _))
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
            if (_raycaster == null || !_raycaster.IsEntryParentOrWorldRoot(obj))
                _locateTarget = obj;
            _selectedEntryName = entryName;
            _cachedInventory = obj != null ? obj.GetComponent<BuildingInventory>() as IBuildingInventory : null;
            _cachedProduction = obj != null ? obj.GetComponent<BuildingProduction>() : null;
            if (_cachedInventory != null)
                _cachedInventory.InventoryChanged += OnInventoryChanged;
            if (_cachedProduction != null)
                _cachedProduction.StateChanged += OnProductionStateChanged;
            ShowSelectionDetail(entryName);
        }

        private void ShowSelectionDetail(string name)
        {
            if (_selectionDetail != null)
                _selectionDetail.RemoveFromClassList("hidden");
            if (_selectionName != null)
                _selectionName.text = name;
            if (_selectionIcon != null)
            {
                var entry = registry?.GetByName(name);
                var sprite = entry?.Sprite;
                if (sprite != null)
                    _selectionIcon.style.backgroundImage = new StyleBackground(sprite);
                else
                    _selectionIcon.style.backgroundImage = StyleKeyword.None;
            }
            if (_inventorySection != null)
            {
                var entry = registry?.GetByName(name);
                bool hideInventory = entry != null && entry.UsesGlobalStorage;
                if (hideInventory)
                    _inventorySection.AddToClassList("hidden");
                else
                    _inventorySection.RemoveFromClassList("hidden");
            }
            if (_recipeListSection != null)
            {
                var entry = registry?.GetByName(name);
                bool showRecipes = entry?.RecipeList != null;
                if (showRecipes)
                {
                    _recipeListSection.RemoveFromClassList("hidden");
                    RefreshRecipeListDisplay();
                }
                else
                    _recipeListSection.AddToClassList("hidden");
            }
            RefreshInventoryDisplay();
            UpdateClearInventoryButtonVisibility();
        }

        private void RefreshRecipeListDisplay()
        {
            if (_recipeListSection == null || _selectedObject == null) return;
            var entry = registry?.GetByName(_selectedEntryName);
            if (entry?.RecipeList == null) return;
            var itemRegistry = worldBootstrap?.ItemRegistry;
            if (itemRegistry == null) return;

            _recipeListSection.Clear();

            var header = new Label("Production");
            header.AddToClassList("production-header");
            header.AddToClassList("inventory-category-header");
            _recipeListSection.Add(header);

            if (_cachedProduction != null)
            {
                var stateRow = new VisualElement();
                stateRow.AddToClassList("production-state-row");
                var stateLabel = new Label(_cachedProduction.State.ToString());
                stateLabel.AddToClassList("production-state-" + _cachedProduction.State.ToString().ToLowerInvariant());
                stateRow.Add(stateLabel);
                _recipeListSection.Add(stateRow);

                if (_cachedProduction.State == ProductionState.Producing)
                {
                    var progressBar = new VisualElement();
                    progressBar.AddToClassList("production-progress-bar");
                    var fill = new VisualElement();
                    fill.AddToClassList("production-progress-fill");
                    fill.style.width = Length.Percent(_cachedProduction.ProgressNormalized * 100f);
                    progressBar.Add(fill);
                    _recipeListSection.Add(progressBar);
                }
            }

            var recipes = entry.RecipeList.Recipes;
            if (recipes == null) return;
            for (int i = 0; i < recipes.Length; i++)
            {
                var recipe = recipes[i];
                if (recipe == null) continue;
                var row = new VisualElement();
                row.AddToClassList("production-recipe-row");
                if (_cachedProduction != null && _cachedProduction.SelectedRecipeIndex == i)
                    row.AddToClassList("production-recipe-selected");
                row.AddToClassList("inventory-row");

                var nameLabel = new Label(recipe.Name ?? "?");
                nameLabel.AddToClassList("inventory-count");
                nameLabel.style.flexGrow = 1;
                row.Add(nameLabel);

                var inputs = new Label(FormatRecipeIO(recipe.Inputs, itemRegistry));
                inputs.AddToClassList("inventory-count");
                inputs.style.fontSize = 8;
                row.Add(inputs);

                var outputs = new Label(" → " + FormatRecipeIO(recipe.Outputs, itemRegistry));
                outputs.AddToClassList("inventory-count");
                outputs.style.fontSize = 8;
                row.Add(outputs);

                var duration = new Label($" {recipe.WorkDurationSeconds:F0}s");
                duration.AddToClassList("inventory-count");
                duration.style.fontSize = 8;
                row.Add(duration);

                int index = i;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_cachedProduction != null && index >= 0 && index < recipes.Length)
                    {
                        _cachedProduction.SelectedRecipeIndex = index;
                        RefreshRecipeListDisplay();
                    }
                });
                row.pickingMode = PickingMode.Position;

                _recipeListSection.Add(row);
            }
        }

        private static string FormatRecipeIO(RecipeInputOutput[] items, IItemRegistry itemRegistry)
        {
            if (items == null || items.Length == 0) return "";
            var parts = new List<string>();
            foreach (var io in items)
            {
                if (io.Count <= 0) continue;
                var def = itemRegistry?.GetDefinition(io.Item);
                var name = def?.Name ?? io.Item.ToString();
                parts.Add(io.Count > 1 ? $"{name} x{io.Count}" : name);
            }
            return string.Join(", ", parts);
        }

        private void RefreshInventoryDisplay()
        {
            if (_inventorySection == null) return;

            _inventorySection.Clear();
            var inventory = _cachedInventory;
            var itemRegistry = worldBootstrap?.ItemRegistry;
            if (inventory != null && itemRegistry != null)
            {
                var capacityLabel = new Label($"{inventory.GetTotalCount()}/{inventory.MaxCapacity}");
                capacityLabel.AddToClassList("inventory-count");
                _inventorySection.Add(capacityLabel);

                var itemsByCategory = GroupInventoryItemsByCategory(inventory.GetAllItems(), itemRegistry);
                var categoryOrder = itemRegistry.CategoryDisplayOrder ?? Array.Empty<string>();

                foreach (var category in GetCategoriesInDisplayOrder(itemsByCategory.Keys, categoryOrder))
                {
                    if (!itemsByCategory.TryGetValue(category, out var items) || items.Count == 0) continue;

                    var header = new Label(category);
                    header.AddToClassList("inventory-category-header");
                    _inventorySection.Add(header);

                    foreach (var (item, count) in items)
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
        }

        private static Dictionary<string, List<(Item Item, int Count)>> GroupInventoryItemsByCategory(
            IEnumerable<(Item Item, int Count)> items, IItemRegistry itemRegistry)
        {
            var grouped = new Dictionary<string, List<(Item, int)>>();
            foreach (var (item, count) in items)
            {
                var def = itemRegistry.GetDefinition(item);
                var category = def?.CategoryDisplayName ?? "Other";
                if (!grouped.TryGetValue(category, out var list))
                {
                    list = new List<(Item, int)>();
                    grouped[category] = list;
                }
                list.Add((item, count));
            }
            return grouped;
        }

        private static IEnumerable<string> GetCategoriesInDisplayOrder(IEnumerable<string> categories, IReadOnlyList<string> order)
        {
            var set = categories.ToHashSet();
            var orderList = order ?? new List<string>();
            foreach (var cat in orderList)
            {
                if (set.Contains(cat))
                    yield return cat;
            }
            foreach (var cat in set.Where(c => !orderList.Contains(c)))
                yield return cat;
        }

        private void HideSelectionDetail()
        {
            if (_selectionDetail != null)
                _selectionDetail.AddToClassList("hidden");
        }

        private void OnLocateClicked()
        {
            var target = _locateTarget ?? _selectedObject;
            if (target == null || worldBootstrap == null) return;
            var footprintCenter = worldBootstrap.GetFootprintCenterWorld(target);
            Vector3 center = footprintCenter ?? target.position;
            worldBootstrap.CenterCameraOnPosition(center);
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
            WorldBootstrap.WorldReady -= OnWorldReady;
            if (_escapeHandler != null)
                HotkeyManager.Instance?.Unregister(_escapeHandler);
        }
    }
}
