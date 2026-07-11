using UnityEngine;

namespace Game.Powerups
{
    // Grenade inventory on the player. Only meaningful on the local avatar; remote avatars
    // never read it. Economy (2026-07-11): every player STARTS with a full pack
    // (startingGrenades), a collected pickup ADDS grenadesPerPickup clamped to maxGrenades
    // (1 held -> 3, 2 held -> 3), and a full player can't collect at all — pickups never
    // accumulate beyond the cap.
    public class GrenadeInventory : MonoBehaviour
    {
        [Tooltip("Grenades granted per pickup collected (clamped to Max grenades).")]
        [SerializeField] private int grenadesPerPickup = 2;

        [Tooltip("Hard cap on grenades held.")]
        [SerializeField] private int maxGrenades = 3;

        [Tooltip("Grenades each player holds at match start.")]
        [SerializeField] private int startingGrenades = 3;

        public int Count { get; private set; }

        public bool HasGrenade => Count > 0;

        private void Start()
        {
            Count = Mathf.Min(startingGrenades, maxGrenades);
        }

        // Adds a pickup's worth of grenades, clamped to the cap. Returns false only when
        // already full, so callers can tell whether the grant actually landed.
        public bool Grant()
        {
            if (Count >= maxGrenades) return false;
            Count = Mathf.Min(Count + grenadesPerPickup, maxGrenades);
            return true;
        }

        public void Consume()
        {
            if (Count > 0) Count--;
        }
    }
}
