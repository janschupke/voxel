using UnityEngine;
using UnityEngine.InputSystem;
using Voxel.Core;

namespace Voxel
{
    /// <summary>
    /// Top-down 3D perspective camera for voxel terrain. WASD/arrow movement, right-mouse panning,
    /// scroll zoom. Zoom by height (10–40 blocks visible). Auto-adjusts camera height from terrain.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TopDownCamera : MonoBehaviour
    {
        [Tooltip("0 = up, 90 = horizontal, 180 = top-down (looking down).")]
        [Range(0f, 180f)]
        [SerializeField] private float lookAngle = 180f;
        [Tooltip("Yaw rotation in degrees (0 = south, 90 = west).")]
        [SerializeField] private float rotationYaw = 0f;
        [Tooltip("Blocks per second when using WASD/arrows.")]
        [SerializeField] private float moveSpeed = 2f;
        [Tooltip("Blocks per second when edge panning.")]
        [SerializeField] private float edgePanSpeed = 1.5f;
        [SerializeField] private float edgePanMargin = 20f;
        [Tooltip("Blocks per pixel when right-drag panning.")]
        [SerializeField] private float panSensitivity = 0.01f;
        [Tooltip("Blocks per scroll notch (scales with zoom level).")]
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minBlocksVisible = 10f;
        [SerializeField] private float maxBlocksVisible = 40f;
        [SerializeField] private float fieldOfView = 50f;
        [Tooltip("Input actions (e.g. InputSystem_Actions). Uses Player/Move. Required for keyboard/gamepad movement.")]
        [SerializeField] private InputActionAsset inputActions;

        private Camera _camera;
        private VoxelGrid _grid;
        private float _blockScale;
        private float _blocksVisible;
        private Vector2 _lastPanPosition;
        private InputAction _moveAction;

        /// <summary>
        /// Call after world is loaded to provide grid data for bounds and terrain sampling.
        /// </summary>
        public void Initialize(VoxelGrid grid, float blockScale)
        {
            _grid = grid;
            _blockScale = blockScale;
            if (_blocksVisible < minBlocksVisible || _blocksVisible > maxBlocksVisible)
                _blocksVisible = (minBlocksVisible + maxBlocksVisible) * 0.5f;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = false;
            _camera.fieldOfView = fieldOfView;
            if (inputActions != null)
                _moveAction = inputActions.FindActionMap("Player")?.FindAction("Move");
        }

        private void OnEnable()
        {
            inputActions?.Enable();
        }

        private void OnDisable()
        {
            inputActions?.Disable();
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

        private float HeightForBlocks(float blocks)
        {
            if (_blockScale <= 0f) return 0f;
            float halfFovRad = fieldOfView * 0.5f * Mathf.Deg2Rad;
            return blocks * _blockScale / (2f * Mathf.Tan(halfFovRad));
        }

        private void HandleMoveInput(float dt)
        {
            Vector2 moveFromKeys = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            Vector2 move = moveFromKeys;

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

            float scale = _blockScale > 0f ? _blockScale : 1f;
            float targetSpeed = (move.sqrMagnitude > 0.01f)
                ? (moveFromKeys.sqrMagnitude > 0.01f ? moveSpeed : edgePanSpeed) * scale
                : 0f;

            Vector2 velocity = move.sqrMagnitude > 0.01f ? move.normalized * targetSpeed : Vector2.zero;
            if (velocity.sqrMagnitude < 0.01f) return;

            Vector3 right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            right.Normalize();

            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 motion = right * velocity.x + fwd * velocity.y;
            transform.position += motion * dt;
        }

        private void HandlePanInput(float dt)
        {
            if (Mouse.current == null) return;

            if (Mouse.current.rightButton.isPressed)
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
                    float scale = _blockScale > 0f ? _blockScale : 1f;
                    Vector3 pan = -right * delta.x * panSensitivity * scale - fwd * delta.y * panSensitivity * scale;
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
            if (Mouse.current == null || _blockScale <= 0f) return;

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            // Normalize: some devices report 1 per notch, others 120
            float notches = Mathf.Abs(scroll) >= 10f ? scroll / 120f : scroll;
            float delta = -notches * zoomSpeed * (_blocksVisible / 25f);
            // Ensure minimum step so scroll never feels dead
            if (Mathf.Abs(delta) < 0.5f) delta = Mathf.Sign(-scroll) * 0.5f;
            _blocksVisible = Mathf.Clamp(_blocksVisible + delta, minBlocksVisible, maxBlocksVisible);
        }

        /// <summary>
        /// Sets camera height from zoom level and terrain.
        /// </summary>
        private void AdjustHeightForTerrain()
        {
            if (_grid == null || _blockScale <= 0f) return;

            float maxTerrainY = GetMaxTerrainHeightInView();
            float zoomHeight = HeightForBlocks(_blocksVisible);
            float targetY = maxTerrainY + zoomHeight;

            Vector3 pos = transform.position;
            pos.y = targetY;
            transform.position = pos;
        }

        /// <summary>
        /// Samples the grid at points in the camera's view bounds to find max solid block height.
        /// </summary>
        private float GetMaxTerrainHeightInView()
        {
            float dist = Vector3.Distance(transform.position, new Vector3(transform.position.x, 0f, transform.position.z));
            float halfHeight = dist * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * _camera.aspect;

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
            const int n = 8;

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

            _camera.orthographic = false;
            _camera.fieldOfView = fieldOfView;
            float farClip = Mathf.Max(2000f, size * 3f, worldHeight * 2f);
            _camera.farClipPlane = farClip;

            _blocksVisible = (minBlocksVisible + maxBlocksVisible) * 0.5f;
            float heightAboveGround = blockScale > 0f
                ? _blocksVisible * blockScale / (2f * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad))
                : size;

            Vector3 lookAt = new Vector3(centerX, worldHeight * 0.5f, centerZ);
            Vector3 forward = Quaternion.Euler(90f - lookAngle, rotationYaw, 0f) * Vector3.forward;
            transform.position = lookAt - forward * heightAboveGround;
            ApplyRotation();
        }
    }
}
