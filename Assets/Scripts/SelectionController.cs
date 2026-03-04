using System.Collections.Generic;
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

        [Header("Outline")]
        [SerializeField] private Color hoverOutlineColor = new Color(0.2f, 0.5f, 0.9f, 1f);
        [SerializeField] private Color selectedOutlineColor = new Color(0.4f, 0.7f, 1f, 1f);
        [SerializeField, Tooltip("Outline thickness in pixels")] private float hoverOutlineWidth = 3f;
        [SerializeField, Tooltip("Outline thickness in pixels")] private float selectedOutlineWidth = 6f;

        private VisualElement _selectionDetail;
        private VisualElement _inventorySection;
        private Label _selectionName;
        private Button _locateButton;
        private Transform _selectedObject;
        private string _selectedEntryName;
        private Transform _hoveredObject;
        private Transform _lastHoveredObject;
        private Transform _lastSelectedObject;
        private ObjectPlacementController _placementController;

        private readonly Dictionary<Transform, GameObject> _outlineObjects = new Dictionary<Transform, GameObject>();
        private Shader _outlineShader;
        private float _inventoryRefreshTimer;
        private BuildingInventory _cachedInventory;
        private const int RaycastBufferSize = 32;
        private readonly RaycastHit[] _raycastBuffer = new RaycastHit[RaycastBufferSize];
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private void Start()
        {
            _outlineShader = Shader.Find("Voxel/SelectionOutline");
            if (worldBootstrap == null)
                worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
            if (registry == null && worldBootstrap != null)
                registry = worldBootstrap.PlacedObjectRegistry;
            _placementController = FindAnyObjectByType<ObjectPlacementController>();

            if (uiDocument?.rootVisualElement != null)
            {
                _selectionDetail = uiDocument.rootVisualElement.Q<VisualElement>("SelectionDetail");
                _selectionName = uiDocument.rootVisualElement.Q<Label>("SelectionName");
                _inventorySection = uiDocument.rootVisualElement.Q<VisualElement>("InventorySection");
                _locateButton = uiDocument.rootVisualElement.Q<Button>("Locate");

                if (_locateButton != null)
                    _locateButton.clicked += OnLocateClicked;

                HideSelectionDetail();
            }
        }

        public Transform SelectedObject => _selectedObject;
        public string SelectedEntryName => _selectedEntryName;

        public void ClearSelection()
        {
            if (_selectedObject != null)
            {
                ClearHighlight(_selectedObject);
                _selectedObject = null;
            }
            _selectedEntryName = null;
            _cachedInventory = null;
            HideSelectionDetail();
        }

        private void ClearHoverHighlight()
        {
            if (_hoveredObject != null && _hoveredObject != _selectedObject)
                ClearHighlight(_hoveredObject);
            _hoveredObject = null;
        }

        private void UpdateHighlights()
        {
            if (_placementController != null && _placementController.IsPlacementModeActive)
                return;

            if (_lastHoveredObject != _hoveredObject && _lastHoveredObject != _selectedObject)
                ClearHighlight(_lastHoveredObject);
            if (_lastSelectedObject != _selectedObject)
                ClearHighlight(_lastSelectedObject);

            ApplyOutlineHighlight(_selectedObject, selectedOutlineColor, selectedOutlineWidth);
            if (_hoveredObject != null && _hoveredObject != _selectedObject)
                ApplyOutlineHighlight(_hoveredObject, hoverOutlineColor, hoverOutlineWidth);

            _lastHoveredObject = _hoveredObject;
            _lastSelectedObject = _selectedObject;

            if (_selectedObject != null && _cachedInventory != null)
            {
                _inventoryRefreshTimer -= Time.deltaTime;
                if (_inventoryRefreshTimer <= 0f)
                {
                    _inventoryRefreshTimer = 0.5f;
                    RefreshInventoryDisplay();
                }
            }
        }

        private void ApplyOutlineHighlight(Transform t, Color color, float width)
        {
            if (t == null || _outlineShader == null) return;
            if (IsEntryParentOrWorldRoot(t)) return;

            if (_outlineObjects.TryGetValue(t, out GameObject existing))
            {
                existing.SetActive(true);
                ApplyOutlineParams(existing, color, width);
                return;
            }

            var meshFilters = t.GetComponentsInChildren<MeshFilter>();
            if (meshFilters == null || meshFilters.Length == 0) return;

            var outlineRoot = new GameObject("SelectionOutline");
            outlineRoot.transform.SetParent(t, false);
            outlineRoot.transform.localPosition = Vector3.zero;
            outlineRoot.transform.localRotation = Quaternion.identity;
            outlineRoot.transform.localScale = Vector3.one;

            var mat = new Material(_outlineShader);
            mat.SetColor(OutlineColorId, color);
            mat.SetFloat(OutlineWidthId, width);

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                var child = new GameObject("OutlineMesh");
                child.transform.SetParent(outlineRoot.transform, false);
                child.transform.localPosition = t.InverseTransformPoint(mf.transform.position);
                child.transform.localRotation = Quaternion.Inverse(t.rotation) * mf.transform.rotation;
                child.transform.localScale = Vector3.one;

                var filter = child.AddComponent<MeshFilter>();
                filter.sharedMesh = mf.sharedMesh;

                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _outlineObjects[t] = outlineRoot;
        }

        private void ApplyOutlineParams(GameObject outlineRoot, Color color, float width)
        {
            foreach (var r in outlineRoot.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.sharedMaterial != null)
                {
                    r.sharedMaterial.SetColor(OutlineColorId, color);
                    r.sharedMaterial.SetFloat(OutlineWidthId, width);
                }
            }
        }

        private void ClearHighlight(Transform t)
        {
            if (t == null) return;
            if (_outlineObjects.TryGetValue(t, out GameObject outlineRoot))
            {
                _outlineObjects.Remove(t);
                Destroy(outlineRoot);
            }
        }

        private void Update()
        {
            if (worldBootstrap == null || registry == null) return;
            if (_placementController != null && _placementController.IsPlacementModeActive)
            {
                ClearHoverHighlight();
                return;
            }

            var cam = Camera.main;
            if (cam != null && Mouse.current != null)
            {
                Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                bool overUI = (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                             UIPanelUtils.IsPointerOverBlockingUI(uiDocument);

                if (!overUI && TryGetSelectableAtRay(ray, out Transform hitTransform, out _))
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

            if (registry?.Entries != null)
            {
                foreach (var entry in registry.Entries)
                {
                    if (entry == null || !entry.IsSelectable) continue;
                    var parent = worldBootstrap.GetParentByEntryName(entry.Name);
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
            if (t == null || worldBootstrap == null || registry == null) return null;
            Transform root = t;
            while (t != null)
            {
                if (registry.Entries != null)
                {
                    foreach (var entry in registry.Entries)
                    {
                        if (entry == null) continue;
                        var parent = worldBootstrap.GetParentByEntryName(entry.Name);
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

        private bool IsEntryParentOrWorldRoot(Transform t)
        {
            if (t == null || worldBootstrap == null || registry == null) return false;
            if (t == worldBootstrap.transform) return true;
            foreach (var entry in registry.Entries)
            {
                if (entry == null) continue;
                var parent = worldBootstrap.GetParentByEntryName(entry.Name);
                if (parent != null && t == parent) return true;
            }
            return false;
        }

        private bool IsSelectablePlacedObject(Transform t, out string entryName)
        {
            entryName = null;
            if (t == null || worldBootstrap == null || registry == null) return false;

            Transform parent = t.parent;
            while (parent != null)
            {
                if (registry.Entries != null)
                {
                    foreach (var entry in registry.Entries)
                    {
                        if (entry == null || !entry.IsSelectable) continue;
                        if (worldBootstrap.GetParentByEntryName(entry.Name) == parent)
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

        private void SelectObject(Transform obj, string entryName)
        {
            _selectedObject = obj;
            _selectedEntryName = entryName;
            _cachedInventory = obj != null ? obj.GetComponent<BuildingInventory>() : null;
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
            _inventoryRefreshTimer = 0f;
            RefreshInventoryDisplay();
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
