namespace SpaceShooter.Powerups
{
    /// <summary>
    /// Represents the runtime state of a single powerup slot.
    /// Plain C# class — no MonoBehaviour overhead.
    /// </summary>
    [System.Serializable]
    public class PowerupSlot
    {
        /// <summary>The powerup asset in this slot, or null if empty.</summary>
        public PowerupDefinition Definition;

        /// <summary>Remaining ammo (weapons only).</summary>
        public int RemainingAmmo;

        /// <summary>True if the weapon has been fired at least once (used for cancel-clear logic).</summary>
        public bool HasBeenUsed;

        // ── Convenience ──────────────────────────────────────────────────────
        public bool IsEmpty  => Definition == null;
        public bool IsWeapon => Definition is WeaponPowerupDefinition;
        public bool IsUtility => Definition is UtilityPowerupDefinition;

        /// <summary>Assign a powerup to this slot.</summary>
        public void Assign(PowerupDefinition definition)
        {
            Definition = definition;
            HasBeenUsed = false;

            if (definition is WeaponPowerupDefinition wpd)
                RemainingAmmo = wpd.ammo;
            else
                RemainingAmmo = 0;
        }

        /// <summary>Clear the slot entirely.</summary>
        public void Clear()
        {
            Definition = null;
            RemainingAmmo = 0;
            HasBeenUsed = false;
        }
    }
}
