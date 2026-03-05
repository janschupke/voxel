using UnityEngine;
using UnityEngine.UI;

namespace Voxel
{
    /// <summary>
    /// Single floating text instance: rises in world space, projects to screen each frame, fades out.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Text))]
    public class FloatingTextInstance : MonoBehaviour
    {
        [SerializeField] private float duration = 1f;
        [SerializeField] private float riseSpeedWorldUnitsPerSec = 1.5f;
        [SerializeField] private float heightOffset = 1.5f;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _text = GetComponent<Text>();
        }

        private Transform _source;
        private RectTransform _rectTransform;
        private Text _text;
        private Canvas _parentCanvas;
        private Camera _cachedCamera;
        private Vector3 _worldPos;
        private float _elapsed;

        public void Initialize(Transform source, string displayText, Canvas parentCanvas)
        {
            _source = source;
            _parentCanvas = parentCanvas;
            _cachedCamera = Camera.main;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_text == null) _text = GetComponent<Text>();

            if (_text != null)
                _text.text = displayText;

            _worldPos = _source != null
                ? _source.position + Vector3.up * heightOffset
                : Vector3.zero;
            _elapsed = 0f;
        }

        private void Update()
        {
            if (_parentCanvas == null || _rectTransform == null)
            {
                Destroy(gameObject);
                return;
            }

            _elapsed += Time.deltaTime;

            if (_elapsed >= duration)
            {
                Destroy(gameObject);
                return;
            }

            if (_source != null)
                _worldPos += Vector3.up * (riseSpeedWorldUnitsPerSec * Time.deltaTime);

            if (_cachedCamera == null)
                _cachedCamera = Camera.main;
            if (_cachedCamera == null) return;

            var screenPoint = _cachedCamera.WorldToScreenPoint(_worldPos);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentCanvas.GetComponent<RectTransform>(),
                screenPoint,
                _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _cachedCamera,
                out var localPoint))
            {
                _rectTransform.anchoredPosition = localPoint;
            }

            if (_text != null)
            {
                var alpha = 1f - (_elapsed / duration);
                var c = _text.color;
                _text.color = new Color(c.r, c.g, c.b, alpha);
            }
        }
    }
}
