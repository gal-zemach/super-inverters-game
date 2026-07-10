using UnityEngine;

namespace Game.Powerups
{
    // Detonation feedback: an expanding circle showing exactly the paint radius. Spawned by
    // GrenadeProjectile.Detonate (which destroys itself immediately, so this lives on its own
    // GameObject) and self-destroys when the expansion finishes. Local-only, like the rest of
    // the grenade visuals — the remote peer gets it with the G5 ghost.
    public class GrenadeExplosionRing : MonoBehaviour
    {
        private const int Segments = 48;
        private const float StartRadius = 0.5f;
        private const float ExpandSeconds = 0.35f;
        private const float RingWidth = 0.3f;

        private const string BoomClipResourcePath = "Audio/grenade_explosion";
        private const float BoomVolume = 0.35f;

        private static AudioClip s_boomClip;
        private static bool s_boomLoadAttempted;

        private LineRenderer _ring;
        private float _maxRadius;
        private float _elapsed01;
        private Color _color;

        // framework = the paint colour, so the ring matches what the explosion painted.
        public static void Spawn(Vector2 center, float maxRadius, Framework framework)
        {
            var go = new GameObject("GrenadeExplosionRing");
            go.transform.position = center;
            var ring = go.AddComponent<GrenadeExplosionRing>();
            ring._maxRadius = maxRadius;
            ring._color = framework == Framework.WHITE ? Color.white : Color.black;
            PlayBoom(center);
        }

        // 2D one-shot on a throwaway GameObject: the ring outlives its short expansion, and
        // AudioSource.PlayClipAtPoint would be 3D-attenuated, near-silent at this camera range.
        private static void PlayBoom(Vector2 pos)
        {
            if (!s_boomLoadAttempted)
            {
                s_boomLoadAttempted = true;
                s_boomClip = Resources.Load<AudioClip>(BoomClipResourcePath);
                if (s_boomClip == null)
                    Debug.LogWarning($"GrenadeExplosionRing: no AudioClip at Resources/{BoomClipResourcePath} — explosion is silent.");
            }
            if (s_boomClip == null) return;

            var go = new GameObject("GrenadeBoomSFX");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = s_boomClip;
            src.volume = BoomVolume;
            src.spatialBlend = 0f;
            src.Play();
            Destroy(go, s_boomClip.length + 0.1f);
        }

        private void Awake()
        {
            _ring = gameObject.AddComponent<LineRenderer>();
            _ring.useWorldSpace = false;
            _ring.loop = true;
            _ring.positionCount = Segments;
            _ring.material = new Material(Shader.Find("Sprites/Default"));
            _ring.sortingOrder = 60;
            _ring.startWidth = RingWidth;
            _ring.endWidth = RingWidth;
        }

        private void Update()
        {
            // Scaled time on purpose, same as the fuse: a synced pause freezes the ring too.
            _elapsed01 += Time.deltaTime / ExpandSeconds;
            if (_elapsed01 >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            float eased = 1f - (1f - _elapsed01) * (1f - _elapsed01); // ease-out: fast pop, soft finish
            float radius = Mathf.Lerp(StartRadius, _maxRadius, eased);
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }

            Color c = _color;
            c.a = 1f - _elapsed01; // fade out as it expands
            _ring.startColor = c;
            _ring.endColor = c;
        }
    }
}
