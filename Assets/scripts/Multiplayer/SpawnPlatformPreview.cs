using Game;
using UnityEngine;

namespace Multiplayer
{
    // Shared raycast used by spawn gizmos, editor handles, and GameManager paint-at-spawn.
    public static class SpawnPlatformPreview
    {
        public const float RayDistance = 12f;
        /// <summary>Feet sit slightly above the platform surface hit point (matches spawn editor gizmos).</summary>
        public const float PlayerFootOffsetY = 0.3f;

        public static Vector2 ResolvePlayerStandPosition(Vector2 spawnAnchor)
        {
            if (TryGetLandingPoint(spawnAnchor, out Vector2 landing, out _))
                return landing + Vector2.up * PlayerFootOffsetY;
            return spawnAnchor;
        }

        public static bool TryGetLandingPoint(Vector2 spawnWorldPos, out Vector2 landingPoint, out PlatformManager platform)
        {
            landingPoint = spawnWorldPos;
            platform = null;
            float bestDist = float.MaxValue;

            foreach (var hit in Physics2D.RaycastAll(spawnWorldPos, Vector2.down, RayDistance))
            {
                var p = hit.collider.GetComponentInParent<PlatformManager>();
                if (p == null) continue;
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    platform = p;
                    landingPoint = hit.point;
                }
            }

            if (platform != null) return true;

            foreach (var hit in Physics2D.RaycastAll(spawnWorldPos, Vector2.up, 2f))
            {
                var p = hit.collider.GetComponentInParent<PlatformManager>();
                if (p == null) continue;
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    platform = p;
                    landingPoint = hit.point;
                }
            }

            return platform != null;
        }
    }
}
