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
        [Tooltip("Brightness pulse floor: the sprite tint oscillates between this grey level " +
                 "and full brightness (1). Sprite tints can only darken, so 'brighter' is " +
                 "faked by breathing up from a dimmed baseline.")]
        [SerializeField] private float pulseBrightnessMin = 0.55f;
        [Tooltip("Brightness pulses per second.")]
        [SerializeField] private float pulseSpeed = 1.5f;
        [Tooltip("Optional spin while falling (deg/sec). 0 = no spin.")]
        [SerializeField] private float spinDegPerSecond = 0f;

        [Tooltip("Anime-style power-up on pickup: the grenade freezes where it was collected " +
                 "and gradually turns solid white, then vanishes.")]
        [SerializeField] private float collectAnimSeconds = 0.6f;

        [Header("Collect converge orbs (DBZ energy gather)")]
        [Tooltip("White orbs spawn on a ring this many world units out and fly inward.")]
        [SerializeField] private float convergeRadius = 4.5f;
        [Tooltip("Inward speed of the orbs (world units/sec).")]
        [SerializeField] private float convergeOrbSpeed = 14f;
        [SerializeField] private float convergeOrbsPerSecond = 60f;
        [Tooltip("World-space size of each orb.")]
        [SerializeField] private float convergeOrbSize = 0.7f;

#if UNITY_EDITOR
        // TESTING ONLY — compiled out of real builds. When > 0, overrides the prefab's
        // CircleCollider2D radius on every live pickup (debug slider, applied in Update so
        // it tunes pickups already falling). Prefab-baked value: 0.25 (×6 scale = 1.5 world).
        public static float DebugColliderRadiusOverride = -1f;
        // When > 0, overrides collectAnimSeconds for the next collection (debug slider;
        // fallback constant in GrenadeDebugSpawner MUST match the serialized default).
        public static float DebugCollectAnimOverride = -1f;
        private CircleCollider2D _debugCollider;
#endif

        private float EffectiveCollectAnimSeconds
        {
            get
            {
#if UNITY_EDITOR
                if (DebugCollectAnimOverride > 0f) return DebugCollectAnimOverride;
#endif
                return collectAnimSeconds;
            }
        }

        private SpriteRenderer _sr;
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

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

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
#if UNITY_EDITOR
            if (DebugColliderRadiusOverride > 0f)
            {
                if (_debugCollider == null) _debugCollider = GetComponent<CircleCollider2D>();
                if (_debugCollider != null) _debugCollider.radius = DebugColliderRadiusOverride;
            }
#endif
            ApplyFallPosition();
            ApplyPulse();
            if (spinDegPerSecond != 0f)
                transform.Rotate(0f, 0f, spinDegPerSecond * Time.deltaTime);
            if (transform.position.y < despawnY)
                Despawn();
        }

        // Brightness heartbeat: tint oscillates dim -> full -> dim. Scaled time on purpose
        // so a synced pause freezes the pulse too.
        private void ApplyPulse()
        {
            if (_sr == null) return;
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * 2f * Mathf.PI);
            float b = Mathf.Lerp(pulseBrightnessMin, 1f, wave);
            _sr.color = new Color(b, b, b, 1f);
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

            // Pickup adds +grenadesPerPickup clamped to the cap; only a FULL inventory
            // ignores it (no accumulating beyond the cap).
            // G3: grant + collect locally. TODO(G4): instead report the claim to the master
            // (GameManager.RPCClaimPowerup, RpcTarget.MasterClient, pickupId, actorNumber);
            // RPCResolvePowerup then grants the winner + collects the pickup on every peer.
            if (!inv.Grant()) return;
            Collect();
        }

        // Collected: stop falling/colliding immediately, play the whiten-and-vanish flash,
        // then self-destruct. (Out-of-bounds despawn stays instant — see Despawn.)
        private void Collect()
        {
            if (_collected) return;
            _collected = true;
            if (_owner != null) _owner.Unregister(_pickupId);
            var col = GetComponent<CircleCollider2D>();
            if (col != null) col.enabled = false;
            StartCoroutine(CollectFlash());
        }

        private System.Collections.IEnumerator CollectFlash()
        {
            // White-silhouette overlay: GUI/Text Shader draws the sprite's alpha as a flat
            // colour — the only way to push a dark sprite TO white (tints only darken).
            // Crossfading it over the normal sprite reads as "gradually turns white".
            var overlayGo = new GameObject("CollectWhiten");
            overlayGo.transform.SetParent(transform, false);
            var overlay = overlayGo.AddComponent<SpriteRenderer>();
            overlay.sprite = _sr != null ? _sr.sprite : null;
            overlay.sortingOrder = (_sr != null ? _sr.sortingOrder : 10) + 1;
            var silhouetteShader = Shader.Find("GUI/Text Shader");
            if (silhouetteShader != null)
                overlay.material = new Material(silhouetteShader);
            overlay.color = new Color(1f, 1f, 1f, 0f);

            GameObject convergeGo = CreateConvergeOrbs();

            const float fadeTail = 0.2f; // last fraction: the now-white grenade fades away
            float duration = EffectiveCollectAnimSeconds;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float whiten = Mathf.Clamp01(k / (1f - fadeTail));            // 0->1 over first 80%
                float fade = Mathf.Clamp01((k - (1f - fadeTail)) / fadeTail); // 0->1 over last 20%
                overlay.color = new Color(1f, 1f, 1f, whiten * (1f - fade));
                if (_sr != null)
                    _sr.color = new Color(1f, 1f, 1f, 1f - fade);
                yield return null;
            }
            if (convergeGo != null) Destroy(convergeGo);
            Destroy(gameObject);
        }

        // DBZ-style energy gather: white glow orbs spawn on a ring around the (frozen)
        // pickup and converge on it. Deliberately NOT parented — the pickup's x6 scale
        // would multiply the ring radius; the pickup doesn't move during the anim anyway.
        private GameObject CreateConvergeOrbs()
        {
            var go = new GameObject("CollectConverge");
            go.transform.position = transform.position;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startSpeed = -convergeOrbSpeed;                       // negative = inward
            main.startLifetime = convergeRadius / convergeOrbSpeed;    // orbs die at the centre
            main.startSize = convergeOrbSize;
            main.startColor = Color.white;
            main.maxParticles = 300;

            var emission = ps.emission;
            emission.rateOverTime = convergeOrbsPerSecond;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = convergeRadius;
            shape.radiusThickness = 0f;                                // ring edge only

            var psr = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default"));
            var glow = GrenadeProjectile.GlowSprite;
            if (glow != null) mat.mainTexture = glow.texture;          // soft round ki orbs
            psr.material = mat;
            psr.sortingOrder = 12;
            return go;
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
