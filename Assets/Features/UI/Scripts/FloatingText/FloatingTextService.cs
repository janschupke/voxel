using UnityEngine;
using UnityEngine.UI;

namespace Voxel
{
    /// <summary>
    /// Manages floating text globally. Subscribes to WorldObjectEventBus, spawns instances for UnitProduced events.
    /// </summary>
    public class FloatingTextService : MonoBehaviour
    {
        [SerializeField] private GameObject floatingTextPrefab;
        [SerializeField] private ItemRegistry itemRegistry;
        [SerializeField] private WorldBootstrap worldBootstrap;
        private IItemRegistry _itemRegistry;

        private Canvas _canvas;
        private float _blockScale = 1f;

        private void Start()
        {
            if (worldBootstrap == null)
                worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
            _itemRegistry = itemRegistry ?? worldBootstrap?.ItemRegistry;

            _blockScale = worldBootstrap?.WorldParameters != null
                ? worldBootstrap.WorldParameters.BlockScale
                : 1f;

            EnsureCanvas();

            WorldObjectEventBus.OnEvent += OnWorldObjectEvent;
        }

        private void OnDestroy()
        {
            WorldObjectEventBus.OnEvent -= OnWorldObjectEvent;
        }

        private void OnWorldObjectEvent(WorldObjectEvent evt)
        {
            if (evt.Source == null) return;

            if (evt.EventType == WorldObjectEventTypes.UnitProduced)
            {
                if (evt.Data is (Item item, int newCount) && newCount > 0)
                {
                    var itemName = _itemRegistry?.GetDefinition(item)?.Name ?? item.ToString();
                    var text = $"{itemName}: {newCount}";
                    ShowAt(evt.Source, text);
                }
            }
        }

        public void ShowAt(Transform source, string text)
        {
            if (source == null) return;
            EnsureCanvas();
            if (_canvas == null) return;

            GameObject instance;
            if (floatingTextPrefab != null)
            {
                instance = Instantiate(floatingTextPrefab, _canvas.transform);
            }
            else
            {
                instance = CreateDefaultTextObject();
                instance.transform.SetParent(_canvas.transform, false);
            }

            var ft = instance.GetComponent<FloatingTextInstance>();
            if (ft != null)
                ft.Initialize(source, text, _canvas);
        }

        private GameObject CreateDefaultTextObject()
        {
            var go = new GameObject("FloatingText");
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 40);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var text = go.AddComponent<Text>();
            text.text = "0";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = new Color(0.3f, 0.9f, 0.3f, 1f);
            text.alignment = TextAnchor.MiddleCenter;

            var ft = go.AddComponent<FloatingTextInstance>();
            return go;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;

            {
                var go = new GameObject("FloatingTextCanvas");
                go.transform.SetParent(transform);
                _canvas = go.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 100;

                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                go.AddComponent<GraphicRaycaster>();
            }
        }
    }
}

