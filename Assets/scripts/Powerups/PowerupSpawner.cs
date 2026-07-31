using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Game.Powerups
{
    // Slice G3: schedules falling GrenadePickups locally (no RPCs yet). Sits on the Bootstrap
    // object in level_1-multiplayer next to MultiplayerSpawner.
    //
    // In G4 this becomes master-authoritative: the master picks (pickupId, spawnX, spawnTime)
    // and RPCs RPCSpawnPowerup to All, whose handler calls SpawnPickup with identical args so
    // both peers simulate the same fall. The local scheduling below is the seam for that; the
    // registry is reused for claim resolution. State is instance (non-static) so a rematch
    // scene reload resets it.
    public class PowerupSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("GrenadePickup.prefab (NOT in Resources — locally instantiated per peer).")]
        [SerializeField] private GrenadePickup grenadePickupPrefab;

        [Header("Schedule")]
        [Tooltip("Seconds between pickup spawns, picked at random in [min, max].")]
        [SerializeField] private float spawnIntervalMin = 15f;
        [SerializeField] private float spawnIntervalMax = 25f;

        [Header("Spawn position")]
        [Tooltip("Horizontal spawn range (arena width).")]
        [SerializeField] private float spawnXMin = -40f;
        [SerializeField] private float spawnXMax = 38f;
        [Tooltip("Y the pickup spawns at — just above the camera top.")]
        [SerializeField] private float spawnTopY = 40f;

        [Header("Fall")]
        [Tooltip("Descent speed in world units/sec (\"falls slowly\").")]
        [SerializeField] private float fallSpeed = 1.5f;

        [Header("Limits")]
        [Tooltip("Skip a spawn tick if this many pickups are already live.")]
        [SerializeField] private int maxConcurrentPickups = 2;

        [Tooltip("Derive the horizontal spawn range from the scene's platform bounds at " +
                 "startup instead of the serialized spawnXMin/Max. Lets one spawner setup " +
                 "serve every single-player level without per-level tuning.")]
        [SerializeField] private bool autoFitArenaWidth = false;

        private readonly Dictionary<int, GrenadePickup> _activePickups = new Dictionary<int, GrenadePickup>();
        private float _spawnTimer;
        private int _nextPickupId;

        private static double NetworkNow =>
            PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;

        private void Awake()
        {
            ResetTimer();
            if (autoFitArenaWidth) FitArenaWidthToPlatforms();
        }

        // Pickups should fall where platforms are: span the platforms' x extent
        // (slightly inset so edge spawns are still catchable).
        private void FitArenaWidthToPlatforms()
        {
            var platforms = GameObject.FindGameObjectsWithTag(Values.PLATFORM_TAG);
            if (platforms.Length == 0) return;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var p in platforms)
            {
                float x = p.transform.position.x;
                if (x < min) min = x;
                if (x > max) max = x;
            }
            spawnXMin = min + 1.5f;
            spawnXMax = max - 1.5f;
        }

        private void Update()
        {
            // No pickups before GO (same gate as throwing): hold the timer until the round
            // has actually started so the first pickup lands a full interval after GO.
            if (!CanSpawn()) return;

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f) return;
            ResetTimer();

            PruneDead();
            if (_activePickups.Count >= maxConcurrentPickups) return;

            // G3: spawn locally. TODO(G4): master-only — photonView.RPC(nameof(RPCSpawnPowerup),
            // RpcTarget.All, pickupId, spawnX, PhotonNetwork.Time); the RPC calls SpawnPickup.
            SpawnAtRandomX();
        }

        private void SpawnAtRandomX()
        {
            int pickupId = _nextPickupId++;
            float spawnX = Random.Range(spawnXMin, spawnXMax);
            SpawnPickup(pickupId, spawnX, NetworkNow);
        }

        // Instantiate + init a pickup. In G4 this is the shared body invoked by RPCSpawnPowerup
        // on every peer, so the fall is identical across peers.
        public void SpawnPickup(int pickupId, float spawnX, double spawnTime)
        {
            if (grenadePickupPrefab == null)
            {
                Debug.LogWarning("PowerupSpawner: no grenadePickupPrefab assigned.");
                return;
            }
            Vector3 pos = new Vector3(spawnX, spawnTopY, 0f);
            GrenadePickup pickup = Instantiate(grenadePickupPrefab, pos, Quaternion.identity);
            pickup.Init(pickupId, spawnTopY, spawnTime, fallSpeed, this);
            _activePickups[pickupId] = pickup;
        }

        public void Unregister(int pickupId) => _activePickups.Remove(pickupId);

        // Destroy every live pickup (e.g. on match end — wired into the end-game path in G6).
        public void ClearAllPickups()
        {
            foreach (var kv in _activePickups)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _activePickups.Clear();
        }

        // The epoch is a multiplayer sync concept (master broadcasts it at GO);
        // offline it stays -1 forever, so single-player only gates on countdown.
        private bool CanSpawn() =>
            !GameManager.CountdownActive
            && (!PhotonNetwork.InRoom || GameManager.PlatformMotionEpoch >= 0);

        private void PruneDead()
        {
            // Pickups Destroy() themselves (and Unregister) on collect/despawn; this also
            // catches any that died without notifying (defensive).
            List<int> dead = null;
            foreach (var kv in _activePickups)
                if (kv.Value == null) (dead ??= new List<int>()).Add(kv.Key);
            if (dead != null)
                foreach (int id in dead) _activePickups.Remove(id);
        }

        private void ResetTimer() => _spawnTimer = Random.Range(spawnIntervalMin, spawnIntervalMax);
    }
}
