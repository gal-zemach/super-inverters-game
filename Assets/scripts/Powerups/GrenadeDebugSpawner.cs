using UnityEngine;

namespace Game.Powerups
{
    // TEMPORARY (Slice G2): editor-only debug helper.
    //   * press the grant key (H) to give the local player a grenade (real pickups arrive
    //     in Slice G3);
    //   * on-screen sliders tune the local player's GrenadeAimController charge feel live.
    // Self-bootstraps and survives scene loads. Removed once the G3 pickup path works.
    //
    // Slider edits apply to the spawned player INSTANCE (lost when Play stops); once the
    // feel is dialled in, bake the numbers into the Black/White player prefabs out of Play.
    public class GrenadeDebugSpawner : MonoBehaviour
    {
        [SerializeField] private KeyCode grantKey = KeyCode.H;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("GrenadeDebugGranter (AUTO)");
            DontDestroyOnLoad(go);
            go.AddComponent<GrenadeDebugSpawner>();
            Debug.Log("[GrenadeDebug] granter bootstrapped. Press 'H' to grant a grenade, " +
                      "hold 'G' to charge and release to throw.");
        }
#endif

        private void Update()
        {
            if (!Input.GetKeyDown(grantKey)) return;

            GameObject player = FindLocalPlayer();
            GrenadeInventory inv = player != null ? player.GetComponent<GrenadeInventory>() : null;
            if (inv == null)
            {
                Debug.LogWarning("[GrenadeDebug] no local GrenadeInventory found - are you " +
                                 "in the level and spawned in?");
                return;
            }
            Debug.Log(inv.Grant()
                ? "[GrenadeDebug] granted a grenade to the local player."
                : "[GrenadeDebug] local player already holds a grenade (capacity 1).");
        }

        private static GameObject FindLocalPlayer()
        {
            var players = Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                var pv = p.GetComponent<Photon.Pun.PhotonView>();
                if (pv == null || pv.IsMine) return p.gameObject;
            }
            return players.Length > 0 ? players[0].gameObject : null;
        }

        private void OnGUI()
        {
            GameObject player = FindLocalPlayer();
            GrenadeInventory inv = player != null ? player.GetComponent<GrenadeInventory>() : null;
            GrenadeAimController aim = player != null ? player.GetComponent<GrenadeAimController>() : null;

            GUILayout.BeginArea(new Rect(10, 10, 340, 160), GUI.skin.box);
            GUILayout.Label($"Grenade debug — '{grantKey}' grant, hold 'G' to throw");
            GUILayout.Label(inv != null ? $"HasGrenade: {inv.HasGrenade}" : "HasGrenade: (no local player)");
            if (aim != null)
            {
                GUILayout.Label($"Max force: {aim.ThrowForceMax:F1}");
                aim.ThrowForceMax = GUILayout.HorizontalSlider(aim.ThrowForceMax, 10f, 80f);
                GUILayout.Label($"Min force: {aim.ThrowForceMin:F1}");
                aim.ThrowForceMin = GUILayout.HorizontalSlider(aim.ThrowForceMin, 0f, 30f);
                GUILayout.Label($"Charge ramp / sec: {aim.ChargeRampPerSecond:F2}");
                aim.ChargeRampPerSecond = GUILayout.HorizontalSlider(aim.ChargeRampPerSecond, 0.3f, 4f);
            }
            GUILayout.EndArea();
        }
    }
}
