using UnityEditor;
using UnityEngine;
using Game;

// Scene-view spawn preview for the single-player levels (same spirit as the
// multiplayer spawn drag-handles): every scene-placed player shows its spawn
// marker and a projected fall line to the surface it will actually land on.
//
//   GREEN  — lands on a static platform (safe; SpawnPlatformPainter recolours
//            the first two platforms in the column to the player's colour at
//            round start, so ANY platform below counts)
//   YELLOW — lands on a MOVING platform (its patrol path is drawn too;
//            it may not be there when the player drops)
//   RED    — nothing below: the player falls into the void
//
// Move the player in the Scene view until the line is green, then save the
// scene.
public static class SpawnPreviewGizmos
{
	private const float FallProbeDistance = 200f;

	[DrawGizmo(GizmoType.NonSelected | GizmoType.Selected, typeof(PlayerState))]
	private static void DrawSpawnPreview(PlayerState state, GizmoType gizmoType)
	{
		if (Application.isPlaying) return;

		Vector3 spawn = state.transform.position;
		bool isBlack = state.player_framework == Framework.BLACK;
		Color playerColor = isBlack ? Color.black : Color.white;

		// Spawn marker: filled disc in the player's colour with a contrast ring.
		Gizmos.color = playerColor;
		Gizmos.DrawSphere(spawn, 0.8f);
		Gizmos.color = isBlack ? Color.white : Color.black;
		Gizmos.DrawWireSphere(spawn, 1.0f);

		// Project the fall: first platform below in ANY colour (the runtime
		// SpawnPlatformPainter recolours the landing column to this player's
		// colour at round start), or the floor.
		int mask = LayerMask.GetMask(Values.BLACK_PLATFORM_LAYER, Values.WHITE_PLATFORM_LAYER,
			Values.GREY_PLATFORM_LAYER, "floor");
		RaycastHit2D landing = default;
		foreach (var hit in Physics2D.RaycastAll(spawn, Vector2.down, FallProbeDistance, mask))
		{
			if (hit.collider.transform.IsChildOf(state.transform)) continue;
			landing = hit;
			break;
		}

		if (landing.collider == null)
		{
			Gizmos.color = Color.red;
			Gizmos.DrawLine(spawn, spawn + Vector3.down * 40f);
			Handles.Label(spawn + Vector3.down * 6f, "  VOID — will die");
			return;
		}

		var pm = landing.collider.GetComponentInParent<PlatformManager>();
		bool moving = IsMover(pm);

		Gizmos.color = moving ? Color.yellow : Color.green;
		Gizmos.DrawLine(spawn, landing.point);
		Gizmos.DrawWireCube(landing.point, new Vector3(2.5f, 0.4f, 0f));
		Handles.Label((Vector3)landing.point + Vector3.down * 1.2f,
			moving ? "  MOVING platform — may not be there!"
			       : "  lands safely (painted to player colour at spawn)");

		if (moving) DrawMoverPath(pm);
	}

	// isMovingPlatform is computed at runtime and always false in edit mode;
	// a platform with more than one path point moves.
	private static bool IsMover(PlatformManager pm)
	{
		if (pm == null) return false;
		var points = new SerializedObject(pm).FindProperty("points");
		return points != null && points.arraySize > 1;
	}

	private static void DrawMoverPath(PlatformManager pm)
	{
		var so = new SerializedObject(pm);
		var points = so.FindProperty("points");
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
