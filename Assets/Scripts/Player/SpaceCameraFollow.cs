using UnityEngine;

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
        [Tooltip("Position follow speed — lower = more floaty lag")]
        [SerializeField] private float positionDamping  = 6f;
        [Tooltip("Rotation follow speed — lower = more cinematic roll")]
        [SerializeField] private float rotationDamping  = 5f;

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

        // ── Runtime ───────────────────────────────────────────────────────────
        private PlayerShip _playerShip;
        private Vector3 _smoothedPosition;
        private Quaternion _smoothedRotation;
        private float _currentPulse;
        private bool _wasThrusting;

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
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Initialize smoothed vectors if this is the first frame
            if (_smoothedPosition == Vector3.zero)
            {
                _smoothedPosition = transform.position;
                _smoothedRotation = transform.rotation;
            }

            UpdatePosition();
            UpdateRotation();

            // Set base position before shake
            transform.position = _smoothedPosition;
            transform.rotation = _smoothedRotation;

            if (_playerShip != null)
                ApplyShake();
        }

        // ── Camera logic ──────────────────────────────────────────────────────
        private void UpdatePosition()
        {
            Vector3 desiredPosition =
                target.position
                - target.forward * followDistance
                + target.up      * heightOffset;

            _smoothedPosition = Vector3.Lerp(
                _smoothedPosition,
                desiredPosition,
                positionDamping * Time.deltaTime
            );
        }

        private void UpdateRotation()
        {
            Vector3 lookTarget = target.position + target.forward * lookAheadDistance;
            Quaternion desiredRotation = Quaternion.LookRotation(
                lookTarget - _smoothedPosition,
                target.up
            );

            _smoothedRotation = Quaternion.Slerp(
                _smoothedRotation,
                desiredRotation,
                rotationDamping * Time.deltaTime
            );
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
                // Generate layered Perlin noise mapped from 0..1 to -1..1
                Vector3 noise = new Vector3(
                    Mathf.PerlinNoise(Time.time * thrustShakeSpeed, 0f) - 0.5f,
                    Mathf.PerlinNoise(0f, Time.time * thrustShakeSpeed) - 0.5f,
                    Mathf.PerlinNoise(Time.time * thrustShakeSpeed, Time.time * thrustShakeSpeed) - 0.5f
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
