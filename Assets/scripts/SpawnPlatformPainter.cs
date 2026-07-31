using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Game
{
	// Single-player round start: paint each player's landing column their colour
	// so the spawn drop always has standable ground — the first platform below
	// the spawn, and the one underneath it as a fallback (if there is one).
	// Runs one frame after load so every PlatformManager has done Init() (grey
	// platforms have rolled a colour, movers have teleported to their path
	// start). Multiplayer is untouched: per-level spawn platforms are part of
	// the MP scene template, and painting here would race the paint sync.
	public class SpawnPlatformPainter : MonoBehaviour
	{
		private const int PlatformsPerColumn = 2;
		private const float ProbeDistance = 200f;

		private IEnumerator Start()
		{
			if (PhotonNetwork.InRoom) yield break;
			yield return null;

			foreach (var playerGo in GameObject.FindGameObjectsWithTag(Values.PLAYER_TAG))
			{
				var state = playerGo.GetComponent<PlayerState>();
				if (state == null) continue;
				PaintLandingColumn(playerGo.transform.position, state.player_framework);
			}
		}

		private static void PaintLandingColumn(Vector3 spawn, Framework framework)
		{
			int mask = LayerMask.GetMask(Values.BLACK_PLATFORM_LAYER,
				Values.WHITE_PLATFORM_LAYER, Values.GREY_PLATFORM_LAYER);
			var hits = Physics2D.RaycastAll(spawn, Vector2.down, ProbeDistance, mask);
			System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

			int painted = 0;
			var seen = new HashSet<PlatformManager>();
			foreach (var hit in hits)
			{
				var pm = hit.collider.GetComponentInParent<PlatformManager>();
				if (pm == null || !seen.Add(pm)) continue;
				pm.ApplyPaintFromNetwork(framework);
				if (++painted >= PlatformsPerColumn) break;
			}
		}
	}
}
