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

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            // If no target set in Inspector, try to find the player automatically
            if (target == null)
            {
                var playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null) target = playerGO.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            UpdatePosition();
            UpdateRotation();
        }

        // ── Camera logic ──────────────────────────────────────────────────────
        private void UpdatePosition()
        {
            // Desired position = behind and above the ship using its own local axes
            // target.forward  = direction the nose points
            // target.up       = direction the cockpit faces
            Vector3 desiredPosition =
                target.position
                - target.forward * followDistance    // pull back along ship's nose
                + target.up      * heightOffset;     // lift up along ship's local up

            // Smooth damp toward desired position
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                positionDamping * Time.deltaTime
            );
        }

        private void UpdateRotation()
        {
            // Look at a point slightly ahead of the ship, not just the ship pivot.
            // This prevents the noisy jitter you get when looking exactly at the origin.
            Vector3 lookTarget = target.position + target.forward * lookAheadDistance;

            // Build a look-rotation using the ship's own up axis.
            // This is what gives the horizon-free / space feel — we use target.up
            // instead of Vector3.up, so the camera rolls with the ship.
            Quaternion desiredRotation = Quaternion.LookRotation(
                lookTarget - transform.position,    // direction to look
                target.up                           // ← ship's local up, NOT world up
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationDamping * Time.deltaTime
            );
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
