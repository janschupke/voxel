using UnityEngine;
using UnityEngine.InputSystem;
using Voxel.Core;

namespace Voxel
{
    /// <summary>
    /// Top-down 3D camera for voxel terrain. WASD/arrow movement, right-mouse panning,
    /// scroll zoom. Auto-adjusts camera height based on terrain in view.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TopDownCamera : MonoBehaviour
    {
        [SerializeField] private bool orthographic = true;
        [Tooltip("0 = up, 90 = horizontal, 180 = top-down (looking down).")]
        [Range(0f, 180f)]
        [SerializeField] private float lookAngle = 180f;
        [Tooltip("Yaw rotation in degrees (0 = south, 90 = west).")]
        [SerializeField] private float rotationYaw = 0f;
        [SerializeField] private float moveSpeed = 30f;
        [SerializeField] private float edgePanSpeed = 25f;
        [SerializeField] private float edgePanMargin = 20f;
        [SerializeField] private float panSensitivity = 0.15f;
        [SerializeField] private float zoomSpeed = 10f;
        [SerializeField] private float minOrthoSize = 20f;
        [SerializeField] private float maxOrthoSize = 500f;
        [SerializeField] private float minFov = 15f;
        [SerializeField] private float maxFov = 75f;
        [SerializeField] private float terrainClearance = 10f;
        [SerializeField] private int terrainSampleResolution = 8;

        private Camera _camera;
        private VoxelGrid _grid;
        private float _blockScale;
        private bool _isPanning;
        private Vector2 _lastPanPosition;

        /// <summary>
        /// Call after world is loaded to provide grid data for bounds and terrain sampling.
        /// </summary>
        public void Initialize(VoxelGrid grid, float blockScale)
        {
            _grid = grid;
            _blockScale = blockScale;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = orthographic;
        }


        private void LateUpdate()
        {
            if (_camera == null) return;

            float dt = Time.deltaTime;

            HandleMoveInput(dt);
            HandlePanInput(dt);
            HandleZoomInput(dt);
            ApplyRotation();
            AdjustHeightForTerrain();
        }

        private void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(90f - lookAngle, rotationYaw, 0f);
        }

        private void HandleMoveInput(float dt)
        {
            Vector2 move = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) move.y += 1f;
                if (Keyboard.current.sKey.isPressed) move.y -= 1f;
                if (Keyboard.current.aKey.isPressed) move.x -= 1f;
                if (Keyboard.current.dKey.isPressed) move.x += 1f;
                if (Keyboard.current.upArrowKey.isPressed) move.y += 1f;
                if (Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
                if (Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;
                if (Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
            }

            if (Mouse.current != null)
            {
                Vector2 pos = Mouse.current.position.ReadValue();
                Vector2 viewSize = new Vector2(Screen.width, Screen.height);
                if (pos.x >= 0 && pos.x < viewSize.x && pos.y >= 0 && pos.y < viewSize.y)
                {
                    float margin = edgePanMargin;
                    if (pos.x < margin) move.x -= 1f - pos.x / margin;
                    else if (pos.x > viewSize.x - margin) move.x += 1f - (viewSize.x - pos.x) / margin;
                    if (pos.y < margin) move.y -= 1f - pos.y / margin;
                    else if (pos.y > viewSize.y - margin) move.y += 1f - (viewSize.y - pos.y) / margin;
                }
            }

            if (move.sqrMagnitude > 1f) move.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            right.Normalize();

            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 motion = right * move.x + fwd * move.y;
            if (motion.sqrMagnitude > 0.01f)
            {
                motion.Normalize();
                float speed = (Keyboard.current != null && (move.x != 0f || move.y != 0f)) ? moveSpeed : edgePanSpeed;
                transform.position += motion * (speed * dt);
            }
        }

        private void HandlePanInput(float dt)
        {
            if (Mouse.current == null) return;

            _isPanning = Mouse.current.rightButton.isPressed;
            if (_isPanning)
            {
                Vector2 pos = Mouse.current.position.ReadValue();
                if (_lastPanPosition != default)
                {
                    Vector2 delta = pos - _lastPanPosition;
                    Vector3 right = transform.right;
                    right.y = 0f;
                    if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                    right.Normalize();
                    Vector3 fwd = transform.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                    fwd.Normalize();
                    Vector3 pan = -right * delta.x * panSensitivity - fwd * delta.y * panSensitivity;
                    transform.position += pan;
                }
                _lastPanPosition = pos;
            }
            else
            {
                _lastPanPosition = default;
            }
        }

        private void HandleZoomInput(float dt)
        {
            if (Mouse.current == null) return;

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            float delta = -scroll * zoomSpeed * 0.01f;
            if (_camera.orthographic)
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize + delta, minOrthoSize, maxOrthoSize);
            else
                _camera.fieldOfView = Mathf.Clamp(_camera.fieldOfView + delta, minFov, maxFov);
        }

