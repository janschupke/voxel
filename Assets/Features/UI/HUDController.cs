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

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument?.rootVisualElement != null)
        {
            _fpsLabel = uiDocument.rootVisualElement.Q<Label>("FPS");

            var worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
            _placementController = FindAnyObjectByType<ObjectPlacementController>();
            _selectionController = FindAnyObjectByType<SelectionController>();
            _removalController = FindAnyObjectByType<RemovalController>();
            if (_removalController == null && _placementController != null)
                _removalController = _placementController.gameObject.AddComponent<RemovalController>();

            SetupDebugControls(uiDocument.rootVisualElement, worldBootstrap);

            _removeButton = uiDocument.rootVisualElement.Q<Button>("Remove");
            if (_removeButton != null && _removalController != null)
            {
                _removeButton.clicked += () =>
                {
                    _removalController.ToggleRemovalMode();
                    UpdateRemoveButtonState();
                };
            }

            var generateButton = uiDocument.rootVisualElement.Q<Button>("Generate");
            if (generateButton != null && worldBootstrap != null)
                generateButton.clicked += worldBootstrap.RegenerateWorld;

            var exitButton = uiDocument.rootVisualElement.Q<Button>("Exit");
            if (exitButton != null)
                exitButton.clicked += () =>
                {
#if UNITY_EDITOR
                    EditorApplication.ExitPlaymode();
#else
                    Application.Quit();
#endif
                };

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
            _debugLogService = GetComponent<DebugLogService>();
            if (_debugLogService == null)
                _debugLogService = FindAnyObjectByType<DebugLogService>();
            if (_debugLogService == null)
                _debugLogService = gameObject.AddComponent<DebugLogService>();
            if (_messageLog != null && _debugLogService != null)
            {
                _debugLogService.LogReceived += OnLogReceived;
            }
        }
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
        bool show = worldBootstrap != null && worldBootstrap.ShowDebugControls;
        foreach (var el in root.Query(className: "debug-only").ToList())
        {
            if (show)
                el.RemoveFromClassList("hidden");
            else
                el.AddToClassList("hidden");
        }
    }

    private void ClearAllInventories(WorldBootstrap worldBootstrap)
    {
        if (worldBootstrap?.PlacedObjectRegistry == null) return;
        var registry = worldBootstrap.PlacedObjectRegistry;
        foreach (var entry in registry.Entries)
        {
            if (entry == null || entry.InventoryCapacity <= 0) continue;
            var parent = worldBootstrap.GetParentByEntryName(entry.Name);
            if (parent == null) continue;
            for (int i = 0; i < parent.childCount; i++)
            {
                var inv = parent.GetChild(i).GetComponent<BuildingInventory>();
                if (inv != null)
                    inv.ClearInventory();
            }
        }
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

    private void OnDestroy()
    {
        if (_debugLogService != null)
            _debugLogService.LogReceived -= OnLogReceived;
    }

    private void OnLogReceived(LogEntry entry)
    {
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
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            _placementController?.CancelPlacementMode();
            _removalController?.CancelRemovalMode();
            _selectionController?.ClearSelection();
            UpdateRemoveButtonState();
        }

        if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
        {
            GameDebugLogger.SetEnabled(!GameDebugLogger.IsEnabled);
            UpdateDebugButtonText();
        }

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
