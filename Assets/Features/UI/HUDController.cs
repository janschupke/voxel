using UnityEngine;
using UnityEngine.UIElements;
using Voxel;

public class HUDController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument?.rootVisualElement != null)
        {
            var generateButton = uiDocument.rootVisualElement.Q<Button>("Generate");
            if (generateButton != null)
            {
                var worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
                if (worldBootstrap != null)
                    generateButton.clicked += worldBootstrap.RegenerateWorld;
            }
        }
    }
}
