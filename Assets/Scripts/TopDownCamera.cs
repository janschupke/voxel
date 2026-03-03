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
        private WorldScale _worldScale;
        private float _blocksVisible;
        private Vector2 _lastPanPosition;
        private InputAction _moveAction;

        private float Scale => _worldScale.BlockScale > 0f ? _worldScale.BlockScale : 1f;

        public void Initialize(VoxelGrid grid, WorldScale worldScale)
        {
            _grid = grid;
            _worldScale = worldScale.BlockScale > 0f ? worldScale : new WorldScale(1f);
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

        private void OnEnable() => inputActions?.Enable();
        private void OnDisable() => inputActions?.Disable();

        private void LateUpdate()
        {
            if (_camera == null) return;
            float dt = Time.deltaTime;
            HandleMoveInput(dt);
            HandlePanInput();
            HandleZoomInput();
            ApplyRotation();
            AdjustHeightForTerrain();
        }

        private void ApplyRotation() =>
            transform.rotation = Quaternion.Euler(90f - lookAngle, rotationYaw, 0f);

        private float HeightForBlocks(float blocks)
        {
            if (_worldScale.BlockScale <= 0f) return 0f;
            return blocks * _worldScale.BlockScale / (2f * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad));
        }

        private void GetHorizontalAxes(out Vector3 right, out Vector3 forward)
        {
            right = transform.right;
            right.y = 0f;
            right = right.sqrMagnitude < 0.01f ? Vector3.right : right.normalized;
            forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
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
                    float m = edgePanMargin;
                    if (pos.x < m) move.x -= 1f - pos.x / m;
                    else if (pos.x > viewSize.x - m) move.x += 1f - (viewSize.x - pos.x) / m;
                    if (pos.y < m) move.y -= 1f - pos.y / m;
                    else if (pos.y > viewSize.y - m) move.y += 1f - (viewSize.y - pos.y) / m;
                }
            }

            if (move.sqrMagnitude > 1f) move.Normalize();
            if (move.sqrMagnitude < 0.01f) return;

            float speed = (moveFromKeys.sqrMagnitude > 0.01f ? moveSpeed : edgePanSpeed) * Scale;
            GetHorizontalAxes(out Vector3 right, out Vector3 forward);
            transform.position += (right * move.x + forward * move.y) * (speed * dt);
        }

        private void HandlePanInput()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.rightButton.isPressed)
            {
                _lastPanPosition = default;
                return;
            }

            Vector2 pos = Mouse.current.position.ReadValue();
            if (_lastPanPosition != default)
            {
                Vector2 delta = pos - _lastPanPosition;
                GetHorizontalAxes(out Vector3 right, out Vector3 forward);
                Vector3 pan = -right * delta.x * panSensitivity * Scale - forward * delta.y * panSensitivity * Scale;
                transform.position += pan;
            }
            _lastPanPosition = pos;
        }

        private void HandleZoomInput()
        {
            if (Mouse.current == null || _worldScale.BlockScale <= 0f) return;
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            float notches = Mathf.Abs(scroll) >= 10f ? scroll / 120f : scroll;
            float delta = -notches * zoomSpeed * (_blocksVisible / 25f);
            if (Mathf.Abs(delta) < 0.5f) delta = Mathf.Sign(-scroll) * 0.5f;
            _blocksVisible = Mathf.Clamp(_blocksVisible + delta, minBlocksVisible, maxBlocksVisible);
        }

        private void AdjustHeightForTerrain()
        {
            if (_grid == null || _worldScale.BlockScale <= 0f) return;
            Vector3 pos = transform.position;
            pos.y = GetMaxTerrainHeightInView() + HeightForBlocks(_blocksVisible);
            transform.position = pos;
        }

        private float GetMaxTerrainHeightInView()
        {
            float dist = Vector3.Distance(transform.position, new Vector3(transform.position.x, 0f, transform.position.z));
            float halfHeight = dist * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * _camera.aspect;
            GetHorizontalAxes(out Vector3 right, out Vector3 forward);

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
                    var (gx, _, gz) = _worldScale.WorldToBlock(sample);

                    if (gx < 0 || gx >= _grid.Width || gz < 0 || gz >= _grid.Depth) continue;

                    for (int gy = _grid.Height - 1; gy >= 0; gy--)
                    {
                        if (_grid.IsSolid(gx, gy, gz))
                        {
                            float worldY = _worldScale.BlockToWorld(0, gy + 1, 0).y;
                            if (worldY > maxY) maxY = worldY;
                            break;
                        }
                    }
                }
            }
            return maxY;
        }

        public void FrameWorld(VoxelGrid grid, WorldScale worldScale)
        {
            if (grid == null || _camera == null) return;

            _worldScale = worldScale.BlockScale > 0f ? worldScale : new WorldScale(1f);
            var ws = _worldScale;
            float centerX = grid.Width * 0.5f * ws.BlockScale;
            float centerZ = grid.Depth * 0.5f * ws.BlockScale;
            float size = Mathf.Max(grid.Width, grid.Depth) * 0.5f * ws.BlockScale;
            float worldHeight = grid.Height * ws.BlockScale;

            _camera.orthographic = false;
            _camera.fieldOfView = fieldOfView;
            _camera.farClipPlane = Mathf.Max(2000f, size * 3f, worldHeight * 2f);

            _blocksVisible = (minBlocksVisible + maxBlocksVisible) * 0.5f;
            float heightAboveGround = ws.BlockScale > 0f ? HeightForBlocks(_blocksVisible) : size;

            Vector3 lookAt = new Vector3(centerX, worldHeight * 0.5f, centerZ);
            Vector3 forward = Quaternion.Euler(90f - lookAngle, rotationYaw, 0f) * Vector3.forward;
            transform.position = lookAt - forward * heightAboveGround;
            ApplyRotation();
        }
    }
}
