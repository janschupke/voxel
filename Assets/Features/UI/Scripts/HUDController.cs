using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Voxel;
using Voxel.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class HUDController : MonoBehaviour
{
    private const float LogDisplayDurationMs = 5000f;
    private const float LogFadeDurationMs = 2000f;

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private PlacedObjectRegistry placedObjectRegistry;
    [SerializeField] private WorldBootstrap worldBootstrap;
    [SerializeField] private ObjectPlacementController placementController;
    [SerializeField] private SelectionController selectionController;
    [SerializeField] private RemovalController removalController;
    [SerializeField] private DebugLogService debugLogService;

    private ObjectPlacementController _placementController;
    private SelectionController _selectionController;
    private RemovalController _removalController;
    private Button _removeButton;
    private Label _fpsLabel;
    private Button _debugButton;
    private float _fpsAccumulator;
    private int _fpsFrameCount;
    private VisualElement _messageLog;
    private DebugLogService _debugLogService;
    private VisualElement _inventoryPanel;
    private ScrollView _inventoryPanelList;
    private Func<bool> _escapeHandlerGoBack;
    private Func<bool> _escapeHandlerOpenMenu;
    private Func<bool> _hotkeyHandlerI;
    private Func<bool> _hotkeyHandlerL;
    private Func<bool> _hotkeyHandlerF2;
    private Func<bool> _hotkeyHandlerQ;
    private Func<bool> _hotkeyHandlerE;
    private VisualElement _logPanel;
    private ScrollView _logPanelList;

    private void Awake()
    {
        WorldBootstrap.WorldReady += OnWorldReady;
    }

    private void OnWorldReady(WorldBootstrap wb)
    {
        WorldBootstrap.WorldReady -= OnWorldReady;
        worldBootstrap = wb;
        if (wb != null)
        {
            var storage = wb.StorageInventory;
            if (storage != null)
                storage.StorageChanged += OnStorageChanged;
            if (uiDocument?.rootVisualElement != null)
            {
                SetupDebugControls(uiDocument.rootVisualElement, wb);
                UpdateDebugControlsVisibility(uiDocument.rootVisualElement, wb);
            }
            var cam = wb.TopDownCamera ?? FindAnyObjectByType<TopDownCamera>();
            var zoomBlockerComp = GetComponent<ZoomBlockerComponent>();
            if (zoomBlockerComp == null)
                zoomBlockerComp = gameObject.AddComponent<ZoomBlockerComponent>();
            cam?.SetZoomBlocker(zoomBlockerComp);
        }
    }

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument?.rootVisualElement != null)
        {
            _fpsLabel = uiDocument.rootVisualElement.Q<Label>("FPS");

            if (worldBootstrap == null) worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
            _placementController = placementController ?? FindAnyObjectByType<ObjectPlacementController>();
            _selectionController = selectionController ?? FindAnyObjectByType<SelectionController>();
            _removalController = removalController ?? FindAnyObjectByType<RemovalController>();
            if (_removalController == null && _placementController != null)
                _removalController = _placementController.gameObject.AddComponent<RemovalController>();

            _removeButton = uiDocument.rootVisualElement.Q<Button>("Remove");
            if (_removeButton != null && _removalController != null)
            {
                _removalController.RemovalModeChanged += UpdateRemoveButtonState;
                _removeButton.clicked += () => _removalController.ToggleRemovalMode();
            }

            var menuButton = uiDocument.rootVisualElement.Q<Button>("Menu");
            if (menuButton != null)
                menuButton.clicked += () => PanelManager.Instance?.OpenPanel(PanelManager.PanelMenu);

            _debugButton = uiDocument.rootVisualElement.Q<Button>("Debug");
            if (_debugButton != null)
            {
                UpdateDebugButtonText();
                _debugButton.clicked += () =>
                {
                    GameDebugLogger.SetEnabled(!GameDebugLogger.IsEnabled);
                    UpdateDebugButtonText();
                };
            }

            _inventoryPanel = uiDocument.rootVisualElement.Q<VisualElement>("InventoryPanel");
            _inventoryPanelList = uiDocument.rootVisualElement.Q<ScrollView>("InventoryPanelList");
            var inventoryButton = uiDocument.rootVisualElement.Q<Button>("Inventory");
            if (inventoryButton != null)
                inventoryButton.clicked += ToggleInventoryPanel;

            var logButton = uiDocument.rootVisualElement.Q<Button>("Log");
            if (logButton != null)
                logButton.clicked += ToggleLogPanel;

            var placementContainer = uiDocument.rootVisualElement.Q<VisualElement>("PlacementButtons");

            if (placementContainer != null && _placementController != null)
            {
                var registry = placedObjectRegistry != null ? placedObjectRegistry : worldBootstrap?.PlacedObjectRegistry;
                if (registry != null && registry.Entries != null)
                {
                    foreach (var entry in registry.Entries)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.Name)) continue;

                        var button = new Button { text = entry.Name, focusable = false };
                        button.name = entry.Name;
                        if (entry.Prefab == null)
                            button.SetEnabled(false);
                        placementContainer.Add(button);

                        _placementController.RegisterButton(entry.Name, button);
                        var entryName = entry.Name;
                        button.clicked += () => _placementController.TogglePlacementMode(entryName);
                    }
                }
            }

            _messageLog = uiDocument.rootVisualElement.Q<VisualElement>("MessageLog");
            _logPanel = uiDocument.rootVisualElement.Q<VisualElement>("LogPanel");
            _logPanelList = uiDocument.rootVisualElement.Q<ScrollView>("LogPanelList");

            if (PanelManager.Instance != null && _inventoryPanel != null)
                PanelManager.Instance.RegisterPanel(PanelManager.PanelInventory, _inventoryPanel, RefreshInventoryPanel, null);
            if (PanelManager.Instance != null && _logPanel != null)
                PanelManager.Instance.RegisterPanel(PanelManager.PanelLog, _logPanel, RefreshLogPanel, null);

            _debugLogService = debugLogService ?? GetComponent<DebugLogService>();
            if (_debugLogService == null)
                _debugLogService = FindAnyObjectByType<DebugLogService>();
            if (_debugLogService == null)
                _debugLogService = gameObject.AddComponent<DebugLogService>();
            if (_messageLog != null && _debugLogService != null)
            {
                _debugLogService.LogReceived += OnLogReceived;
            }

            _escapeHandlerGoBack = TryGoBack;
            _escapeHandlerOpenMenu = TryOpenMenu;
            HotkeyManager.Instance?.Register(Key.Escape, "ESC", "Go back / close panel", null, _escapeHandlerGoBack, HotkeyManager.PriorityUiOverlay + 10);
            HotkeyManager.Instance?.Register(Key.Escape, "ESC", "Open menu", null, _escapeHandlerOpenMenu, HotkeyManager.PriorityDeselect - 10);

            _hotkeyHandlerI = () => { ToggleInventoryPanel(); return true; };
            _hotkeyHandlerL = () => { ToggleLogPanel(); return true; };
            HotkeyManager.Instance?.Register(Key.I, "I", "Toggle inventory panel", null, _hotkeyHandlerI);
            HotkeyManager.Instance?.Register(Key.L, "L", "Toggle log window", null, _hotkeyHandlerL);

            _hotkeyHandlerF2 = () => { GameDebugLogger.SetEnabled(!GameDebugLogger.IsEnabled); UpdateDebugButtonText(); return true; };
            HotkeyManager.Instance?.Register(Key.F2, "F2", "Toggle debug logger", null, _hotkeyHandlerF2);

            _hotkeyHandlerQ = () => { (worldBootstrap?.TopDownCamera ?? FindAnyObjectByType<TopDownCamera>())?.RotateYawBy(-90f); return true; };
            HotkeyManager.Instance?.Register(Key.Q, "Q", "Rotate camera left 90°", null, _hotkeyHandlerQ);

            _hotkeyHandlerE = () => { (worldBootstrap?.TopDownCamera ?? FindAnyObjectByType<TopDownCamera>())?.RotateYawBy(90f); return true; };
            HotkeyManager.Instance?.Register(Key.E, "E", "Rotate camera right 90°", null, _hotkeyHandlerE);

        }
    }

    private bool TryGoBack()
    {
        return PanelManager.Instance != null && PanelManager.Instance.GoBack();
    }

    private bool TryOpenMenu()
    {
        if (PanelManager.Instance == null) return false;
        if (PanelManager.Instance.IsPanelOpen(PanelManager.PanelInventory) ||
            PanelManager.Instance.IsPanelOpen(PanelManager.PanelLog) ||
            PanelManager.Instance.IsPanelOpen(PanelManager.PanelHotkeys) ||
            PanelManager.Instance.IsPanelOpen(PanelManager.PanelMenu))
            return false;
        PanelManager.Instance.OpenPanel(PanelManager.PanelMenu);
        return true;
    }

    private void ToggleLogPanel()
    {
        PanelManager.Instance?.TogglePanel(PanelManager.PanelLog);
    }

    private void RefreshLogPanel()
    {
        if (_logPanelList == null || _debugLogService == null) return;
        _logPanelList.Clear();
        foreach (var entry in _debugLogService.Entries)
            AddLogEntryToPanel(entry);
        ScrollLogPanelToBottom();
    }

    private void ScrollLogPanelToBottom()
    {
        if (_logPanelList == null || _logPanelList.childCount == 0) return;
        var last = _logPanelList[_logPanelList.childCount - 1];
        _logPanelList.ScrollTo(last);
    }

    private void AddLogEntryToPanel(LogEntry entry)
    {
        if (_logPanelList == null) return;
        var label = new Label(entry.Message) { enableRichText = false };
        label.style.whiteSpace = WhiteSpace.Normal;
        label.AddToClassList("log-panel-entry");
        switch (entry.Type)
        {
            case LogType.Warning:
                label.AddToClassList("log-panel-entry-warning");
                break;
            case LogType.Error:
            case LogType.Exception:
                label.AddToClassList("log-panel-entry-error");
                break;
        }
        _logPanelList.Add(label);
    }

    private void OnStorageChanged()
    {
        if (PanelManager.Instance != null && PanelManager.Instance.IsPanelOpen(PanelManager.PanelInventory))
            RefreshInventoryPanel();
    }

    private void SetupDebugControls(VisualElement root, WorldBootstrap worldBootstrap)
    {
        if (root == null) return;

        var clearAllButton = root.Q<Button>("ClearAllInventories");
        if (clearAllButton != null && worldBootstrap != null)
        {
            clearAllButton.clicked += () => ClearAllInventories(worldBootstrap);
        }

        UpdateDebugControlsVisibility(root, worldBootstrap);
    }

    private void UpdateDebugControlsVisibility(VisualElement root, WorldBootstrap worldBootstrap)
    {
        if (root == null) return;
        UIPanelUtils.UpdateDebugControlsVisibility(root, worldBootstrap != null && worldBootstrap.ShowDebugControls);
    }

    private void ClearAllInventories(WorldBootstrap worldBootstrap)
    {
        if (worldBootstrap?.PlacedObjectRegistry == null) return;
        var registry = worldBootstrap.PlacedObjectRegistry;
        foreach (var entry in registry.Entries)
        {
            if (entry == null || entry.InventoryCapacity <= 0 || entry.UsesGlobalStorage) continue;
            var parent = worldBootstrap.GetParentByEntryName(entry.Name);
            if (parent == null) continue;
            for (int i = 0; i < parent.childCount; i++)
            {
                var inv = parent.GetChild(i).GetComponent<BuildingInventory>();
                if (inv != null)
                    inv.ClearInventory();
            }
        }
        worldBootstrap.StorageInventory?.Clear();
        _selectionController?.RefreshSelectionDisplay();
    }

    private void UpdateRemoveButtonState()
    {
        if (_removeButton != null && _removalController != null)
        {
            if (_removalController.IsRemovalModeActive)
                _removeButton.AddToClassList("placing");
            else
                _removeButton.RemoveFromClassList("placing");
        }
    }

    private void UpdateDebugButtonText()
    {
        if (_debugButton != null)
            _debugButton.text = GameDebugLogger.IsEnabled ? "Debug On" : "Debug Off";
    }

    private void ToggleInventoryPanel()
    {
        PanelManager.Instance?.TogglePanel(PanelManager.PanelInventory);
    }

    private void RefreshInventoryPanel()
    {
        if (_inventoryPanelList == null || worldBootstrap == null) return;
        _inventoryPanelList.Clear();

        var itemRegistry = worldBootstrap.ItemRegistry;
        var storage = worldBootstrap.StorageInventory;
        if (itemRegistry == null || storage == null) return;

        foreach (Item item in Enum.GetValues(typeof(Item)))
        {
            var def = itemRegistry.GetDefinition(item);
            var count = storage.GetCount(item);

            var row = new VisualElement();
            row.AddToClassList("inventory-row");

            var icon = new VisualElement();
            icon.AddToClassList("inventory-icon");
            if (def?.Sprite != null)
                icon.style.backgroundImage = new StyleBackground(def.Sprite);

            var nameLabel = new Label(def?.Name ?? item.ToString());
            nameLabel.AddToClassList("inventory-count");
            nameLabel.style.flexGrow = 1;

            var countLabel = new Label(count.ToString());
            countLabel.AddToClassList("inventory-count");

            row.Add(icon);
            row.Add(nameLabel);
            row.Add(countLabel);
            _inventoryPanelList.Add(row);
        }
    }

    private void OnDestroy()
    {
        WorldBootstrap.WorldReady -= OnWorldReady;
        if (_debugLogService != null)
            _debugLogService.LogReceived -= OnLogReceived;
        var storage = worldBootstrap?.StorageInventory;
        if (storage != null)
            storage.StorageChanged -= OnStorageChanged;
        if (_escapeHandlerGoBack != null)
            HotkeyManager.Instance?.Unregister(_escapeHandlerGoBack);
        if (_escapeHandlerOpenMenu != null)
            HotkeyManager.Instance?.Unregister(_escapeHandlerOpenMenu);
        if (_hotkeyHandlerI != null)
            HotkeyManager.Instance?.Unregister(_hotkeyHandlerI);
        if (_hotkeyHandlerL != null)
            HotkeyManager.Instance?.Unregister(_hotkeyHandlerL);
        if (_hotkeyHandlerF2 != null)
            HotkeyManager.Instance?.Unregister(_hotkeyHandlerF2);
        if (_hotkeyHandlerQ != null)
            HotkeyManager.Instance?.Unregister(_hotkeyHandlerQ);
        if (_hotkeyHandlerE != null)
            HotkeyManager.Instance?.Unregister(_hotkeyHandlerE);
        if (_removalController != null)
            _removalController.RemovalModeChanged -= UpdateRemoveButtonState;
    }

    private void OnLogReceived(LogEntry entry)
    {
        if (PanelManager.Instance != null && PanelManager.Instance.IsPanelOpen(PanelManager.PanelLog))
        {
            AddLogEntryToPanel(entry);
            ScrollLogPanelToBottom();
        }

        if (_messageLog == null) return;

        var label = new Label(entry.Message)
        {
            enableRichText = false
        };
        label.style.whiteSpace = WhiteSpace.Normal;
        label.AddToClassList("log-entry");
        switch (entry.Type)
        {
            case LogType.Warning:
                label.AddToClassList("log-warning");
                break;
            case LogType.Error:
            case LogType.Exception:
                label.AddToClassList("log-error");
                break;
        }

        _messageLog.Add(label);

        label.schedule.Execute(() => label.AddToClassList("log-entry-fade")).StartingIn((long)LogDisplayDurationMs);

        void OnFadeComplete(TransitionEndEvent evt)
        {
            if (evt.target == label)
            {
                label.UnregisterCallback<TransitionEndEvent>(OnFadeComplete);
                label.RemoveFromHierarchy();
            }
        }
        label.RegisterCallback<TransitionEndEvent>(OnFadeComplete);
    }

    private void Update()
    {
        if (_fpsLabel == null) return;

        _fpsAccumulator += Time.unscaledDeltaTime;
        _fpsFrameCount++;
        if (_fpsAccumulator >= 0.25f)
        {
            int fps = Mathf.RoundToInt(_fpsFrameCount / _fpsAccumulator);
            _fpsLabel.text = $"{fps} FPS";
            _fpsAccumulator = 0f;
            _fpsFrameCount = 0;
        }
    }
}
