using System.Collections;
using UnityEngine;

namespace SpaceShooter.Weapons
{
    /// <summary>
    /// Projectile behaviour. Moves forward at speed, deals damage on collision,
    /// then destroys itself. Speed and lifetime are injected by the shooter
    /// via Initialize() — no serialized fields for those values.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Bullet : MonoBehaviour
    {
        // ── Runtime (set via Initialize) ──────────────────────────────────────
        private float     _speed;
        private float     _lifetime;
        private int       _damage;
        private Vector3   _inheritedVelocity;   // ship velocity baked in at spawn
        private Vector3   _fireDirection;       // world-space travel direction, decoupled from visual rotation
        private GameObject _impactEffectPrefab;
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
            _hasHit = false;
            // Coroutine starts here (not in Initialize) because pooled bullets
            // are still inactive when Initialize is called.
            // _lifetime is guaranteed set by Initialize before SetActive(true).
            if (_lifetime > 0f)
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

        private bool _hasHit;

        private void FixedUpdate()
        {
            if (_fireDirection == Vector3.zero) return;

            float forwardBoost = Vector3.Dot(_inheritedVelocity, _fireDirection);
            Vector3 currentVelocity = _fireDirection * (_speed + forwardBoost);
            _rb.linearVelocity = currentVelocity;

            // Force the bullet to face its travel direction every frame.
            // This overrides any prefab mesh orientation so the visual is always correct.
            _rb.MoveRotation(Quaternion.LookRotation(_fireDirection));

            // Prevent tunneling: Raycast ahead to see if we'll move completely through an object this frame
            float distanceThisFrame = currentVelocity.magnitude * Time.fixedDeltaTime;
            if (Physics.Raycast(transform.position, _fireDirection, out RaycastHit hit, distanceThisFrame, Physics.AllLayers, QueryTriggerInteraction.Collide))
            {
                // Manually trigger the hit before the physics engine misses it
                HandleHit(hit.collider, hit.point, hit.normal);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = -_fireDirection;
            HandleHit(other, hitPoint, hitNormal);
        }

        private void HandleHit(Collider other, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (_hasHit) return;

            // Don't hit the owner's layer (placeholder for multiplayer collision matrix)
            if (other.CompareTag("Player"))  return;

            _hasHit = true;

            if (_impactEffectPrefab != null)
            {
                GameObject effect = Instantiate(_impactEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
                Destroy(effect, 2f);
            }

            if (other.TryGetComponent<IHittable>(out var target))
            {
                target.TakeDamage(_damage);
            }

            Destroy(gameObject);
        }

        // ── API ───────────────────────────────────────────────────────────────
        /// <summary>
        /// Called by the shooter to fully configure the bullet before enabling it.
        /// Speed and lifetime are always provided by the caller (PlayerShooter for
        /// default bullets, WeaponPowerupDefinition SO for powerup bullets).
        /// </summary>
        public void Initialize(int damage, float speed, float lifetime,
                               Vector3 inheritedVelocity = default,
                               Vector3 fireDirection     = default,
                               GameObject impactEffectPrefab = null)
        {
            _damage            = damage;
            _speed             = speed;
            _lifetime          = lifetime;
            _inheritedVelocity = inheritedVelocity;
            _impactEffectPrefab = impactEffectPrefab;

            _fireDirection = (fireDirection == default || fireDirection == Vector3.zero)
                             ? transform.forward
                             : fireDirection.normalized;
        }

        private IEnumerator LifetimeRoutine()
        {
            yield return new WaitForSeconds(_lifetime);
            Destroy(gameObject);
        }
    }
}
