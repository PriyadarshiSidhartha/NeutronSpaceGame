using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpaceShooter.Player
{
    /// <summary>
    /// A horizon-free space camera that follows the ship using the ship's OWN
    /// local axes — not the world up vector — so it freely rolls with the ship.
    ///
    /// Setup:
    ///   1. Attach this script to your Main Camera (or a child empty of it).
    ///   2. Drag the player ship into the 'target' field.
    ///   3. Adjust followDistance, heightOffset, and damping in the Inspector.
    ///
    /// NOTE: Disable any Cinemachine Brain on the same camera, or delete the
    /// CinemachineCamera — this script takes full control of the camera transform.
    /// </summary>
    public class SpaceCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Offset (in ship local space)")]
        [Tooltip("How far behind the ship the camera sits")]
        [SerializeField] private float followDistance = 12f;
        [Tooltip("How far above the ship's local up axis the camera sits")]
        [SerializeField] private float heightOffset   = 3f;

        [Header("Smoothing")]
        [Tooltip("Position follow speed — higher = tighter follow")]
        [SerializeField] private float positionDamping  = 6f;
        [Tooltip("Pitch & yaw follow speed — higher = snappier aiming")]
        [SerializeField] private float pitchYawDamping  = 5f;
        [Tooltip("Roll follow speed — lower = more cinematic, floaty roll")]
        [SerializeField] private float rollDamping      = 3f;

        [Header("Look-ahead")]
        [Tooltip("How far ahead of the ship the camera looks (depth)")]
        [SerializeField] private float lookAheadDistance = 5f;

        [Header("Camera Shake")]
        [Tooltip("Constant shake intensity while thrusting")]
        [SerializeField] private float thrustShakeIntensity = 0.05f;
        [Tooltip("How fast the shake vibrates")]
        [SerializeField] private float thrustShakeSpeed = 20f;
        [Tooltip("Initial burst of shake when thrust is first applied")]
        [SerializeField] private float initialPulseIntensity = 0.3f;
        [Tooltip("How fast the initial pulse fades out")]
        [SerializeField] private float pulseDecayRate = 5f;

        [Header("FOV Shift")]
        [Tooltip("Base field of view when not throttling")]
        [SerializeField] private float baseFOV = 60f;
        [Tooltip("Extra FOV added at full throttle")]
        [SerializeField] private float thrustFOVBoost = 10f;
        [Tooltip("How fast the FOV transitions")]
        [SerializeField] private float fovLerpSpeed = 5f;

        [Header("Chromatic Aberration")]
        [Tooltip("Max chromatic aberration intensity at full throttle (0-1)")]
        [SerializeField] private float thrustChromaticAberration = 0.35f;
        [Tooltip("How fast the aberration transitions")]
        [SerializeField] private float chromaticLerpSpeed = 5f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private PlayerShip _playerShip;
        private Camera _cam;
        private Vector3 _smoothedPosition;
        private Quaternion _smoothedRotation;
        private Vector3 _smoothedForward;
        private Vector3 _smoothedUp;
        private float _currentPulse;
        private bool _wasThrusting;
        private bool _initialized;
        private ChromaticAberration _chromaticAberration;

        // Fixed Perlin noise seed offsets — avoids axis correlation
        private float _noiseSeedX;
        private float _noiseSeedY;
        private float _noiseSeedZ;

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            // If no target set in Inspector, try to find the player automatically
            if (target == null)
            {
                var playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null) target = playerGO.transform;
            }

            if (target != null)
                _playerShip = target.GetComponent<PlayerShip>();

            _cam = GetComponent<Camera>();
            if (_cam != null)
                baseFOV = _cam.fieldOfView;

            // Try to find a Volume on this camera or in the scene
            var volume = GetComponent<Volume>();
            if (volume == null)
                volume = FindFirstObjectByType<Volume>();

            if (volume != null && volume.profile != null)
            {
                if (!volume.profile.TryGet(out _chromaticAberration))
                {
                    _chromaticAberration = volume.profile.Add<ChromaticAberration>();
                }
                _chromaticAberration.active = true;
                _chromaticAberration.intensity.overrideState = true;
            }

            // Generate unique random seeds so each noise axis is uncorrelated
            _noiseSeedX = Random.Range(0f, 1000f);
            _noiseSeedY = Random.Range(0f, 1000f);
            _noiseSeedZ = Random.Range(0f, 1000f);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Snap to target on the very first frame — no lerp-from-origin pop
            if (!_initialized)
            {
                _smoothedPosition = target.position
                                    - target.forward * followDistance
                                    + target.up * heightOffset;

                Vector3 lookTarget = target.position + target.forward * lookAheadDistance;
                _smoothedForward = (lookTarget - _smoothedPosition).normalized;
                _smoothedUp = target.up;
                _smoothedRotation = Quaternion.LookRotation(_smoothedForward, _smoothedUp);

                _initialized = true;
            }

            UpdatePosition();
            UpdateRotation();

            // Set base position before shake
            transform.position = _smoothedPosition;
            transform.rotation = _smoothedRotation;

            if (_playerShip != null)
            {
                ApplyShake();
                ApplyFOVShift();
                ApplyChromaticAberration();
            }
        }

        // ── Camera logic ──────────────────────────────────────────────────────
        private void UpdatePosition()
        {
            Vector3 desiredPosition =
                target.position
                - target.forward * followDistance
                + target.up      * heightOffset;

            // Frame-rate independent exponential decay smoothing
            float t = SmoothFactor(positionDamping, Time.deltaTime);
            _smoothedPosition = Vector3.Lerp(_smoothedPosition, desiredPosition, t);
        }

        private void UpdateRotation()
        {
            // Desired aim direction and up vector
            Vector3 lookTarget = target.position + target.forward * lookAheadDistance;
            Vector3 desiredForward = (lookTarget - _smoothedPosition).normalized;
            Vector3 desiredUp = target.up;

            // Smooth forward (pitch + yaw) and up (roll) independently
            float aimT  = SmoothFactor(pitchYawDamping, Time.deltaTime);
            float rollT = SmoothFactor(rollDamping, Time.deltaTime);

            _smoothedForward = Vector3.Slerp(_smoothedForward, desiredForward, aimT).normalized;
            _smoothedUp      = Vector3.Slerp(_smoothedUp, desiredUp, rollT).normalized;

            // Re-orthogonalise up against forward to prevent drift
            _smoothedUp = (Quaternion.LookRotation(_smoothedForward, _smoothedUp) * Vector3.up).normalized;

            _smoothedRotation = Quaternion.LookRotation(_smoothedForward, _smoothedUp);
        }

        private void ApplyShake()
        {
            bool isThrusting = _playerShip.ThrustInput > 0.05f;

            // Trigger pulse if we just started thrusting
            if (isThrusting && !_wasThrusting)
            {
                _currentPulse = initialPulseIntensity;
            }
            _wasThrusting = isThrusting;

            // Decay the pulse
            _currentPulse = Mathf.MoveTowards(_currentPulse, 0f, pulseDecayRate * Time.deltaTime);

            // Calculate active noise based on sustained thrust + the decaying pulse
            float activeShake = isThrusting ? (thrustShakeIntensity * _playerShip.ThrustInput) : 0f;
            float totalShake = _currentPulse + activeShake;

            if (totalShake > 0.001f)
            {
                // Generate layered Perlin noise with unique seeds per axis
                // to avoid correlated movement patterns
                float time = Time.time * thrustShakeSpeed;
                Vector3 noise = new Vector3(
                    Mathf.PerlinNoise(time + _noiseSeedX, _noiseSeedY) - 0.5f,
                    Mathf.PerlinNoise(_noiseSeedZ, time + _noiseSeedX) - 0.5f,
                    Mathf.PerlinNoise(time + _noiseSeedY, time + _noiseSeedZ) - 0.5f
                ) * 2f;

                transform.position += noise * totalShake;
                
                // Add a tiny bit of rotational shake for extra impact
                transform.rotation *= Quaternion.Euler(
                    noise.x * totalShake * 10f,
                    noise.y * totalShake * 10f,
                    noise.z * totalShake * 10f
                );
            }
        }

        private void ApplyFOVShift()
        {
            if (_cam == null) return;

            float targetFOV = baseFOV + thrustFOVBoost * _playerShip.ThrustInput;
            float t = SmoothFactor(fovLerpSpeed, Time.deltaTime);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, t);
        }

        private void ApplyChromaticAberration()
        {
            if (_chromaticAberration == null) return;

            float targetIntensity = thrustChromaticAberration * _playerShip.ThrustInput;
            float t = SmoothFactor(chromaticLerpSpeed, Time.deltaTime);
            _chromaticAberration.intensity.value = Mathf.Lerp(
                _chromaticAberration.intensity.value,
                targetIntensity,
                t
            );
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Adds a sudden impulse of shake (e.g. for firing weapons or taking damage)
        /// that naturally decays over time using the camera's pulseDecayRate.
        /// </summary>
        public void AddShake(float intensity, float maxIntensity = 1f)
        {
            _currentPulse = Mathf.Min(_currentPulse + intensity, maxIntensity);
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a frame-rate independent blend factor for use with Lerp/Slerp.
        ///
        /// The naive approach <c>Lerp(a, b, speed * deltaTime)</c> is NOT frame-rate
        /// independent — it converges faster at higher frame rates.
        ///
        /// The correct model is exponential decay: each frame, the remaining gap
        /// shrinks by a fixed *ratio* rather than a fixed *amount*.
        ///
        ///   blendFactor = 1 − e^(−speed × Δt)
        ///
        /// This gives identical visual results at 30 fps, 60 fps, 144 fps, or any
        /// variable rate — eliminating a major source of camera jitter.
        /// </summary>
        private static float SmoothFactor(float speed, float deltaTime)
        {
            return 1f - Mathf.Exp(-speed * deltaTime);
        }

        // ── Editor helper — draw the follow gizmo ─────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (target == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                target.position - target.forward * followDistance + target.up * heightOffset,
                0.3f);
            Gizmos.DrawLine(target.position, transform.position);
        }
#endif
    }
}
