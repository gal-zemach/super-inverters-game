using Photon.Pun;
using UnityEngine;

namespace Game.Powerups
{
    // Slice G3: a grenade pickup that falls slowly from the top of the arena and grants the
    // local player one grenade on touch (inventory capacity 1 — touching while already
    // holding is a no-op, the pickup keeps falling for someone else).
    //
    // Position is analytic from (spawnTime, fallSpeed) rather than physics-driven so that in
    // G4 both peers descend identically off an epoch-synced PhotonNetwork.Time (same trick as
    // platform motion). Locally instantiated on each peer — no PhotonView; correlated by
    // pickupId in the RPCs added in G4.
    //
    // Networking (master-authoritative spawn + claim arbitration) is Slice G4; seams marked
    // TODO(G4).
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class GrenadePickup : MonoBehaviour
    {
        [Header("Despawn")]
        [Tooltip("The pickup destroys itself (uncollected) once it falls below this world Y.")]
        [SerializeField] private float despawnY = -25f;

        [Header("Cosmetic")]
        [Tooltip("Heartbeat pulse: fraction of the base scale the pickup grows/shrinks by " +
                 "as it breathes (0.2 = pulses between 80% and 120% of its size). 0 = no pulse.")]
        [SerializeField] private float pulseAmplitude = 0.2f;
        [Tooltip("Heartbeat pulses per second.")]
        [SerializeField] private float pulseSpeed = 1.5f;
        [Tooltip("Optional spin while falling (deg/sec). 0 = no spin.")]
        [SerializeField] private float spinDegPerSecond = 0f;

        private Vector3 _baseScale = Vector3.one;
        private int _pickupId;
        private double _spawnTime;
        private float _spawnTopY;
        private float _fallSpeed;
        private PowerupSpawner _owner;
        private bool _collected;

        public int PickupId => _pickupId;

        // Same clock on both peers when in a room (epoch-synced fall in G4); a local fallback
        // keeps the G3 single-editor test working even outside a room.
        private static double NetworkNow =>
            PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;

        private void Awake() => _baseScale = transform.localScale;

        // Injected by PowerupSpawner at spawn.
        public void Init(int pickupId, float spawnTopY, double spawnTime, float fallSpeed, PowerupSpawner owner)
        {
            _pickupId = pickupId;
            _spawnTopY = spawnTopY;
            _spawnTime = spawnTime;
            _fallSpeed = fallSpeed;
            _owner = owner;
            ApplyFallPosition();
        }

        private void Update()
        {
            if (_collected) return;
            ApplyFallPosition();
            ApplyPulse();
            if (spinDegPerSecond != 0f)
                transform.Rotate(0f, 0f, spinDegPerSecond * Time.deltaTime);
            if (transform.position.y < despawnY)
                Despawn();
        }

        // Heartbeat: scale breathes around the base scale. Scaled time on purpose so a
        // synced pause freezes the pulse too.
        private void ApplyPulse()
        {
            if (pulseAmplitude <= 0f) return;
            float s = 1f + pulseAmplitude * Mathf.Sin(Time.time * pulseSpeed * 2f * Mathf.PI);
            transform.localScale = _baseScale * s;
        }

        // Analytic descent: y = spawnTopY - elapsed * fallSpeed. Deterministic across peers
        // once spawnTime is epoch-synced (G4).
        private void ApplyFallPosition()
        {
            float y = _spawnTopY - (float)(NetworkNow - _spawnTime) * _fallSpeed;
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, y, p.z);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected) return;
            if (!other.CompareTag(Values.PLAYER_TAG)) return;

            // Only the local peer reports for its own avatar (same IsMine gating as the kill
            // path), so a pickup shared across peers is claimed once per peer, not twice.
            PhotonView pv = other.GetComponentInParent<PhotonView>();
            if (pv != null && !pv.IsMine) return;

            GrenadeInventory inv = other.GetComponentInParent<GrenadeInventory>();
            if (inv == null) return;

            // Single-slot rule: touching a pickup while already holding one does nothing.
            if (inv.HasGrenade) return;

            // G3: grant + destroy locally. TODO(G4): instead report the claim to the master
            // (GameManager.RPCClaimPowerup, RpcTarget.MasterClient, pickupId, actorNumber);
            // RPCResolvePowerup then grants the winner + destroys the pickup on every peer.
            inv.Grant();
            Despawn();
        }

        private void Despawn()
        {
            if (_collected) return;
            _collected = true;
            if (_owner != null) _owner.Unregister(_pickupId);
            Destroy(gameObject);
        }
    }
}
