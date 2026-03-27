using UnityEngine;

namespace SpaceShooter.Powerups
{
    /// <summary>
    /// Abstract base class for all powerup definitions.
    /// Create concrete subclasses (WeaponPowerupDefinition, UtilityPowerupDefinition)
    /// and author them as ScriptableObject assets in the project.
    /// The concrete subclass type itself distinguishes weapon vs utility — no enum needed.
    /// </summary>
    public abstract class PowerupDefinition : ScriptableObject
    {
        [Tooltip("Name shown in the HUD / pickup prompt")]
        public string displayName;

        [Tooltip("Icon shown in the slot UI")]
        public Sprite icon;
    }
}
