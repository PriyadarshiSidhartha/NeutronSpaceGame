using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceShooter.Player
{
    /// <summary>
    /// Handles player firing — spawns bullets from one or more muzzle points.
    /// Hold the fire button (LMB / Left Trigger) for continuous fire.
    /// Bullet lifetime and pooling are managed entirely by Bullet.cs + BulletPool.cs.
    /// </summary>
    public class PlayerShooter : MonoBehaviour
    {
        [Header("Firing")]
        [SerializeField] private float fireRate = 8f;           // shots per second
        [SerializeField] private Transform[] muzzlePoints;      // assign in Inspector
        [SerializeField] private int damagePerBullet = 20;

        [Header("Muzzle Visuals")]
        [Tooltip("Optional. Visual models to rotate towards the aim point.")]
        [SerializeField] private Transform[] muzzleVisuals;
        [Tooltip("How fast the visuals rotate to track the target.")]
        [SerializeField] private float muzzleTurnSpeed = 15f;
        [Tooltip("Euler offset applied to the muzzle visual's rotation to correct mesh orientation. (e.g., if the barrel points up, set X to 90)")]
        [SerializeField] private Vector3 muzzleVisualRotationOffset = Vector3.zero;

        [Header("Aiming")]
        [Tooltip("Fallback aim distance when the camera center ray hits nothing")]
        [SerializeField] private float aimDistance = 500f;
        [Tooltip("If any muzzle is closer than this to an object, camera-center aiming is disabled and muzzles fire straight forward.")]
        [SerializeField] private float minShootDistance = 2f;

        [Header("Auto Aim")]
        [Tooltip("Radius around the player to search for targets")]
        [SerializeField] private float autoAimRadius = 300f;
        [Tooltip("Maximum angle (in degrees) from the center of the screen to lock on")]
        [SerializeField] private float autoAimAngle = 15f;
        [Tooltip("Tag used to identify auto-aim targets")]
        [SerializeField] private string targetTag = "target";
        [Tooltip("Layer mask for obstacles blocking line of sight to the target")]
        [SerializeField] private LayerMask obstacleMask;

        [Header("Predictive Lead")]
        [Tooltip("How much of the predicted offset is applied (0 = no lead, 1 = full lead)")]
        [SerializeField][Range(0f, 1f)] private float predictionStrength = 0.85f;
        [Tooltip("Maximum lead distance to prevent wild overshoots on very fast targets")]
        [SerializeField] private float maxLeadDistance = 50f;
        [Tooltip("Beyond this distance, full prediction is applied. Closer targets get less prediction.")]
        [SerializeField] private float fullPredictionDistance = 150f;

        [Header("Bullet Spread (optional)")]
        [Range(0f, 5f)]
        [SerializeField] private float spreadAngle = 0f;        // 0 = laser-straight

        [Header("Bullet Visual")]
        [Tooltip("Euler offset applied to bullet's spawn rotation to correct mesh orientation.\nCommon fixes: if bullet points Up → set X to -90. If it points backward → set Y to 180.")]
        [SerializeField] private Vector3 bulletSpawnRotationOffset = Vector3.zero;

        [Header("Juice / Feedback")]
        [Tooltip("Amount of screenshake impulse added per shot")]
        [SerializeField] private float fireShakeIntensity = 0.05f;
        [Tooltip("Maximum allowed screenshake from rapid firing")]
        [SerializeField] private float fireShakeMax = 0.2f;

        // ── Internals ─────────────────────────────────────────────────────────
        private float     _fireTimer;
        private bool      _isFiring;
        private Rigidbody _rb;          // ship rigidbody — velocity inherited by bullets
        private Camera    _cam;
        private SpaceCameraFollow _spaceCam;

        private Vector3   _currentAimPoint;
        private bool      _isTooClose;
        private int       _currentMuzzleIndex;
        private float     _bulletSpeed;       // auto-detected from BulletPool prefab
        private bool      _aimAssistEnabled = true;

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _cam = Camera.main;

            if (_cam != null)
                _spaceCam = _cam.GetComponent<SpaceCameraFollow>();
            if (_spaceCam == null)
                _spaceCam = FindFirstObjectByType<SpaceCameraFollow>();

            // Prime the timer so the first shot fires immediately on button press
            _fireTimer = 0f;
        }

        private void Start()
        {
            // Auto-detect bullet speed from the BulletPool prefab — no need to duplicate the value manually
            if (Weapons.BulletPool.Instance != null)
                _bulletSpeed = Weapons.BulletPool.Instance.PlayerBulletSpeed;
            else
                _bulletSpeed = 60f; // safe fallback
        }

        private void Update()
        {
            ReadFireInput();
            ReadToggleInput();
            UpdateAim();
            HandleFiring();
        }

        // ── Input ─────────────────────────────────────────────────────────────
        private void ReadFireInput()
        {
            _isFiring = false;

            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                _isFiring = true;

            if (Gamepad.current != null && Gamepad.current.leftTrigger.isPressed)
                _isFiring = true;
        }

        /// <summary>Toggle aim assist on Triangle / Y (gamepad buttonNorth) or T (keyboard).</summary>
        private void ReadToggleInput()
        {
            bool pressed = false;

            if (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame)
                pressed = true;

            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                pressed = true;

            if (pressed)
            {
                _aimAssistEnabled = !_aimAssistEnabled;
                Debug.Log($"[PlayerShooter] Aim Assist: {(_aimAssistEnabled ? "ON" : "OFF")}");
            }
        }

        // ── Aiming ────────────────────────────────────────────────────────────
        private void UpdateAim()
        {
            // 1. Calculate ideal aim point — auto-aim only if toggled ON
            _currentAimPoint = GetAimPoint(allowAutoAim: _aimAssistEnabled);

            // 2. Proximity Check
            _isTooClose = false;
            if (muzzlePoints != null)
            {
                foreach (var muzzle in muzzlePoints)
                {
                    if (muzzle == null) continue;

                    Vector3 dirToAim = _currentAimPoint - muzzle.position;
                    float distToAim = dirToAim.magnitude;

                    // Is target point too close, or path blocked?
                    if (distToAim < minShootDistance || 
                        Physics.Raycast(muzzle.position, dirToAim.normalized, out _, minShootDistance, obstacleMask))
                    {
                        _isTooClose = true;
                        break;
                    }
                }
            }

            // 3. Align Visuals
            if (muzzleVisuals != null && muzzleVisuals.Length > 0)
            {
                for (int i = 0; i < muzzleVisuals.Length; i++)
                {
                    Transform visual = muzzleVisuals[i];
                    if (visual == null) continue;

                    // If too close, look straight forward relative to the ship, otherwise look at aim point
                    Vector3 targetDir = _isTooClose ? transform.forward : (_currentAimPoint - visual.position).normalized;
                    
                    if (targetDir != Vector3.zero)
                    {
                        // Use the visual's parent's up to lock the Z-axis (roll) so the visual doesn't twist relative to its mount
                        Vector3 referenceUp = visual.parent != null ? visual.parent.up : transform.up;
                        Quaternion targetRot = Quaternion.LookRotation(targetDir, referenceUp) * Quaternion.Euler(muzzleVisualRotationOffset);
                        visual.rotation = Quaternion.Slerp(visual.rotation, targetRot, Time.deltaTime * muzzleTurnSpeed);
                    }
                }
            }
        }

        // ── Firing logic ──────────────────────────────────────────────────────
        private void HandleFiring()
        {
            _fireTimer += Time.deltaTime;

            if (!_isFiring) return;
            if (_fireTimer < 1f / fireRate) return;   // still on cooldown

            _fireTimer = 0f;
            Fire();
        }

        /// <summary>
        /// Determines the world-space aim point from screen centre, then fires
        /// each muzzle's bullet toward that converged point.
        /// </summary>
        private void Fire()
        {
            if (Weapons.BulletPool.Instance == null)
            {
                Debug.LogWarning("[PlayerShooter] BulletPool not found in scene!");
                return;
            }

            // No muzzle points assigned — fire from ship centre as fallback
            if (muzzlePoints == null || muzzlePoints.Length == 0)
            {
                Vector3 dir = (_currentAimPoint - transform.position).normalized;
                SpawnBullet(transform.position, dir);
                return;
            }

            // Find the next valid muzzle in the array to alternate shots
            int startIndex = _currentMuzzleIndex;
            Transform muzzle = null;
            do
            {
                _currentMuzzleIndex = (_currentMuzzleIndex + 1) % muzzlePoints.Length;
                muzzle = muzzlePoints[_currentMuzzleIndex];
                if (muzzle != null) break;
            } while (_currentMuzzleIndex != startIndex);

            if (muzzle == null) return; // All muzzles are null

            // Direction: Straight forward if too close, otherwise converge on aim point
            Vector3 direction = _isTooClose 
                ? transform.forward 
                : (_currentAimPoint - muzzle.position).normalized;

            if (spreadAngle > 0f)
            {
                direction = Quaternion.Euler(
                    Random.Range(-spreadAngle, spreadAngle),
                    Random.Range(-spreadAngle, spreadAngle),
                    0f) * direction;
            }

            SpawnBullet(muzzle.position, direction);

            // Screen shake
            if (_spaceCam != null && fireShakeIntensity > 0f)
            {
                _spaceCam.AddShake(fireShakeIntensity, fireShakeMax);
            }
        }

        // ── Auto Aim & Prediction ─────────────────────────────────────────────

        /// <summary>
        /// Casts a ray from screen centre (crosshair) into the scene to find an aim point.
        /// Prioritizes Auto-Aim targets within the configured angle if allowed.
        /// </summary>
        private Vector3 GetAimPoint(bool allowAutoAim)
        {
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null)
                    _cam = FindFirstObjectByType<Camera>();
            }

            if (_cam == null)
                return transform.position + transform.forward * aimDistance;

            Ray camRay = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            // 1. Check for Auto-Aim target (if not blocked by proximity)
            if (allowAutoAim && TryFindAutoAimTarget(camRay, out Vector3 targetPos))
            {
                return targetPos;
            }

            // 2. Fallback to physical raycast from center
            if (Physics.Raycast(camRay, out RaycastHit hit, aimDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            // 3. Absolute fallback in empty space
            return camRay.GetPoint(aimDistance);
        }

        /// <summary>
        /// Searches for the most suitable target within the Auto-Aim parameters.
        /// Returns a PREDICTED aim point that leads the target based on bullet travel time.
        /// </summary>
        private bool TryFindAutoAimTarget(Ray camRay, out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;

            Collider[] colliders = Physics.OverlapSphere(transform.position, autoAimRadius);

            float bestScore = float.MaxValue;
            bool foundTarget = false;
            Collider bestCollider = null;

            foreach (var col in colliders)
            {
                if (!col.CompareTag(targetTag)) continue;

                Vector3 currentPos = col.bounds.center;
                Vector3 dirToTarget = currentPos - camRay.origin;
                float distToTarget = dirToTarget.magnitude;

                // Check if target is behind camera or beyond aim distance
                if (distToTarget > autoAimRadius || distToTarget < 1f) continue;

                // Check angle from camera center ray
                float angleToTarget = Vector3.Angle(camRay.direction, dirToTarget);
                if (angleToTarget > autoAimAngle) continue;

                // Line of Sight check
                if (Physics.Raycast(camRay.origin, dirToTarget.normalized, out RaycastHit hit, distToTarget, obstacleMask))
                {
                    if (hit.collider != col && !hit.collider.CompareTag(targetTag))
                    {
                        continue; // Blocked by an obstacle
                    }
                }

                // Scoring: lower is better. Angle weighted heavily to prioritize crosshair-near targets.
                float score = angleToTarget * 10f + distToTarget;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestCollider = col;
                    foundTarget = true;
                }
            }

            if (foundTarget && bestCollider != null)
            {
                targetPosition = GetPredictedPosition(bestCollider, camRay.origin);
            }

            return foundTarget;
        }

        /// <summary>
        /// Computes a predicted aim point for a target, leading it based on:
        ///   - bullet travel time (distance / bulletSpeed)
        ///   - target velocity (from its Rigidbody)
        ///   - distance-based scaling (close targets need less lead)
        ///   - clamped maximum offset (prevents wild overshoots)
        ///   - blended with raw position via predictionStrength
        /// </summary>
        private Vector3 GetPredictedPosition(Collider targetCollider, Vector3 shooterOrigin)
        {
            Vector3 currentPos = targetCollider.bounds.center;
            float distance = Vector3.Distance(shooterOrigin, currentPos);

            // Get target velocity — if there's no Rigidbody, there's no movement to predict
            Rigidbody targetRb = targetCollider.attachedRigidbody;
            if (targetRb == null || _bulletSpeed < 0.01f)
                return currentPos;

            Vector3 targetVelocity = targetRb.linearVelocity;
            
            // If target is barely moving, skip prediction entirely to avoid micro-jitter
            if (targetVelocity.sqrMagnitude < 0.1f)
                return currentPos;

            // Estimate bullet travel time
            float travelTime = distance / _bulletSpeed;

            // Raw predicted offset
            Vector3 leadOffset = targetVelocity * travelTime;

            // Clamp the lead offset to prevent wild overshoots on very fast targets
            if (leadOffset.magnitude > maxLeadDistance)
                leadOffset = leadOffset.normalized * maxLeadDistance;

            // Distance-based scaling: closer targets get less prediction (they need less lead),
            // far targets get full prediction
            float distanceFactor = Mathf.Clamp01(distance / Mathf.Max(fullPredictionDistance, 1f));

            // Final blended prediction: lerp between raw position and predicted position
            // predictionStrength controls the overall assist aggressiveness
            // distanceFactor scales it down for close-range encounters
            Vector3 predictedPos = currentPos + leadOffset * (predictionStrength * distanceFactor);

            return predictedPos;
        }

        // ── Bullet Spawning ───────────────────────────────────────────────────
        private void SpawnBullet(Vector3 position, Vector3 direction)
        {
            // 1. Get from pool — bullet is still DISABLED at this point
            GameObject bulletGO = Weapons.BulletPool.Instance.GetPlayerBullet();
            if (bulletGO == null) return;

            // 2. Detach from pool hierarchy so it moves independently in world space
            bulletGO.transform.SetParent(null);

            // 3. Visual rotation = fire direction + optional mesh correction offset
            //    (tweak bulletSpawnRotationOffset in the Inspector until the nose faces forward)
            Quaternion visualRot = Quaternion.LookRotation(direction)
                                   * Quaternion.Euler(bulletSpawnRotationOffset);
            bulletGO.transform.SetPositionAndRotation(position, visualRot);

            // 4. Initialize: pass the TRUE fire direction separately so physics isn't
            //    affected by the visual rotation offset
            if (bulletGO.TryGetComponent<Weapons.Bullet>(out var bullet))
                bullet.Initialize(damagePerBullet,
                                  inheritedVelocity: _rb != null ? _rb.linearVelocity : Vector3.zero,
                                  fireDirection: direction);

            // 5. Enable last — triggers OnEnable → lifetime coroutine starts with correct state
            bulletGO.SetActive(true);
        }

        // ── Public API ────────────────────────────────────────────────────────
        /// <summary>Whether aim assist is currently active.</summary>
        public bool AimAssistEnabled => _aimAssistEnabled;
    }
}
