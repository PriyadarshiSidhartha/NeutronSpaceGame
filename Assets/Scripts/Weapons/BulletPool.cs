using UnityEngine;
using UnityEngine.Pool;

namespace SpaceShooter.Weapons
{
    /// <summary>
    /// Singleton object pool for player bullets.
    /// Attach to an empty GameObject in the scene called "BulletPool".
    /// Assign the player bullet prefab in the Inspector.
    /// </summary>
    public class BulletPool : MonoBehaviour
    {
        public static BulletPool Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject playerBulletPrefab;

        [Header("Pool Settings")]
        [SerializeField] private int defaultCapacity = 30;
        [SerializeField] private int maxSize         = 100;

        private ObjectPool<GameObject> _playerPool;

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreatePools();
        }

        // ── Pool creation ─────────────────────────────────────────────────────
        private void CreatePools()
        {
            _playerPool = new ObjectPool<GameObject>(
                createFunc:       () => CreateBullet(playerBulletPrefab),
                actionOnGet:      go => { },              // caller sets position/rotation BEFORE enabling
                actionOnRelease:  go => go.SetActive(false),
                actionOnDestroy:  go => Destroy(go),
                collectionCheck:  false,
                defaultCapacity:  defaultCapacity,
                maxSize:          maxSize
            );
        }

        private GameObject CreateBullet(GameObject prefab)
        {
            var go = Instantiate(prefab, transform);
            go.SetActive(false);
            return go;
        }

        // ── Public API ────────────────────────────────────────────────────────
        public GameObject GetPlayerBullet()  => _playerPool.Get();
        public void ReturnPlayerBullet(GameObject go) => _playerPool.Release(go);

        /// <summary>Returns the speed value configured on the player bullet prefab's Bullet component.</summary>
        public float PlayerBulletSpeed
        {
            get
            {
                if (playerBulletPrefab != null && playerBulletPrefab.TryGetComponent<Bullet>(out var bullet))
                    return bullet.Speed;
                return 60f; // sensible fallback
            }
        }
    }
}
