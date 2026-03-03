using UnityEngine;
using UnityEngine.UIElements;
using Voxel;

public class HUDController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

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

            var generateButton = uiDocument.rootVisualElement.Q<Button>("Generate");
            if (generateButton != null)
            {
                var worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
                if (worldBootstrap != null)
                    generateButton.clicked += worldBootstrap.RegenerateWorld;
            }

            var houseButton = uiDocument.rootVisualElement.Q<Button>("House");
            if (houseButton != null)
            {
                var housePlacementController = FindAnyObjectByType<HousePlacementController>();
                if (housePlacementController != null)
                    houseButton.clicked += housePlacementController.TogglePlacementMode;
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
