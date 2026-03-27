using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceShooter.Powerups
{
    /// <summary>
    /// Reads D-pad / keyboard input for powerup slots and forwards commands to PowerupInventory.
    /// Completely decoupled from what the powerups actually do.
    /// Attach to the player GameObject alongside PowerupInventory.
    /// </summary>
    [RequireComponent(typeof(PowerupInventory))]
    public class PowerupInputController : MonoBehaviour
    {
        private PowerupInventory _inventory;

        private void Awake()
        {
            _inventory = GetComponent<PowerupInventory>();
        }

        private void Update()
        {
            // ── Gamepad D-pad ────────────────────────────────────────────────
            if (Gamepad.current != null)
            {
                if (Gamepad.current.dpad.up.wasPressedThisFrame)
                    _inventory.ActivateSlot(0);

                if (Gamepad.current.dpad.left.wasPressedThisFrame)
                    _inventory.ActivateSlot(1);

                if (Gamepad.current.dpad.right.wasPressedThisFrame)
                    _inventory.ActivateSlot(2);

                if (Gamepad.current.dpad.down.wasPressedThisFrame)
                    _inventory.CancelWeapon();
            }

            // ── Keyboard ─────────────────────────────────────────────────────
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame)
                    _inventory.ActivateSlot(0);

                if (Keyboard.current.digit2Key.wasPressedThisFrame)
                    _inventory.ActivateSlot(1);

                if (Keyboard.current.digit3Key.wasPressedThisFrame)
                    _inventory.ActivateSlot(2);

                if (Keyboard.current.digit4Key.wasPressedThisFrame)
                    _inventory.CancelWeapon();
            }
        }
    }
}
