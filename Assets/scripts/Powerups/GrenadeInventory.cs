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

#if UNITY_EDITOR
        // TESTING ONLY — this whole block is compiled out of real builds. Start with a full
        // pack and refill after it empties. NOTE: while this is ON, walking into a pickup is
        // a no-op (you always hold grenades) — turn it OFF (debug overlay toggle) to test
        // collection.
        [Header("Debug (Editor only)")]
        [SerializeField] private bool debugAlwaysHaveGrenade = true;

        public bool DebugAlwaysHaveGrenade
        {
            get => debugAlwaysHaveGrenade;
            set
            {
                debugAlwaysHaveGrenade = value;
                if (value && Count <= 0) Count = grenadesPerPickup;
            }
        }

        private void Start()
        {
            if (debugAlwaysHaveGrenade) Count = grenadesPerPickup;
        }
#endif

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
#if UNITY_EDITOR
            if (debugAlwaysHaveGrenade && Count <= 0) Count = grenadesPerPickup;
#endif
        }
    }
}
