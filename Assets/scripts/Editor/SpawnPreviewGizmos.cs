using UnityEditor;
using UnityEngine;
using Game;

// Scene-view spawn preview for the single-player levels (same spirit as the
// multiplayer spawn drag-handles): every scene-placed player shows its spawn
// marker and a projected fall line to the surface it will actually land on.
//
// CRITICAL detail this preview accounts for: at round start every moving
// platform TELEPORTS to its first path point (PlatformManager.Init sets the
// view to points[0]) — its edit-time resting spot is irrelevant. The
// projection therefore shifts every mover to its round-start position first,
// and draws that footprint so you can see where the platform really begins.
//
//   GREEN  — lands on a static platform (safe; SpawnPlatformPainter recolours
//            the landing column to the player's colour at round start, so ANY
//            platform counts)
//   YELLOW — lands on a MOVING platform at its round-start position (footprint
//            + patrol path drawn; it departs as the round runs)
//   RED    — nothing beneath the spawn at round start: the player dies
//
// Move the player in the Scene view until the line is green (or yellow with a
// tiny drop), then save the scene.
public static class SpawnPreviewGizmos
{
	[DrawGizmo(GizmoType.NonSelected | GizmoType.Selected, typeof(PlayerState))]
	private static void DrawSpawnPreview(PlayerState state, GizmoType gizmoType)
	{
		if (Application.isPlaying) return;

		Vector3 spawn = state.transform.position;
		bool isBlack = state.player_framework == Framework.BLACK;

		// Spawn marker: filled disc in the player's colour with a contrast ring.
		Gizmos.color = isBlack ? Color.black : Color.white;
		Gizmos.DrawSphere(spawn, 0.8f);
		Gizmos.color = isBlack ? Color.white : Color.black;
		Gizmos.DrawWireSphere(spawn, 1.0f);

		// Round-start snapshot: every platform's collider bounds, movers shifted
		// to points[0]. Pure bounds math — physics queries would test edit-time
		// positions, which is exactly the trap this preview exists to avoid.
		Bounds best = default;
		bool found = false, bestMoving = false;
		PlatformManager bestPm = null;
		foreach (var pm in Object.FindObjectsOfType<PlatformManager>())
		{
			var col = pm.GetComponentInChildren<Collider2D>();
			if (col == null) continue;
			Bounds b = col.bounds;
			bool moving = false;
			var points = new SerializedObject(pm).FindProperty("points");
			if (points != null && points.arraySize > 0)
			{
				var p0 = points.GetArrayElementAtIndex(0).objectReferenceValue as Transform;
				if (p0 != null)
				{
					b.center += p0.position - col.transform.position;
					moving = points.arraySize > 1;
				}
			}
			if (spawn.x < b.min.x || spawn.x > b.max.x) continue;
			if (b.max.y > spawn.y + 0.5f) continue;                   // platform starts above the spawn
			if (found && b.max.y <= best.max.y) continue;             // keep the highest surface
			best = b; found = true; bestMoving = moving; bestPm = pm;
		}

		if (!found)
		{
			Gizmos.color = Color.red;
			Gizmos.DrawLine(spawn, spawn + Vector3.down * 40f);
			Handles.Label(spawn + Vector3.down * 6f, "  VOID at round start — will die");
			return;
		}

		Vector3 landing = new Vector3(spawn.x, best.max.y, 0f);
		Gizmos.color = bestMoving ? Color.yellow : Color.green;
		Gizmos.DrawLine(spawn, landing);
		Gizmos.DrawWireCube(best.center, best.size);                  // round-start footprint
		Handles.Label(landing + Vector3.down * 1.2f,
			bestMoving ? "  lands on MOVER at its round-start spot (footprint shown)"
			           : "  lands safely (painted to player colour at spawn)");

		if (bestMoving) DrawMoverPath(bestPm);
	}

	private static void DrawMoverPath(PlatformManager pm)
	{
		var points = new SerializedObject(pm).FindProperty("points");
		if (points == null) return;
		Gizmos.color = Color.yellow;
		for (int i = 1; i < points.arraySize; i++)
		{
			var a = points.GetArrayElementAtIndex(i - 1).objectReferenceValue as Transform;
			var b = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
			if (a != null && b != null) Gizmos.DrawLine(a.position, b.position);
		}
	}
}
