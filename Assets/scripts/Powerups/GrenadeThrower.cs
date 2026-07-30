using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Game.Powerups
{
    // Spawns the real grenade from the player, coloured by the thrower's framework, and
    // (Slice G5) mirrors it on the remote peer as a visual-only ghost:
    //   * Throw -> RPCSpawnGhostGrenade (Others): the remote instantiates the same prefab as
    //     a ghost that simulates the same physics locally but never paints;
    //   * owner detonation -> RPCDetonateGhostGrenade (Others) with the authoritative position
    //     and radius, so the remote explosion lands exactly where the paint broadcast painted.
    // Deviation from spec §3.8 (which sketched these RPCs on GameManager): they live on the
    // player's PhotonView instead — the remote copy of this player already carries the
    // grenadePrefab reference and the thrower's framework, so no scene wiring is needed, and
    // PUN's per-view ordering guarantees spawn arrives before detonate.
    public class GrenadeThrower : MonoBehaviour
    {
        [Tooltip("The Grenade.prefab to throw.")]
        [SerializeField] private GrenadeProjectile grenadePrefab;

        [Tooltip("Spawn distance from the player centre along the aim direction, so the " +
                 "grenade clears the player's own body.")]
        [SerializeField] private float spawnOffset = 1.2f;

        private PlayerState _playerState;
        private PhotonView _pv;
        private int _nextGrenadeId;
        private readonly Dictionary<int, GrenadeProjectile> _ghosts = new Dictionary<int, GrenadeProjectile>();
        private readonly List<GrenadeProjectile> _liveOwned = new List<GrenadeProjectile>();

        private void Awake()
        {
            _playerState = GetComponent<PlayerState>();
            _pv = GetComponent<PhotonView>();
        }

        private Framework ThrowerFramework =>
            _playerState != null ? _playerState.player_framework : Framework.BLACK;

        // Sprite of the grenade prefab — used by the HUD for the grenade-count icons.
        public Sprite GrenadeSprite
        {
            get
            {
                if (grenadePrefab == null) return null;
                var sr = grenadePrefab.GetComponent<SpriteRenderer>();
                return sr != null ? sr.sprite : null;
            }
        }

        // dir must be normalized; force is world units/sec.
        public void Throw(Vector2 dir, float force)
        {
            if (grenadePrefab == null)
            {
                Debug.LogWarning("GrenadeThrower: no grenadePrefab assigned.");
                return;
            }

            Vector2 spawnPos = (Vector2)transform.position + dir * spawnOffset;
            Vector2 velocity = dir * force;

            GrenadeProjectile g = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
            g.Init(ThrowerFramework, velocity);
            _liveOwned.Add(g);

            if (PhotonNetwork.InRoom && _pv != null)
            {
                int grenadeId = _nextGrenadeId++;
                g.SetOwnerCallback(this, grenadeId);
                // FuseSecondsAtSpawn bakes in any debug fuse override, so the ghost's fallback
                // fuse matches what the owner's grenade will actually do.
                _pv.RPC(nameof(RPCSpawnGhostGrenade), RpcTarget.Others,
                        grenadeId, spawnPos, velocity, g.FuseSecondsAtSpawn);
            }
        }

        // True while any grenade this player threw is still flying. Gates throwing: one
        // airborne grenade at a time, and G detonates instead of charging.
        public bool HasAirborne
        {
            get
            {
                for (int i = _liveOwned.Count - 1; i >= 0; i--)
                    if (_liveOwned[i] == null) _liveOwned.RemoveAt(i);
                return _liveOwned.Count > 0;
            }
        }

        // Juice: pressing the throw key again while grenades are airborne detonates them
        // immediately. The remote ghost follows automatically — Detonate() ends in the
        // usual NotifyOwnerDetonated RPC.
        public void DetonateAirborne()
        {
            for (int i = _liveOwned.Count - 1; i >= 0; i--)
            {
                GrenadeProjectile g = _liveOwned[i];
                _liveOwned.RemoveAt(i);
                if (g != null) g.ForceDetonate();
            }
        }

        // Called by the owner's grenade when it detonates, with the values it actually used.
        public void NotifyOwnerDetonated(int grenadeId, Vector2 pos, float paintRadius)
        {
            if (!PhotonNetwork.InRoom || _pv == null) return;
            _pv.RPC(nameof(RPCDetonateGhostGrenade), RpcTarget.Others, grenadeId, pos, paintRadius);
        }

        // Runs on the REMOTE peer's copy of the throwing player.
        [PunRPC]
        private void RPCSpawnGhostGrenade(int grenadeId, Vector2 spawnPos, Vector2 velocity, float fuseSeconds)
        {
            if (grenadePrefab == null) return;
            GrenadeProjectile g = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
            g.InitGhost(ThrowerFramework, velocity, fuseSeconds);
            _ghosts[grenadeId] = g;
        }

        [PunRPC]
        private void RPCDetonateGhostGrenade(int grenadeId, Vector2 pos, float paintRadius)
        {
            if (_ghosts.TryGetValue(grenadeId, out GrenadeProjectile ghost) && ghost != null)
            {
                _ghosts.Remove(grenadeId);
                ghost.GhostDetonateAt(pos, paintRadius);
            }
            else
            {
                // Ghost already despawned (bounds exit or its grace fuse fired) — still show
                // the boom at the authoritative spot so the paint change is explained.
                GrenadeExplosionRing.Spawn(pos, paintRadius, ThrowerFramework);
            }
        }
    }
}
