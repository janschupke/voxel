using UnityEngine;
using UnityEngine.InputSystem;

namespace Voxel
{
    public class FreeFlyCamera : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private float moveSpeed = 50f;
        [SerializeField] private float sprintMultiplier = 3f;
        [SerializeField] private float lookSensitivity = 0.2f;
        [SerializeField] private bool invertY;
        [SerializeField] private bool lockCursor = true;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _crouchAction;
        private InputAction _sprintAction;

        private float _pitch;
        private float _yaw;

        private void Awake()
        {
            if (inputActions == null) return;

            var map = inputActions.FindActionMap("Player");
            _moveAction = map.FindAction("Move");
            _lookAction = map.FindAction("Look");
            _jumpAction = map.FindAction("Jump");
            _crouchAction = map.FindAction("Crouch");
            _sprintAction = map.FindAction("Sprint");
        }

        private void OnEnable()
        {
            inputActions?.Enable();
            if (lockCursor)
                Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnDisable()
        {
            inputActions?.Disable();
            if (lockCursor)
                Cursor.lockState = CursorLockMode.None;
        }

        private void Start()
        {
            var euler = transform.eulerAngles;
            _pitch = euler.x;
            _yaw = euler.y;

            var cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.orthographic = false;
                cam.fieldOfView = 60f;
            }
        }

        private void LateUpdate()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.None;

            if (inputActions == null) return;

            float dt = Time.deltaTime;
            float speed = moveSpeed * (_sprintAction?.IsPressed() == true ? sprintMultiplier : 1f);

            Vector2 move = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            float vertical = 0f;
            if (_jumpAction?.IsPressed() == true) vertical += 1f;
            if (_crouchAction?.IsPressed() == true) vertical -= 1f;

            Vector3 motion = transform.right * move.x + transform.forward * move.y + Vector3.up * vertical;
            if (motion.sqrMagnitude > 1f) motion.Normalize();
            transform.position += motion * (speed * dt);

            Vector2 look = _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
            _yaw += look.x * lookSensitivity;
            _pitch += (invertY ? 1f : -1f) * look.y * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}
