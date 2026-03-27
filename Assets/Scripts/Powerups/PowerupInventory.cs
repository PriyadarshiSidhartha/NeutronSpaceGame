using System;
using UnityEngine;

namespace SpaceShooter.Powerups
{
    /// <summary>
    /// Manages the player's 3 powerup slots and orchestrates equip / use / clear logic.
    /// Attach to the player GameObject alongside PlayerShooter.
    /// </summary>
    public class PowerupInventory : MonoBehaviour
    {
        private const int SlotCount = 3;

        // ── Events (for UI / VFX hooks) ─────────────────────────────────────
        /// <summary>Fired when any slot's contents change. Args: slotIndex, newDefinition (may be null).</summary>
        public event Action<int, PowerupDefinition> OnSlotChanged;

        /// <summary>Fired when a weapon powerup is equipped. Arg: the weapon definition.</summary>
        public event Action<WeaponPowerupDefinition> OnWeaponEquipped;

        /// <summary>Fired when the equipped weapon is unequipped (cancel, ammo out, or replaced).</summary>
        public event Action OnWeaponUnequipped;

        // ── State ────────────────────────────────────────────────────────────
        private readonly PowerupSlot[] _slots = new PowerupSlot[SlotCount];

        /// <summary>Index of the slot whose weapon is currently equipped, or -1 if using default weapon.</summary>
        private int _equippedSlotIndex = -1;

        private Player.PlayerShooter _shooter;

        // ── Public API ───────────────────────────────────────────────────────
        /// <summary>Read-only access to slots for UI.</summary>
        public PowerupSlot GetSlot(int index) => _slots[index];
        public int GetSlotCount() => SlotCount;

        /// <summary>Index of the currently equipped weapon slot, or -1 if default.</summary>
        public int EquippedSlotIndex => _equippedSlotIndex;

        // ── Unity lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = new PowerupSlot();

            _shooter = GetComponent<Player.PlayerShooter>();
        }

        // ── Pickup ───────────────────────────────────────────────────────────
        /// <summary>
        /// Attempt to place a powerup in the first empty slot.
        /// Returns true if the pickup was accepted, false if all slots are full.
        /// </summary>
        public bool TryAddPowerup(PowerupDefinition definition)
        {
            if (definition == null) return false;

            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    _slots[i].Assign(definition);
                    OnSlotChanged?.Invoke(i, definition);
                    Debug.Log($"[PowerupInventory] Picked up '{definition.displayName}' → slot {i + 1}");
                    return true;
                }
            }

            Debug.Log("[PowerupInventory] All slots full — pickup ignored.");
            return false;
        }

        // ── Slot Activation ──────────────────────────────────────────────────
        /// <summary>
        /// Called when the player presses a slot button (D-pad Up / Left / Right).
        /// Weapons → equip. Utilities → instant use.
        /// </summary>
        public void ActivateSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return;

            PowerupSlot slot = _slots[index];
            if (slot.IsEmpty) return;

            if (slot.IsWeapon)
            {
                EquipWeaponFromSlot(index);
            }
            else if (slot.IsUtility)
            {
                UseUtility(index);
            }
        }

        // ── Cancel (D-pad Down) ──────────────────────────────────────────────
        /// <summary>
        /// Reverts to the default weapon.
        /// If the equipped weapon was used (fired at least once), clears its slot.
        /// If it was never used, the powerup stays in the slot for later.
        /// </summary>
        public void CancelWeapon()
        {
            if (_equippedSlotIndex < 0) return; // already on default

            PowerupSlot slot = _slots[_equippedSlotIndex];

            if (slot.HasBeenUsed)
            {
                Debug.Log($"[PowerupInventory] Cancelled weapon in slot {_equippedSlotIndex + 1} (used → clearing).");
                slot.Clear();
                OnSlotChanged?.Invoke(_equippedSlotIndex, null);
            }
            else
            {
                Debug.Log($"[PowerupInventory] Cancelled weapon in slot {_equippedSlotIndex + 1} (unused → kept).");
            }

            UnequipCurrentWeapon();
        }

        // ── Ammo Tracking (called by PlayerShooter after each shot) ──────────
        /// <summary>
        /// Notifies the inventory that the equipped weapon fired a shot.
        /// Decrements ammo and auto-clears the slot if ammo reaches 0.
        /// </summary>
        public void NotifyAmmoUsed()
        {
            if (_equippedSlotIndex < 0) return;

            PowerupSlot slot = _slots[_equippedSlotIndex];
            slot.HasBeenUsed = true;
            slot.RemainingAmmo--;

            Debug.Log($"[PowerupInventory] Weapon in slot {_equippedSlotIndex + 1} fired. Ammo remaining: {slot.RemainingAmmo}");

            if (slot.RemainingAmmo <= 0)
            {
                Debug.Log($"[PowerupInventory] Weapon in slot {_equippedSlotIndex + 1} ammo depleted — clearing.");
                int clearedIndex = _equippedSlotIndex;
                UnequipCurrentWeapon();
                _slots[clearedIndex].Clear();
                OnSlotChanged?.Invoke(clearedIndex, null);
            }
        }

        // ── Internals ────────────────────────────────────────────────────────
        private void EquipWeaponFromSlot(int index)
        {
            // If another weapon is already equipped, handle its state first
            if (_equippedSlotIndex >= 0 && _equippedSlotIndex != index)
            {
                PowerupSlot currentSlot = _slots[_equippedSlotIndex];
                if (currentSlot.HasBeenUsed)
                {
                    Debug.Log($"[PowerupInventory] Swapping away from slot {_equippedSlotIndex + 1} (used → clearing).");
                    currentSlot.Clear();
                    OnSlotChanged?.Invoke(_equippedSlotIndex, null);
                }
                UnequipCurrentWeapon();
            }

            // Equip the new weapon
            PowerupSlot newSlot = _slots[index];
            WeaponPowerupDefinition wpd = newSlot.Definition as WeaponPowerupDefinition;
            if (wpd == null) return;

            _equippedSlotIndex = index;

            if (_shooter != null)
                _shooter.EquipWeapon(wpd);

            OnWeaponEquipped?.Invoke(wpd);
            Debug.Log($"[PowerupInventory] Equipped '{wpd.displayName}' from slot {index + 1} (ammo: {newSlot.RemainingAmmo}).");
        }

        private void UseUtility(int index)
        {
            PowerupSlot slot = _slots[index];
            UtilityPowerupDefinition upd = slot.Definition as UtilityPowerupDefinition;
            if (upd == null) return;

            Debug.Log($"[PowerupInventory] Using utility '{upd.displayName}' from slot {index + 1}.");

            // Spawn the effect prefab on the player
            if (upd.effectPrefab != null)
            {
                Instantiate(upd.effectPrefab, transform.position, transform.rotation, transform);
            }

            // Consume and clear
            slot.Clear();
            OnSlotChanged?.Invoke(index, null);
        }

        private void UnequipCurrentWeapon()
        {
            if (_equippedSlotIndex < 0) return;

            _equippedSlotIndex = -1;

            if (_shooter != null)
                _shooter.UnequipWeapon();

            OnWeaponUnequipped?.Invoke();
        }
    }
}
