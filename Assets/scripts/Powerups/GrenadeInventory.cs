using UnityEngine;

namespace Game.Powerups
{
    // Grenade inventory on the player. Only meaningful on the local avatar; remote avatars
    // never read it. A collected pickup TOPS UP to a full pack (grenadesPerPickup throws):
    // collectable at any count below max, ignored only when already full — so pickups
    // refresh your ammo but never accumulate beyond the cap.
    public class GrenadeInventory : MonoBehaviour
    {
        [Tooltip("Grenades granted per pickup collected.")]
        [SerializeField] private int grenadesPerPickup = 2;

        public int Count { get; private set; }

        public bool HasGrenade => Count > 0;

        // Tops the inventory up to a full pack. Returns false only when already full, so
        // callers can tell whether the grant actually landed.
        public bool Grant()
        {
            if (Count >= grenadesPerPickup) return false;
            Count = grenadesPerPickup;
            return true;
        }

        public void Consume()
        {
            if (Count > 0) Count--;
        }
    }
}
