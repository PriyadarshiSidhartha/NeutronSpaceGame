using UnityEngine;

namespace SpaceShooter.Powerups
{
    /// <summary>
    /// Placed in the world as a destructible object.
    /// When the player shoots it and destroys it, the powerup is added to their first empty slot.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PowerupPickup : MonoBehaviour, IHittable
    {
        [Tooltip("The powerup definition asset this pickup grants")]
        [SerializeField] private PowerupDefinition powerupDefinition;

        [Tooltip("How much damage is needed to break open this powerup container")]
        [SerializeField] private int health = 1;

        public void TakeDamage(int damage)
        {
            health -= damage;
            if (health <= 0)
            {
                GrantPowerup();
            }
        }

        private void GrantPowerup()
        {
            // Find the player in the scene
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var inventory = player.GetComponent<PowerupInventory>();
                if (inventory == null) inventory = player.GetComponentInParent<PowerupInventory>();

                if (inventory != null)
                {
                    inventory.TryAddPowerup(powerupDefinition);
                }
            }
            
            // Destroy the container regardless of whether it was successfully picked up, 
            // since it was logically destroyed by damage.
            Destroy(gameObject);
        }
    }
}
