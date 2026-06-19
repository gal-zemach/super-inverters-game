using Game;
using UnityEngine;

namespace Multiplayer
{
    // Shared raycast used by spawn gizmos, editor handles, and GameManager paint-at-spawn.
    public static class SpawnPlatformPreview
    {
        public const float RayDistance = 12f;

        // BlackPlayer/WhitePlayer root scale 1.25, BoxCollider2D offset (0, 0.3) size (2.7, 4.4).
        // Transform Y must sit this far above the platform hit point so the collider bottom
        // clears the surface (was 0.3 — far too low, spawned body inside the platform).
        private const float PlayerRootScaleY = 1.25f;
        private const float PlayerColliderOffsetY = 0.3f;
        private const float PlayerColliderHalfHeight = 4.4f / 2f;
        private const float PlayerFootClearanceY = 0.08f;
        public const float PlayerFootOffsetY =
            PlayerRootScaleY * (PlayerColliderHalfHeight - PlayerColliderOffsetY) + PlayerFootClearanceY;

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
