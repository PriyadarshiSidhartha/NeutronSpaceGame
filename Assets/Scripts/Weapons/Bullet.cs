using System.Collections;
using UnityEngine;

namespace SpaceShooter.Weapons
{
    /// <summary>
    /// Projectile behaviour. Moves forward at speed, deals damage on collision,
    /// then returns itself to the bullet pool.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Bullet : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float speed  = 60f;
        [SerializeField] private float lifetime = 4f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private int       _damage;
        private bool      _isPlayerBullet;
        private Vector3   _inheritedVelocity;   // ship velocity baked in at spawn
        private Vector3   _fireDirection;       // world-space travel direction, decoupled from visual rotation
        private Rigidbody _rb;
        private Coroutine _lifetimeCoroutine;

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void OnEnable()
        {
            _lifetimeCoroutine = StartCoroutine(LifetimeRoutine());
        }

        private void OnDestroy()
        {
            if (_lifetimeCoroutine != null)
            {
                StopCoroutine(_lifetimeCoroutine);
                _lifetimeCoroutine = null;
            }
        }

        private void FixedUpdate()
        {
            float forwardBoost = Vector3.Dot(_inheritedVelocity, _fireDirection);
            _rb.linearVelocity = _fireDirection * (speed + forwardBoost);

            // Force the bullet to face its travel direction every frame.
            // This overrides any prefab mesh orientation so the visual is always correct.
            _rb.MoveRotation(Quaternion.LookRotation(_fireDirection));
        }

        private void OnTriggerEnter(Collider other)
        {
            // Don't hit the owner's layer
            if (_isPlayerBullet && other.CompareTag("Player"))  return;
            if (!_isPlayerBullet && other.CompareTag("Enemy"))  return;

            if (other.TryGetComponent<IHittable>(out var target))
            {
                target.TakeDamage(_damage);
            }

            Destroy(gameObject);
        }

        // ── API ───────────────────────────────────────────────────────────────
        /// <summary>Called by the shooter to set up the bullet before enabling it.</summary>
        /// <param name="fireDirection">True world-space travel direction. Pass explicitly so the
        /// visual rotation (set via transform) can be offset independently.</param>
        public void Initialize(int damage, bool isPlayerBullet,
                               Vector3 inheritedVelocity = default,
                               Vector3 fireDirection     = default)
        {
            _damage            = damage;
            _isPlayerBullet    = isPlayerBullet;
            _inheritedVelocity = inheritedVelocity;
            // Fall back to transform.forward if caller doesn't supply a direction
            _fireDirection     = (fireDirection == default || fireDirection == Vector3.zero)
                                 ? transform.forward
                                 : fireDirection.normalized;
        }

        private IEnumerator LifetimeRoutine()
        {
            yield return new WaitForSeconds(lifetime);
            Destroy(gameObject);
        }
    }
}
