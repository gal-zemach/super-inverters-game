using UnityEngine;

namespace Game.Powerups
{
    // Spawns the real grenade from the player, coloured by the thrower's framework.
    // Slice G2: local spawn only. The ghost RPC that mirrors the throw on the remote
    // peer is added in Slice G5 (see the TODO).
    public class GrenadeThrower : MonoBehaviour
    {
        [Tooltip("The Grenade.prefab to throw.")]
        [SerializeField] private GrenadeProjectile grenadePrefab;

        [Tooltip("Spawn distance from the player centre along the aim direction, so the " +
                 "grenade clears the player's own body.")]
        [SerializeField] private float spawnOffset = 1.2f;

        private PlayerState _playerState;

        private void Awake() => _playerState = GetComponent<PlayerState>();

        // dir must be normalized; force is world units/sec.
        public void Throw(Vector2 dir, float force)
        {
            if (grenadePrefab == null)
            {
                Debug.LogWarning("GrenadeThrower: no grenadePrefab assigned.");
                return;
            }

            Framework framework = _playerState != null ? _playerState.player_framework : Framework.BLACK;
            Vector2 spawnPos = (Vector2)transform.position + dir * spawnOffset;

            GrenadeProjectile g = Instantiate(grenadePrefab, spawnPos, Quaternion.identity);
            g.Init(framework, dir * force);

            // TODO(G5): gameManager.photonView.RPC(RPCSpawnGhostGrenade, RpcTarget.Others,
            //           spawnPos, dir * force, (int)framework, fuseSeconds, grenadeId)
        }
    }
}
