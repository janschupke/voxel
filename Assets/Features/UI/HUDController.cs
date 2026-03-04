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

    private Label _fpsLabel;
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

            var placementContainer = uiDocument.rootVisualElement.Q<VisualElement>("PlacementButtons");
            var objectPlacementController = FindAnyObjectByType<ObjectPlacementController>();

            if (placementContainer != null && objectPlacementController != null)
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

                        objectPlacementController.RegisterButton(entry.Name, button);
                        var entryName = entry.Name;
                        button.clicked += () => objectPlacementController.TogglePlacementMode(entryName);
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
            var placementController = FindAnyObjectByType<ObjectPlacementController>();
            var selectionController = FindAnyObjectByType<SelectionController>();
            placementController?.CancelPlacementMode();
            selectionController?.ClearSelection();
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
