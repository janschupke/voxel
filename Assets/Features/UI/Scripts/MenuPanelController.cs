using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Voxel
{
    /// <summary>
    /// Controls the Menu overlay panel with Generate, Save, Exit, Hotkeys. Registers with PanelManager.
    /// </summary>
    public class MenuPanelController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private WorldBootstrap worldBootstrap;

        private VisualElement _menuPanel;

        private void Start()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
            if (worldBootstrap == null)
                worldBootstrap = FindAnyObjectByType<WorldBootstrap>();

            if (uiDocument?.rootVisualElement == null) return;

            _menuPanel = uiDocument.rootVisualElement.Q<VisualElement>("MenuPanel");
            if (_menuPanel == null) return;

            if (PanelManager.Instance != null)
                PanelManager.Instance.RegisterPanel(PanelManager.PanelMenu, _menuPanel, null, null);

            var generateButton = uiDocument.rootVisualElement.Q<Button>("MenuGenerate");
            if (generateButton != null && worldBootstrap != null)
                generateButton.clicked += worldBootstrap.RegenerateWorld;

            var saveButton = uiDocument.rootVisualElement.Q<Button>("MenuSave");
            if (saveButton != null && worldBootstrap != null)
                saveButton.clicked += worldBootstrap.SaveWorld;

            var exitButton = uiDocument.rootVisualElement.Q<Button>("MenuExit");
            if (exitButton != null)
                exitButton.clicked += () =>
                {
#if UNITY_EDITOR
                    EditorApplication.ExitPlaymode();
#else
                    Application.Quit();
#endif
                };

            var hotkeysButton = uiDocument.rootVisualElement.Q<Button>("MenuHotkeys");
            if (hotkeysButton != null)
                hotkeysButton.clicked += () => PanelManager.Instance?.OpenPanel(PanelManager.PanelHotkeys);
        }
    }
}
