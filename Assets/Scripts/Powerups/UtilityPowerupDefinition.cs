using UnityEngine;

namespace SpaceShooter.Powerups
{
    /// <summary>
    /// Data definition for a utility-type powerup.
    /// Utilities are activated immediately on slot press — they are NOT equipped.
    /// The effectPrefab is instantiated, runs its own logic, and self-destructs.
    /// Create assets via: Assets → Create → SpaceShooter → Powerups → Utility Powerup.
    /// </summary>
    [CreateAssetMenu(fileName = "NewUtilityPowerup", menuName = "SpaceShooter/Powerups/Utility Powerup")]
    public class UtilityPowerupDefinition : PowerupDefinition
    {
        [Header("Behaviour")]
        [Tooltip("Prefab instantiated on use. Should contain a MonoBehaviour that runs its effect and then destroys itself.")]
        public GameObject effectPrefab;
    }
}
