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
        [Tooltip("How quickly the ship slows down over time (drag)")]
        [SerializeField] private float movementDrag = 2.5f;
        [Tooltip("Maximum speed the ship can reach")]
        [SerializeField] private float maxSpeed = 60f;

        [Header("Rotation")]
        [Tooltip("Max pitch & yaw speed (degrees/sec)")]
        [SerializeField] private float pitchYawSpeed = 90f;
        [Tooltip("Max roll speed (degrees/sec)")]
        [SerializeField] private float rollSpeed = 75f;
        [Tooltip("Mouse sensitivity multiplier")]
        [SerializeField] private float mouseSensitivity = 0.5f;

        [Header("Rotation Dynamics")]
        [Tooltip("How fast the ship accelerates into a spin (degrees/sec^2). Higher = snappier attack.")]
        [SerializeField] private float rotationAcceleration = 360f;
        [Tooltip("How quickly the ship loses angular velocity (stops spinning) when there is no input. Lower = more sustain/drift. Higher = quicker stop.")]
        [SerializeField] private float rotationDrag = 8f;

        [Header("Visuals (Optional)")]
        [Tooltip("Child transform containing the ship mesh to apply sway to")]
        [SerializeField] private Transform shipModel;
        [Tooltip("Amount of visual tilt when strafing horizontally")]
        [SerializeField] private float swayAmount = 15f;
        [Tooltip("Amount of visual bank when turning left/right (yaw bank)")]
        [SerializeField] private float turnSwayMultiplier = 10f;
        [Tooltip("How fast the sway interpolates")]
        [SerializeField] private float swaySpeed = 5f;

        [Header("Thruster Visuals")]
        [Tooltip("Renderers for the thruster flames")]
        [SerializeField] private Renderer[] thrusterRenderers;
        [Tooltip("The base emission color of the thruster")]
        [ColorUsage(false, true)][SerializeField] private Color thrusterBaseColor = Color.cyan;
        [Tooltip("Emission intensity when idle")]
        [SerializeField] private float idleEmissionMultiplier = 0.5f;
        [Tooltip("Emission intensity when strafing/rotating/rolling")]
        [SerializeField] private float moveEmissionMultiplier = 5f;
        [Tooltip("Emission intensity when throttling forward")]
        [SerializeField] private float thrustEmissionMultiplier = 20f;
        [Tooltip("How fast the emission changes")]
        [SerializeField] private float emissionLerpSpeed = 10f;

        [Tooltip("Particle systems on thruster nozzles")]
        [SerializeField] private ParticleSystem[] thrusterParticles;

        [Header("Particle Lifetime")]
        [Tooltip("Particle lifetime when idle")]
        [SerializeField] private float idleParticleLifetime = 0.05f;
        [Tooltip("Particle lifetime when strafing/rotating/rolling")]
        [SerializeField] private float moveParticleLifetime = 0.2f;
        [Tooltip("Particle lifetime when throttling")]
        [SerializeField] private float thrustParticleLifetime = 0.6f;

        [Header("Particle Alpha")]
        [Tooltip("Particle material alpha when idle")]
        [SerializeField] private float idleParticleAlpha = 0.05f;
        [Tooltip("Particle material alpha when strafing/rotating/rolling")]
        [SerializeField] private float moveParticleAlpha = 0.4f;
        [Tooltip("Particle material alpha when throttling")]
        [SerializeField] private float thrustParticleAlpha = 1f;

        [Tooltip("How fast the particle visuals transition")]
        [SerializeField] private float particleLerpSpeed = 10f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private Rigidbody _rb;
        private Quaternion _initialModelRot;
        private Vector3 _targetVelocity;
        private Vector3 _currentAngularInput; // pitch, yaw, roll per frame
        private Material[] _thrusterMaterials;
        private float _currentEmissionIntensity;
        private float _currentParticleLifetime;
        private float _currentParticleAlpha;
        private Material[] _particleMaterials;

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _rb.useGravity = false;
                _rb.linearDamping = movementDrag;  // Applies atmospheric drag to smooth out sliding
                _rb.angularDamping = rotationDrag;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                // No rotation constraints — we handle rotation manually
                _rb.constraints = RigidbodyConstraints.None;
            }

            // Lock & hide cursor for mouse-look
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Auto-assign ship model if empty
            if (shipModel == null)
            {
                var renderer = GetComponentInChildren<MeshRenderer>();
                if (renderer != null && renderer.transform != this.transform)
                    shipModel = renderer.transform;
            }

            if (shipModel != null)
                _initialModelRot = shipModel.localRotation;

            if (thrusterRenderers != null && thrusterRenderers.Length > 0)
            {
                _thrusterMaterials = new Material[thrusterRenderers.Length];
                for (int i = 0; i < thrusterRenderers.Length; i++)
                {
                    if (thrusterRenderers[i] != null)
                    {
                        Material[] mats = thrusterRenderers[i].materials;
                        if (mats.Length > 1)
                        {
                            _thrusterMaterials[i] = mats[1];
                            _thrusterMaterials[i].EnableKeyword("_EMISSION");
                        }
                    }
                }
            }

            // Cache particle system materials for alpha control
            if (thrusterParticles != null && thrusterParticles.Length > 0)
            {
                _particleMaterials = new Material[thrusterParticles.Length];
                for (int i = 0; i < thrusterParticles.Length; i++)
                {
                    if (thrusterParticles[i] != null)
                    {
                        var psRenderer = thrusterParticles[i].GetComponent<ParticleSystemRenderer>();
                        if (psRenderer != null)
                            _particleMaterials[i] = psRenderer.material;
                    }
                }
            }

            // Start at idle values so there's no ramp-from-zero on the first frame
            _currentEmissionIntensity = idleEmissionMultiplier;
            _currentParticleLifetime = idleParticleLifetime;
            _currentParticleAlpha = idleParticleAlpha;
        }

        private void Update()
        {
            ReadInput();
            ApplySway();
            UpdateThrusterVisuals();
        }

        private void ApplySway()
        {
            if (shipModel == null || _rb == null) return;

            // 1. Strafe right (strafe > 0) -> roll right (negative Z)
            float strafeBank = -_strafe * swayAmount;

            // 2. Yaw right (local angular velocity Y > 0) -> bank right (negative Z)
            float localYawVelocity = transform.InverseTransformDirection(_rb.angularVelocity).y;
            float turnBank = -localYawVelocity * turnSwayMultiplier;

            // Combine the two banks seamlessly, clamped to prevent extreme flipping if they max out both!
            float totalTargetRoll = Mathf.Clamp(strafeBank + turnBank, -70f, 70f);

            Quaternion targetRotation = _initialModelRot * Quaternion.Euler(0f, 0f, totalTargetRoll);
            shipModel.localRotation = Quaternion.Slerp(shipModel.localRotation, targetRotation, Time.deltaTime * swaySpeed);
        }

        private void UpdateThrusterVisuals()
        {
            bool isThrusting = _thrust > 0.01f;
            bool isMovingWithoutThrust = Mathf.Abs(_strafe) > 0.01f || Mathf.Abs(_vertical) > 0.01f ||
                                         Mathf.Abs(_stickPitch) > 0.01f || Mathf.Abs(_stickYaw) > 0.01f || Mathf.Abs(_stickRoll) > 0.01f ||
                                         Mathf.Abs(_mousePitchAccumulated) > 0.01f || Mathf.Abs(_mouseYawAccumulated) > 0.01f;

            // ── Thruster particles (lifetime + alpha) ──────────────────────
            if (thrusterParticles != null && thrusterParticles.Length > 0)
            {
                // Pick target based on 3 states
                float targetLifetime = idleParticleLifetime;
                float targetAlpha = idleParticleAlpha;
                if (isThrusting)
                {
                    targetLifetime = thrustParticleLifetime;
                    targetAlpha = thrustParticleAlpha;
                }
                else if (isMovingWithoutThrust)
                {
                    targetLifetime = moveParticleLifetime;
                    targetAlpha = moveParticleAlpha;
                }

                _currentParticleLifetime = Mathf.Lerp(_currentParticleLifetime, targetLifetime, Time.deltaTime * particleLerpSpeed);
                _currentParticleAlpha = Mathf.Lerp(_currentParticleAlpha, targetAlpha, Time.deltaTime * particleLerpSpeed);

                foreach (var ps in thrusterParticles)
                {
                    if (ps == null) continue;
                    var main = ps.main;
                    main.startLifetime = _currentParticleLifetime;
                }

                if (_particleMaterials != null)
                {
                    foreach (var mat in _particleMaterials)
                    {
                        if (mat == null) continue;
                        Color c = mat.color;
                        c.a = _currentParticleAlpha;
                        mat.color = c;
                    }
                }
            }

            // ── Emission color ───────────────────────────────────────────────
            if (_thrusterMaterials == null || _thrusterMaterials.Length == 0) return;

            float targetEmission = idleEmissionMultiplier;
            if (isThrusting) targetEmission = thrustEmissionMultiplier;
            else if (isMovingWithoutThrust) targetEmission = moveEmissionMultiplier;

            _currentEmissionIntensity = Mathf.Lerp(_currentEmissionIntensity, targetEmission, Time.deltaTime * emissionLerpSpeed);

            foreach (var mat in _thrusterMaterials)
            {
                if (mat != null)
                {
                    mat.SetColor("_EmissionColor", thrusterBaseColor * _currentEmissionIntensity);
                }
            }
        }

        private void FixedUpdate()
        {
            ApplyThrust();
            ApplyRotation();
        }

        // ── Input ─────────────────────────────────────────────────────────────
        private float _thrust, _strafe, _vertical;

        // Continuous input axes (Keyboard, Gamepad stick)
        private float _stickPitch, _stickYaw, _stickRoll;

        // Accumulated mouse deltas (to sync Update with FixedUpdate without jitter)
        private float _mousePitchAccumulated, _mouseYawAccumulated;

        private void ReadInput()
        {
            _thrust = 0f;
            _strafe = 0f;
            _vertical = 0f;
            _stickPitch = 0f;
            _stickYaw = 0f;
            _stickRoll = 0f;

            // ── Keyboard ──────────────────────────────────────────────────────
            if (Keyboard.current != null)
            {
                // Forward thrust — Space only, no backward
                if (Keyboard.current.spaceKey.isPressed) _thrust += 1f;

                // Pitch — W/S
                if (Keyboard.current.wKey.isPressed) _stickPitch += 1f;  // W = pitch down
                if (Keyboard.current.sKey.isPressed) _stickPitch -= 1f;  // S = pitch up

                // Roll — A/D tilt left/right
                if (Keyboard.current.aKey.isPressed) _stickRoll += 1f;  // A = tilt right
                if (Keyboard.current.dKey.isPressed) _stickRoll -= 1f;  // D = tilt left
            }

            // ── Mouse ─────────────────────────────────────────────────────────
            if (Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                // Accumulate mouse deltas to apply them perfectly in fixed intervals without losing input.
                // Scaled slightly so standard sensitivity feels reasonable against stick input.
                _mousePitchAccumulated -= mouseDelta.y * mouseSensitivity * 0.1f;
                _mouseYawAccumulated += mouseDelta.x * mouseSensitivity * 0.1f;
            }

            // ── Gamepad ───────────────────────────────────────────────────────
            if (Gamepad.current != null)
            {
                Vector2 leftStick = Gamepad.current.leftStick.ReadValue();
                Vector2 rightStick = Gamepad.current.rightStick.ReadValue();

                _stickPitch -= rightStick.y;
                _stickYaw += rightStick.x;
                _strafe += leftStick.x;
                _vertical += leftStick.y;
                _thrust += Gamepad.current.rightTrigger.ReadValue(); // forward only
                _stickRoll += Gamepad.current.leftShoulder.isPressed ? 1f : 0f;
                _stickRoll -= Gamepad.current.rightShoulder.isPressed ? 1f : 0f;
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
            // Combine continuous stick/key inputs with raw accumulated mouse deltas
            float rawPitch = _stickPitch + _mousePitchAccumulated;
            float rawYaw = _stickYaw + _mouseYawAccumulated;
            float rawRoll = _stickRoll;

            // Reset mouse accumulators since we're now converting them to rotation this fixed frame
            _mousePitchAccumulated = 0f;
            _mouseYawAccumulated = 0f;

            bool hasPitch = Mathf.Abs(rawPitch) > 0.001f;
            bool hasYaw = Mathf.Abs(rawYaw) > 0.001f;
            bool hasRoll = Mathf.Abs(rawRoll) > 0.001f;

            if (hasPitch || hasYaw || hasRoll)
            {
                // Current angular velocity in local space
                Vector3 localAngularVel = transform.InverseTransformDirection(_rb.angularVelocity);

                float radPitchSpeed = pitchYawSpeed * Mathf.Deg2Rad;
                float radRollSpeed = rollSpeed * Mathf.Deg2Rad;
                float accel = rotationAcceleration * Mathf.Deg2Rad * Time.fixedDeltaTime;

                // Apply input acceleration per-axis.
                // If an axis has no input, we don't overwrite its velocity, 
                // allowing Rigidbody angularDamping to naturally decay that specific axis.
                if (hasPitch)
                    localAngularVel.x = Mathf.MoveTowards(localAngularVel.x, rawPitch * radPitchSpeed, accel);

                if (hasYaw)
                    localAngularVel.y = Mathf.MoveTowards(localAngularVel.y, rawYaw * radPitchSpeed, accel);

                if (hasRoll)
                    localAngularVel.z = Mathf.MoveTowards(localAngularVel.z, rawRoll * radRollSpeed, accel);

                // Convert back to world space
                _rb.angularVelocity = transform.TransformDirection(localAngularVel);
            }
            // NO ELSE: When there's no input on ALL axes, doing nothing allows _rb.angularDamping (rotationDrag) to naturally sustain/decay the whole spin!
        }

        // ── Public API ────────────────────────────────────────────────────────
        public Vector2 MoveInput => new Vector2(_stickYaw, _stickPitch);
        public float ThrustInput => _thrust;
    }
}
