using UnityEngine;
using UnityEngine.UIElements;
using Voxel;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class HUDController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private PlacedObjectRegistry placedObjectRegistry;

    private Label _fpsLabel;
    private float _fpsAccumulator;
    private int _fpsFrameCount;

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
        }
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
