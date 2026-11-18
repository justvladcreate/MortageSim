using DG.Tweening;
using MortgageSim.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MortgageSim.Player
{
    /// <summary>Контроллер от первого лица на новой Input System.</summary>
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        public Camera PlayerCamera { get; private set; }
        public CharacterController Controller { get; private set; }

        private InputAction _moveAction;
        private InputAction _lookAction;
        private float _yaw;
        private float _pitch;
        private float _verticalVelocity;
        private float _bob;
        private float _stepDistance;
        private bool _inputEnabled = true;
        private bool _canLook = true;
        private bool _cursorLocked = true;
        private const float EyeHeight = 1.66f;
        private const float MouseSensitivity = 0.075f;

        public void Initialize(Camera cam)
        {
            PlayerCamera = cam;
            Controller = GetComponent<CharacterController>();
            if (Controller == null)
            {
                Controller = gameObject.AddComponent<CharacterController>();
                Controller.height = 1.8f;
                Controller.radius = 0.3f;
                Controller.center = new Vector3(0f, 0.9f, 0f);
                Controller.stepOffset = 0.25f;
                Controller.slopeLimit = 45f;
            }

            _yaw = transform.eulerAngles.y;
            _pitch = 0f;

            PlayerCamera.transform.SetParent(transform, false);
            PlayerCamera.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            PlayerCamera.transform.localRotation = Quaternion.identity;

            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.Enable();

            _lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");
            _lookAction.Enable();

            // Лёгкое «дыхание» камеры, чтобы кадр не был мёртвым.
            DOTween.To(() => 0f, v => _bob = v, 1f, 2.6f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
            if (!enabled)
            {
                _verticalVelocity = 0f;
            }
        }

        public void SetCursorLocked(bool locked)
        {
            _cursorLocked = locked;
            _canLook = locked && _inputEnabled;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void Teleport(Vector3 position, float yaw)
        {
            if (Controller != null) Controller.enabled = false;
            transform.position = position;
            if (Controller != null) Controller.enabled = true;
            _yaw = yaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _pitch = 0f;
            PlayerCamera.transform.localRotation = Quaternion.identity;
        }

        public void SnapCameraToPlayer()
        {
            PlayerCamera.transform.SetParent(transform, false);
            PlayerCamera.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            PlayerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

            if (_inputEnabled && _canLook && _cursorLocked)
            {
                Vector2 look = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
                _yaw += look.x * MouseSensitivity;
                _pitch -= look.y * MouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, -82f, 82f);
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                PlayerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            if (_inputEnabled && Controller != null && Controller.enabled)
            {
                Vector2 move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
                Vector3 direction = transform.right * move.x + transform.forward * move.y;
                if (direction.sqrMagnitude > 1f) direction.Normalize();

                if (Controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
                _verticalVelocity += -19.6f * Time.deltaTime;

                Vector3 velocity = direction * GameBalance.PlayerSpeed + Vector3.up * _verticalVelocity;
                Controller.Move(velocity * Time.deltaTime);

                Vector3 flatVelocity = Controller.velocity;
                flatVelocity.y = 0f;
                if (Controller.isGrounded && flatVelocity.magnitude > 0.6f)
                {
                    _stepDistance += flatVelocity.magnitude * Time.deltaTime;
                    if (_stepDistance >= 2.1f)
                    {
                        _stepDistance = 0f;
                        GameManager.Instance?.Audio?.Footstep();
                    }
                }
                else
                {
                    _stepDistance = Mathf.Min(_stepDistance, 1.6f);
                }
            }

            // Пока камера «прилипла» к монитору, её нельзя трогать из контроллера.
            if (PlayerCamera != null && PlayerCamera.transform.parent == transform)
            {
                Vector3 local = PlayerCamera.transform.localPosition;
                local.y = EyeHeight + _bob * 0.012f;
                PlayerCamera.transform.localPosition = local;
            }
        }
    }
}
