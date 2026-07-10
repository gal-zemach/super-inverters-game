using UnityEngine;

namespace Game.Powerups
{
    // Single-grenade inventory (capacity 1) on the player. Only meaningful on the local
    // avatar; remote avatars never read it. Granted by a pickup, consumed on throw.
    public class GrenadeInventory : MonoBehaviour
    {
        public bool HasGrenade { get; private set; }

        // Returns false if already holding one (capacity 1), so callers can tell whether
        // the grant actually landed.
        public bool Grant()
        {
            if (HasGrenade) return false;
            HasGrenade = true;
            return true;
        }

        public void Consume()
        {
            HasGrenade = false;
        }
    }
}
