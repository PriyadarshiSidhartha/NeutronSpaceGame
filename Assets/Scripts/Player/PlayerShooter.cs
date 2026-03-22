using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceShooter.Player
{
    /// <summary>
    /// Handles player firing — spawns bullets from one or more muzzle points.
    /// Hold the fire button (LMB / South gamepad button) for continuous fire.
    /// Bullet lifetime and pooling are managed entirely by Bullet.cs + BulletPool.cs.
    /// </summary>
    public class PlayerShooter : MonoBehaviour
    {
        [Header("Firing")]
        [SerializeField] private float fireRate = 8f;           // shots per second
        [SerializeField] private Transform[] muzzlePoints;      // assign in Inspector
        [SerializeField] private int damagePerBullet = 20;

        [Header("Bullet Spread (optional)")]
        [Range(0f, 5f)]
        [SerializeField] private float spreadAngle = 0f;        // 0 = laser-straight

        [Header("Bullet Visual")]
        [Tooltip("Euler offset applied to bullet's spawn rotation to correct mesh orientation.\nCommon fixes: if bullet points Up → set X to -90. If it points backward → set Y to 180.")]
        [SerializeField] private Vector3 bulletSpawnRotationOffset = Vector3.zero;

        // ── Internals ─────────────────────────────────────────────────────────
        private float     _fireTimer;
        private bool      _isFiring;
        private Rigidbody _rb;          // ship rigidbody — velocity inherited by bullets

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // Prime the timer so the first shot fires immediately on button press
            _fireTimer = 0f;
        }

        private void Update()
        {
            ReadFireInput();
            HandleFiring();
        }

        // ── Input ─────────────────────────────────────────────────────────────
        private void ReadFireInput()
        {
            _isFiring = false;

            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                _isFiring = true;

            if (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed)
                _isFiring = true;
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
                SpawnBullet(transform.position, transform.forward);
                return;
            }

            foreach (var muzzle in muzzlePoints)
            {
                if (muzzle == null) continue;

                Vector3 direction = muzzle.forward;

                if (spreadAngle > 0f)
                {
                    direction = Quaternion.Euler(
                        Random.Range(-spreadAngle, spreadAngle),
                        Random.Range(-spreadAngle, spreadAngle),
                        0f) * direction;
                }

                SpawnBullet(muzzle.position, direction);
            }
        }

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
                                  isPlayerBullet: true,
                                  inheritedVelocity: _rb != null ? _rb.linearVelocity : Vector3.zero,
                                  fireDirection: direction);

            // 5. Enable last — triggers OnEnable → lifetime coroutine starts with correct state
            bulletGO.SetActive(true);
        }
    }
}