        /// <summary>
        /// Samples terrain height in the visible area and raises the camera if needed
        /// so terrain doesn't clip through the view.
        /// </summary>
        private void AdjustHeightForTerrain()
        {
            if (_grid == null) return;

            float maxTerrainY = GetMaxTerrainHeightInView();
            float minCameraY = maxTerrainY + terrainClearance;

            Vector3 pos = transform.position;
            if (pos.y < minCameraY)
            {
                pos.y = minCameraY;
                transform.position = pos;
            }
        }

        /// <summary>
        /// Samples the grid at points in the camera's view bounds to find max solid block height.
        /// </summary>
        private float GetMaxTerrainHeightInView()
        {
            float halfHeight, halfWidth;
            if (_camera.orthographic)
            {
                halfHeight = _camera.orthographicSize;
                halfWidth = halfHeight * _camera.aspect;
            }
            else
            {
                float dist = Vector3.Distance(transform.position, new Vector3(transform.position.x, 0f, transform.position.z));
                halfHeight = dist * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                halfWidth = halfHeight * _camera.aspect;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            right.Normalize();

            Vector3 center = transform.position;
            center.y = 0f;

            float maxY = 0f;
            int n = Mathf.Max(2, terrainSampleResolution);

            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j <= n; j++)
                {
                    float u = (i / (float)n - 0.5f) * 2f;
                    float v = (j / (float)n - 0.5f) * 2f;
                    Vector3 sample = center + right * (u * halfWidth) + forward * (v * halfHeight);

                    int gx = Mathf.FloorToInt(sample.x / _blockScale);
                    int gz = Mathf.FloorToInt(sample.z / _blockScale);

                    if (gx < 0 || gx >= _grid.Width || gz < 0 || gz >= _grid.Depth)
                        continue;

                    for (int gy = _grid.Height - 1; gy >= 0; gy--)
                    {
                        if (_grid.IsSolid(gx, gy, gz))
                        {
                            float worldY = (gy + 1) * _blockScale;
                            if (worldY > maxY) maxY = worldY;
                            break;
                        }
                    }
                }
            }

            return maxY;
        }

        /// <summary>
        /// Sets the initial position and zoom to frame the world. Call from WorldBootstrap.
        /// </summary>
        public void FrameWorld(VoxelGrid grid, float blockScale)
        {
            if (grid == null || _camera == null) return;

            float centerX = grid.Width * 0.5f * blockScale;
            float centerZ = grid.Depth * 0.5f * blockScale;
            float size = Mathf.Max(grid.Width, grid.Depth) * 0.5f * blockScale;
            float worldHeight = grid.Height * blockScale;

            _camera.orthographic = orthographic;
            float farClip = Mathf.Max(2000f, size * 3f, worldHeight * 2f);
            _camera.farClipPlane = farClip;

            Vector3 lookAt = new Vector3(centerX, worldHeight * 0.5f, centerZ);
            Vector3 forward = Quaternion.Euler(90f - lookAngle, rotationYaw, 0f) * Vector3.forward;
            transform.position = lookAt - forward * size;
            ApplyRotation();

            if (_camera.orthographic)
                _camera.orthographicSize = Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
            else
                _camera.fieldOfView = Mathf.Clamp(50f, minFov, maxFov);
        }
    }
}
