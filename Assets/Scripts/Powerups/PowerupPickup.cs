using UnityEngine;

namespace SpaceShooter.Powerups
{
    /// <summary>
    /// Placed in the world as a trigger collider.
    /// When the player flies through it, the powerup is added to their first empty slot.
    /// If all slots are full the pickup is ignored and stays in the world.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PowerupPickup : MonoBehaviour
    {
        [Tooltip("The powerup definition asset this pickup grants")]
        [SerializeField] private PowerupDefinition powerupDefinition;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            var inventory = other.GetComponent<PowerupInventory>();
            if (inventory == null) inventory = other.GetComponentInParent<PowerupInventory>();

            if (inventory != null && inventory.TryAddPowerup(powerupDefinition))
            {
                // Pickup consumed — destroy the world object
                Destroy(gameObject);
            }
        }
    }
}
