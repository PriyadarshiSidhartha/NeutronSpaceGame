using UnityEngine;

namespace SpaceShooter.Powerups
{
    /// <summary>
    /// Data definition for a weapon-type powerup.
    /// When equipped, overrides the player's default weapon until ammo is expended or the player cancels.
    /// Create assets via: Assets → Create → SpaceShooter → Powerups → Weapon Powerup.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeaponPowerup", menuName = "SpaceShooter/Powerups/Weapon Powerup")]
    public class WeaponPowerupDefinition : PowerupDefinition
    {
        [Header("Weapon Stats")]
        [Tooltip("Total ammo for this weapon. 1 = single-use.")]
        public int ammo = 1;

        [Tooltip("Shots per second")]
        public float fireRate = 2f;

        [Tooltip("Random cone spread angle per shot")]
        [Range(0f, 10f)]
        public float spreadAngle = 0f;

        [Tooltip("Damage dealt per bullet")]
        public int damagePerShot = 50;

        [Header("Bullet")]
        [Tooltip("Unique projectile prefab for this weapon")]
        public GameObject bulletPrefab;

        [Tooltip("Projectile travel speed")]
        public float bulletSpeed = 80f;

        [Tooltip("How long the bullet lives before auto-destroying (seconds)")]
        public float bulletLifetime = 4f;

        [Tooltip("Euler offset to correct bullet mesh orientation")]
        public Vector3 bulletRotationOffset;

        [Header("Heat-Seeking")]
        [Tooltip("If true, bullets from this weapon will home in on the nearest target")]
        public bool isHeatSeeking = false;

        [Tooltip("How aggressively the bullet steers toward its target (degrees/sec)")]
        public float homingTurnSpeed = 180f;

        [Tooltip("Maximum distance at which the bullet will acquire a target")]
        public float homingSearchRadius = 200f;

        [Tooltip("Tag used to identify valid homing targets")]
        public string homingTargetTag = "target";

        [Header("Juice")]
        [Tooltip("Spawned at the muzzle point when fired")]
        public GameObject muzzleFlashPrefab;

        [Tooltip("Spawned at the hit point on impact")]
        public GameObject impactEffectPrefab;

        [Tooltip("Screenshake impulse per shot")]
        public float screenShake = 0.1f;

        [Tooltip("Maximum screenshake from rapid firing")]
        public float screenShakeMax = 0.2f;
    }
}
