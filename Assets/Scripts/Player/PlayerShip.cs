using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceShooter.Player
{
    /// <summary>
    /// Full 6-DOF space flight controller.
    ///
    /// Controls:
    ///   Space            — Forward thrust
    ///   W / S            — Pitch up / down  (like A/D for yaw)
    ///   A / D            — Yaw left / right
    ///   Q / E            — Roll left / right
    ///   Mouse X/Y        — Yaw & Pitch (fine aim)
    ///   Gamepad Left Stick  X/Y — Yaw & Pitch
    ///   Gamepad Right Trigger   — Forward thrust
    ///
    /// The ship moves in LOCAL space, so it flies wherever it's pointing.
    /// </summary>
    public class PlayerShip : MonoBehaviour
    {
        [Header("Thrust")]
        [Tooltip("Forward / backward speed")]
        [SerializeField] private float thrustSpeed = 25f;
        [Tooltip("Lateral (strafe) speed")]
        [SerializeField] private float strafeSpeed = 12f;
        [Tooltip("How quickly the ship accelerates toward input velocity")]
        [SerializeField] private float acceleration = 6f;
        [Tooltip("Maximum speed the ship can reach")]
        [SerializeField] private float maxSpeed = 60f;

        [Header("Rotation")]
        [Tooltip("Pitch & yaw sensitivity (degrees/sec)")]
        [SerializeField] private float pitchYawSpeed = 90f;
        [Tooltip("Roll sensitivity (degrees/sec)")]
        [SerializeField] private float rollSpeed = 75f;
        [Tooltip("Mouse sensitivity multiplier")]
        [SerializeField] private float mouseSensitivity = 0.5f;
        [Tooltip("How quickly the ship damps its angular velocity")]
        [SerializeField] private float rotationDamping = 8f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private Rigidbody _rb;
        private Vector3 _targetVelocity;
        private Vector3 _currentAngularInput; // pitch, yaw, roll per frame

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _rb.useGravity = false;
                _rb.linearDamping = 0f;  // No drag — inertia is preserved
                _rb.angularDamping = rotationDamping;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                // No rotation constraints — we handle rotation manually
                _rb.constraints = RigidbodyConstraints.None;
            }

            // Lock & hide cursor for mouse-look
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            ReadInput();
        }

        private void FixedUpdate()
        {
            ApplyThrust();
            ApplyRotation();
        }

        // ── Input ─────────────────────────────────────────────────────────────
        private float _thrust, _strafe, _vertical, _pitch, _yaw, _roll;

        private void ReadInput()
        {
            _thrust = 0f;
            _strafe = 0f;
            _vertical = 0f;
            _pitch = 0f;
            _yaw = 0f;
            _roll = 0f;

            // ── Keyboard ──────────────────────────────────────────────────────
            if (Keyboard.current != null)
            {
                // Forward thrust — Space only, no backward
                if (Keyboard.current.spaceKey.isPressed) _thrust += 1f;

                // Pitch — W/S
                if (Keyboard.current.wKey.isPressed) _pitch += 1f;  // W = pitch down
                if (Keyboard.current.sKey.isPressed) _pitch -= 1f;  // S = pitch up

                // Roll — A/D tilt left/right
                if (Keyboard.current.aKey.isPressed) _roll += 1f;  // A = tilt right
                if (Keyboard.current.dKey.isPressed) _roll -= 1f;  // D = tilt left
            }

            // ── Mouse ─────────────────────────────────────────────────────────
            if (Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                _pitch -= mouseDelta.y * mouseSensitivity;   // mouse up  = pitch up
                _yaw   += mouseDelta.x * mouseSensitivity;   // mouse right = yaw right
            }

            // ── Gamepad ───────────────────────────────────────────────────────
            if (Gamepad.current != null)
            {
                Vector2 leftStick = Gamepad.current.leftStick.ReadValue();
                Vector2 rightStick = Gamepad.current.rightStick.ReadValue();

                _pitch += leftStick.y * pitchYawSpeed * Time.deltaTime;
                _yaw += leftStick.x * pitchYawSpeed * Time.deltaTime;
                _strafe += rightStick.x * strafeSpeed;
                _vertical += rightStick.y * strafeSpeed;
                _thrust += Gamepad.current.rightTrigger.ReadValue(); // forward only
                _roll -= Gamepad.current.leftShoulder.isPressed  ? 1f : 0f;
                _roll += Gamepad.current.rightShoulder.isPressed ? 1f : 0f;
            }
        }

        // ── Physics ───────────────────────────────────────────────────────────
        private void ApplyThrust()
        {
            bool hasInput = (_thrust != 0f || _strafe != 0f || _vertical != 0f);

            if (hasInput)
            {
                // Desired velocity in LOCAL space based on current input
                Vector3 localDesired = new Vector3(
                    _strafe * strafeSpeed,
                    _vertical * strafeSpeed,
                    _thrust * thrustSpeed
                );

                // Convert desired to world space
                Vector3 worldDesired = transform.TransformDirection(localDesired);

                // Accelerate current velocity toward desired — inertia is preserved in the other axes
                _rb.linearVelocity = Vector3.MoveTowards(
                    _rb.linearVelocity,
                    worldDesired,
                    acceleration * thrustSpeed * Time.fixedDeltaTime
                );
            }
            // No input → do nothing; existing Rigidbody velocity carries the ship forward

            // Clamp to max speed
            if (_rb.linearVelocity.magnitude > maxSpeed)
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
        }

        private void ApplyRotation()
        {
            // Calculate torque from inputs. We no longer need Time.fixedDeltaTime 
            // since the physics engine handles integration when applying forces.
            Vector3 torque = new Vector3(
                _pitch * pitchYawSpeed,
                _yaw * pitchYawSpeed,
                _roll * rollSpeed
            );

            // Add relative torque (local space) to achieve heavy, physics-based drifting
            _rb.AddRelativeTorque(torque, ForceMode.Acceleration);
        }

        // ── Public API ────────────────────────────────────────────────────────
        public Vector2 MoveInput => new Vector2(_yaw, _pitch);
    }
}
