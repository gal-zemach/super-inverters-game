using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Game.Powerups
{
    // Grenade projectile.
    //   * full Rigidbody2D physics, bounces off platforms/floor/walls (see the
    //     `grenade` layer's Physics2D matrix row), each bounce damped by
    //     bounceVelocityRetention;
    //   * detonates after a fuse and paints every platform whose collider overlaps
    //     explosionRadius the thrower's colour (paint broadcast per platform);
    //   * ghost mode (Slice G5): the remote peer's copy simulates the same physics but
    //     never paints — it waits for the owner's RPCDetonateGhostGrenade to explode at
    //     the authoritative position (with a grace-fuse fallback if that never arrives).
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class GrenadeProjectile : MonoBehaviour
    {
        [Header("Physics")]
        [Tooltip("Gravity multiplier applied to the Rigidbody2D. Higher = steeper arc.")]
        [SerializeField] private float grenadeGravityScale = 1.0f;

        [Tooltip("Fraction of speed kept after each bounce (0..1). 1 = no decay; " +
                 "lower makes bounces die out faster. The PhysicsMaterial2D bounciness " +
                 "handles the reflection, this scalar handles the height decay.")]
        [Range(0f, 1f)]
        [SerializeField] private float bounceVelocityRetention = 0.6f;

        [Header("Spin")]
        [Tooltip("Tumble on throw: random angular speed range (deg/sec). Spin direction is " +
                 "opposite the horizontal throw direction, like a grenade leaving a hand.")]
        [SerializeField] private float spinDegPerSecMin = 180f;
        [SerializeField] private float spinDegPerSecMax = 540f;
        [Tooltip("Random initial tilt (+/- deg) so consecutive throws don't look identical.")]
        [SerializeField] private float initialTiltRange = 25f;

        [Header("Fuse")]
        [Tooltip("Seconds from spawn until the grenade detonates.")]
        [SerializeField] private float fuseSeconds = 2.5f;

        [Header("Detonation")]
        [Tooltip("World-space radius around the final position. Every platform whose " +
                 "collider overlaps this circle is painted the thrower's colour.")]
        [SerializeField] private float explosionRadius = 6f;

        [Header("Bounds")]
        [Tooltip("If true, leaving the arena bounds detonates the grenade; " +
                 "if false it just vanishes with no paint.")]
        [SerializeField] private bool detonateOnBoundsExit = false;

        [Header("Fuse light")]
        [Tooltip("Blink rate of the white armed-fuse halo (full on/off cycles per second).")]
        [SerializeField] private float fuseLightBlinksPerSecond = 1.5f;
        [Tooltip("Local scale of the small white LED-style blink dot drawn on the grenade " +
                 "body. 0.85 is roughly 8 screen px at the game camera's zoom (~70 visible " +
                 "world units on a 1080p view).")]
        [SerializeField] private float fuseLightSize = 0.85f;
        [Tooltip("Opacity of the LED dot when lit.")]
        [Range(0f, 1f)]
        [SerializeField] private float fuseLightAlpha = 1f;
        [Tooltip("Local offset of the dot from the grenade centre (near the cap).")]
        [SerializeField] private Vector2 fuseLightOffset = new Vector2(0.05f, 0.1f);

        [Header("Trail")]
        [Tooltip("Seconds each trail particle lives before fading out (the decay).")]
        [SerializeField] private float trailParticleLifetime = 0.45f;
        [Tooltip("Trail particles emitted per second (the amount).")]
        [SerializeField] private float trailParticlesPerSecond = 40f;
        [Tooltip("World-space size of each square trail particle (the arena is ~80 units " +
                 "wide, so ~0.5 is roughly a 14px square on screen).")]
        [SerializeField] private float trailParticleSize = 0.55f;
        [Tooltip("Trail colour per thrower. Mid-grey is invisible against the grey arena " +
                 "background — keep these near-black / near-white for contrast.")]
        [SerializeField] private Color trailColorBlack = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        [SerializeField] private Color trailColorWhite = new Color(0.95f, 0.95f, 0.95f, 0.85f);

#if UNITY_EDITOR
        // TESTING ONLY — debug-slider overrides, compiled out of real builds. -1 = use the
        // serialized value. The slider fallback constants in GrenadeDebugSpawner MUST match
        // the serialized values in Grenade.prefab (the slider re-assigns these every frame).
        public static float DebugBlinkHzOverride = -1f;
        public static float DebugTrailLifetimeOverride = -1f;
        public static float DebugTrailRateOverride = -1f;
        public static float DebugFuseOverride = -1f;
        // Diagnostic: forces the fuse light permanently lit, separating "renders but too
        // subtle to notice while blinking" from "doesn't render at all".
        public static bool DebugFuseLightAlwaysOn = false;
#endif

        private float EffectiveFuseSeconds
        {
            get
            {
#if UNITY_EDITOR
                if (DebugFuseOverride > 0f) return DebugFuseOverride;
#endif
                return fuseSeconds;
            }
        }

        private float BlinkHz
        {
            get
            {
#if UNITY_EDITOR
                if (DebugBlinkHzOverride > 0f) return DebugBlinkHzOverride;
#endif
                return fuseLightBlinksPerSecond;
            }
        }

        private float TrailLifetime
        {
            get
            {
#if UNITY_EDITOR
                if (DebugTrailLifetimeOverride > 0f) return DebugTrailLifetimeOverride;
#endif
                return trailParticleLifetime;
            }
        }

        private float TrailRate
        {
            get
            {
#if UNITY_EDITOR
                if (DebugTrailRateOverride >= 0f) return DebugTrailRateOverride;
#endif
                return trailParticlesPerSecond;
            }
        }

        private Rigidbody2D _rb;
        private Framework _framework = Framework.BLACK;
        private float _fuseRemaining;
        private float _aliveTime;
        private bool _detonated;
        private int _platformLayersMask;
        private GameManager _gameManager;
        private SpriteRenderer _fuseLight;
        private ParticleSystem _trail;

        // White glow with a soft radial falloff, generated once — no asset dependency for
        // the fuse flash. Solid core to ~75% radius, then fades to transparent at the edge.
        private static Sprite s_lightSprite;
        private static Sprite LightSprite
        {
            get
            {
                if (s_lightSprite == null)
                {
                    const int n = 64;
                    var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                    float c = (n - 1) / 2f, r = n / 2f - 0.5f;
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            float dx = x - c, dy = y - c;
                            float d01 = Mathf.Sqrt(dx * dx + dy * dy) / r;
                            float a = 1f - Mathf.SmoothStep(0.75f, 1f, d01);
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                        }
                    tex.Apply();
                    // PPU 400 keeps the sprite the same world size as the old 16px/100ppu one.
                    s_lightSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 400f);
                }
                return s_lightSprite;
            }
        }

        // Ghost state (G5): a ghost is cosmetic-only. It normally detonates via
        // GhostDetonateAt (owner's RPC, exact position); the grace below is how long past
        // its nominal fuse it waits before detonating in place as a fallback.
        private const float GhostFuseGraceSeconds = 0.75f;
        private bool _isGhost;
        private float _ghostPaintRadius = -1f;
        private GrenadeThrower _ownerThrower;
        private int _ownerGrenadeId = -1;

        // The fuse this grenade will actually use; read right after Init by the thrower
        // so the ghost RPC carries the real value.
        public float FuseSecondsAtSpawn => _fuseRemaining;

        public void SetOwnerCallback(GrenadeThrower thrower, int grenadeId)
        {
            _ownerThrower = thrower;
            _ownerGrenadeId = grenadeId;
        }

        public void InitGhost(Framework framework, Vector2 initialVelocity, float fuseSeconds)
        {
            Init(framework, initialVelocity);
            _isGhost = true;
            _fuseRemaining = fuseSeconds + GhostFuseGraceSeconds;
        }

        // Owner told us exactly where it exploded: snap there and detonate.
        public void GhostDetonateAt(Vector2 pos, float paintRadius)
        {
            transform.position = pos;
            _ghostPaintRadius = paintRadius;
            Detonate();
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = grenadeGravityScale;
            _platformLayersMask = LayerMask.GetMask("platforms_black", "platforms_grey", "platforms_white");
            _fuseRemaining = EffectiveFuseSeconds;
        }

        // Local launch (G1). framework = the colour the detonation will paint;
        // initialVelocity = launch velocity in world units/sec.
        public void Init(Framework framework, Vector2 initialVelocity)
        {
            _framework = framework;
            _fuseRemaining = EffectiveFuseSeconds;
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = grenadeGravityScale;
            _rb.linearVelocity = initialVelocity;

            // Tumble: random tilt + spin, so each throw looks slightly different. Sign is
            // opposite the horizontal throw direction (backward roll off the hand); pure
            // cosmetics on a circle collider, so the ghost rolling its own values is fine.
            float spin = Random.Range(spinDegPerSecMin, spinDegPerSecMax);
            float spinDir = initialVelocity.x != 0f
                ? -Mathf.Sign(initialVelocity.x)
                : (Random.value < 0.5f ? -1f : 1f);
            _rb.angularVelocity = spin * spinDir;
            transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-initialTiltRange, initialTiltRange));

            CreateFuseLight();
            CreateTrail();
        }

        // Owner-side only: lets the thrower detonate this grenade mid-air (the "press G
        // again" juice). Ghosts detonate via the owner's RPC instead.
        public void ForceDetonate()
        {
            if (!_isGhost) Detonate();
        }

        private void CreateFuseLight()
        {
            if (_fuseLight != null) return;
            var go = new GameObject("FuseLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = fuseLightOffset;
            go.transform.localScale = Vector3.one * fuseLightSize;
            _fuseLight = go.AddComponent<SpriteRenderer>();
            _fuseLight.sprite = LightSprite;
            _fuseLight.color = new Color(1f, 1f, 1f, fuseLightAlpha);
            _fuseLight.sortingOrder = 12; // flash IN FRONT of the grenade sprite (order 10)
        }

        // World-space smoke trace: zero gravity, zero speed — squares are simply left where
        // the grenade was and fade out. Purely visual (no collision module, no forces).
        private void CreateTrail()
        {
            if (_trail != null) return;
            var go = new GameObject("GrenadeTrail");
            go.transform.SetParent(transform, false);
            _trail = go.AddComponent<ParticleSystem>();

            var main = _trail.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.startLifetime = TrailLifetime;
            main.startSize = trailParticleSize;
            main.startColor = _framework == Framework.WHITE ? trailColorWhite : trailColorBlack;
            main.maxParticles = 500;

            var emission = _trail.emission;
            emission.rateOverTime = TrailRate;

            var shape = _trail.shape;
            shape.enabled = false;

            var colorOverLifetime = _trail.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingOrder = 8; // below the halo (9) and the grenade (10)
        }

        // The grenade is about to die: let already-emitted trail squares finish their decay
        // instead of vanishing with the parent.
        private void ReleaseTrail()
        {
            if (_trail == null) return;
            _trail.transform.SetParent(null);
            _trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(_trail.gameObject, TrailLifetime + 0.5f);
            _trail = null;
        }

        private void Update()
        {
            if (_detonated) return;
            // Scaled time on purpose: a synced pause (timeScale 0) freezes the fuse.
            _fuseRemaining -= Time.deltaTime;
            _aliveTime += Time.deltaTime;
            if (_fuseLight != null)
            {
                bool lit = (int)(_aliveTime * BlinkHz * 2f) % 2 == 0;
#if UNITY_EDITOR
                lit |= DebugFuseLightAlwaysOn;
#endif
                _fuseLight.enabled = lit;
            }
            if (_fuseRemaining <= 0f) Detonate();
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (_detonated) return;
            // Bounciness (material) reflects the velocity; this decays its magnitude so
            // successive bounces lose height. Two independent tunables by design.
            // Spin decays with the same retention so the tumble settles as the bounces do.
            _rb.linearVelocity *= bounceVelocityRetention;
            _rb.angularVelocity *= bounceVelocityRetention;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_detonated) return;
            // Mirror ShotView: the arena boundary is detected on trigger *exit*.
            if (other.CompareTag(Values.BOUNDRIES_TAG))
            {
                if (detonateOnBoundsExit) Detonate();
                else
                {
                    ReleaseTrail();
                    Destroy(gameObject);
                }
            }
        }

        private void Detonate()
        {
            if (_detonated) return;
            _detonated = true;

            Vector2 pos = transform.position;
            float radius = _isGhost && _ghostPaintRadius > 0f ? _ghostPaintRadius : explosionRadius;
            GrenadeExplosionRing.Spawn(pos, radius, _framework);

            if (!_isGhost)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radius, _platformLayersMask);
                var painted = new HashSet<PlatformManager>();
                foreach (var hit in hits)
                {
                    var pm = hit.GetComponentInParent<PlatformManager>();
                    if (pm == null || painted.Contains(pm)) continue;
                    painted.Add(pm);
                    // Local apply: colour + collision layer + release of mismatched carried
                    // players. ApplyPaintFromNetwork deliberately does NOT re-broadcast, so we
                    // broadcast each platform explicitly below — same shape as the shot-hit and
                    // spawn-repaint paths. THIS is what makes the remote peer's platforms flip.
                    pm.ApplyPaintFromNetwork(_framework);
                    if (PhotonNetwork.InRoom && pm.networkId >= 0)
                    {
                        if (_gameManager == null) _gameManager = FindFirstObjectByType<GameManager>();
                        if (_gameManager != null)
                            _gameManager.BroadcastPaintPlatform(pm.networkId, _framework);
                    }
                }

                // Tell the remote ghost exactly where and how big the boom was (explicit
                // null check, not ?. — the thrower may be a destroyed Unity object mid-reload).
                if (_ownerThrower != null)
                    _ownerThrower.NotifyOwnerDetonated(_ownerGrenadeId, pos, radius);
            }

            // TODO(G6): explosion VFX polish per spec 3.7a (ring + SFX live in GrenadeExplosionRing).
            ReleaseTrail();
            Destroy(gameObject);
        }
    }
}
