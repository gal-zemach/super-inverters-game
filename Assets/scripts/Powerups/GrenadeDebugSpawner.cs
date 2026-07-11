#if UNITY_EDITOR
using UnityEngine;

namespace Game.Powerups
{
    // TESTING ONLY — the entire file is compiled out of real builds (#if UNITY_EDITOR),
    // so nothing here can leak into the shipped WebGL build. Current kit:
    //   * spawnKey (H) drops a real pickup from the sky, exercising the collection path
    //     on demand instead of waiting out the spawn interval;
    //   * a toggle for GrenadeInventory.DebugAlwaysHaveGrenade. Leave it OFF when testing
    //     collection — while you already hold a full pack every pickup is a no-op, which
    //     looks exactly like "collecting doesn't work";
    //   * an Audio probe for the runtime sound-dropout bug: live voice census (playing /
    //     virtualized AudioSources) + the local player's Shoot source state. Unity keeps
    //     at most 32 REAL voices and silently virtualizes the least-audible ones — the
    //     quiet shot/boom SFX die first. "virtual > 0" here at dropout time confirms it.
    public class GrenadeDebugSpawner : MonoBehaviour
    {
        [SerializeField] private KeyCode spawnKey = KeyCode.H;

        private static Texture2D s_panelBg;
        private static Texture2D PanelBg
        {
            get
            {
                if (s_panelBg == null)
                {
                    s_panelBg = new Texture2D(1, 1);
                    s_panelBg.SetPixel(0, 0, new Color(0.13f, 0.13f, 0.13f, 1f));
                    s_panelBg.Apply();
                }
                return s_panelBg;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("GrenadeDebugSpawner (AUTO)");
            DontDestroyOnLoad(go);
            go.AddComponent<GrenadeDebugSpawner>();
            Debug.Log("[GrenadeDebug] bootstrapped (Editor only). 'H' drops a pickup; " +
                      "audio probe watches for virtualized (silenced) voices.");
        }

        private void Update()
        {
            if (!Input.GetKeyDown(spawnKey)) return;

            PowerupSpawner spawner = Object.FindFirstObjectByType<PowerupSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[GrenadeDebug] no PowerupSpawner in scene - are you in " +
                                 "level_1-multiplayer? (it lives on the Bootstrap object).");
                return;
            }
            spawner.DebugSpawnNow();
            Debug.Log("[GrenadeDebug] dropped a grenade pickup from the sky.");
        }

        private static PlayerManager FindLocalPlayer()
        {
            var players = Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                var pv = p.GetComponent<Photon.Pun.PhotonView>();
                if (pv == null || pv.IsMine) return p;
            }
            return null;
        }

        // ---- Audio probe ------------------------------------------------------------
        // Sampled on a short interval (OnGUI runs several times per frame; FindObjectsByType
        // every pass would be wasteful even for a debug panel).
        private const float AudioSampleInterval = 0.5f;
        private float _nextAudioSampleAt;
        private int _srcTotal, _srcPlaying, _srcVirtual;
        private readonly System.Collections.Generic.List<string> _virtualNames = new();
        private string _shootLine = "Shoot source: (sampling…)";
        private string _callLine = "";

        private void SampleAudio()
        {
            if (Time.unscaledTime < _nextAudioSampleAt) return;
            _nextAudioSampleAt = Time.unscaledTime + AudioSampleInterval;

            _virtualNames.Clear();
            var sources = Object.FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _srcTotal = sources.Length;
            _srcPlaying = 0;
            _srcVirtual = 0;
            foreach (var s in sources)
            {
                if (s == null || !s.isPlaying) continue;
                _srcPlaying++;
                if (s.isVirtual)
                {
                    _srcVirtual++;
                    if (_virtualNames.Count < 6) _virtualNames.Add(s.gameObject.name);
                }
            }

            var local = FindLocalPlayer();
            var sfx = local != null ? local.GetComponentInChildren<PlayerSFX>() : null;
            var shoot = sfx != null ? sfx.Shoot : null;
            _shootLine = shoot == null
                ? "Shoot source: (no local player)"
                : $"Shoot: playing={shoot.isPlaying}  virtual={shoot.isVirtual}  " +
                  $"vol={shoot.volume:F2}  enabled={shoot.enabled}";

            // Call-side vs sampled-instance identity: distinguishes "PlayShoot never
            // called" (calls frozen), "called on a STALE/other instance" (id mismatch)
            // and "called on this instance but silent" (ids match, vol still wrong).
            int mine = 0;
            var players = Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                var pv = p.GetComponent<Photon.Pun.PhotonView>();
                if (pv == null || pv.IsMine) mine++;
            }
            bool enableSfx = local != null && local.EnableSFX;
            string idInfo = sfx != null
                ? (PlayerSFX.DebugLastShootSfxId == sfx.GetInstanceID() ? "id=MATCH" : $"id=STALE({PlayerSFX.DebugLastShootSfxId}≠{sfx.GetInstanceID()})")
                : "id=?";
            int ago = PlayerSFX.DebugLastShootFrame < 0 ? -1 : Time.frameCount - PlayerSFX.DebugLastShootFrame;
            _callLine = $"shots={PlayerManager.DebugShotAttempts} sfxCalls={PlayerSFX.DebugShootCalls} " +
                        $"lastCall={(ago < 0 ? "never" : ago + "f ago")} " +
                        $"{idInfo} EnableSFX={enableSfx} players={players.Length}/{mine} mine";
        }

        // Static so the collapsed state survives scene reloads (rounds) within a session.
        private static bool s_collapsed;

        private void OnGUI()
        {
            SampleAudio();

            float height = s_collapsed ? 34f : 208f + 18f * _virtualNames.Count;
            var panelRect = new Rect(10, 10, 330, height);
            GUI.DrawTexture(panelRect, PanelBg);
            GUILayout.BeginArea(panelRect, GUI.skin.box);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(s_collapsed ? "▶" : "▼", GUILayout.Width(26f)))
                s_collapsed = !s_collapsed;
            GUILayout.Label($"Grenade debug (Editor only) — '{spawnKey}' drops a pickup");
            GUILayout.EndHorizontal();
            if (s_collapsed)
            {
                GUILayout.EndArea();
                return;
            }

            var local = FindLocalPlayer();
            GrenadeInventory inv = local != null ? local.GetComponent<GrenadeInventory>() : null;
            GUILayout.Label(inv != null ? $"Grenades held: {inv.Count}" : "Grenades held: (no local player)");
            if (inv != null)
                inv.DebugAlwaysHaveGrenade = GUILayout.Toggle(
                    inv.DebugAlwaysHaveGrenade, " always have a grenade (turn OFF to test collection)");

            GUILayout.Space(6f);
            GUILayout.Label("— Audio (sound-dropout probe) —");
            GUILayout.Label($"AudioSources: {_srcTotal} total | {_srcPlaying} playing | {_srcVirtual} VIRTUAL (silenced)");
            GUILayout.Label($"AudioListener.pause: {AudioListener.pause}");
            GUILayout.Label(_shootLine);
            GUILayout.Label(_callLine);
            foreach (string n in _virtualNames)
                GUILayout.Label($"  virtual: {n}");

            GUILayout.EndArea();
        }
    }
}
#endif
