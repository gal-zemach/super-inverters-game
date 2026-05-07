using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Utils.Utils;


namespace Game{
	public class PlatformManager : MonoBehaviour {

		// Slice 5 phase 2b: deterministic per-platform id assigned by
		// GameManager.AssignPlatformNetworkIds at scene start. Used as the
		// key in the paint RPC so both peers know which platform is being
		// repainted. -1 = unassigned (single-player path doesn't touch it).
		[HideInInspector] public int networkId = -1;

		[SerializeField] protected List<Transform> points;

		[SerializeField] public int beats_per_cycle = 4;

		[SerializeField] protected float cycle_period;

		[SerializeField] protected float segment_period;

		[SerializeField] static public int init_num_lives = 1;
		
		[SerializeField] public Framework init_platform_framework = Framework.GREY;

		[SerializeField] public bool isMovingPlatform;

		protected int target_point_idx = 0;

		protected int current_point_idx = 0;

		protected bool reverse_dir = false;

		protected float initial_lerp_time;

		protected GameManager game_manager;

		protected PlatformState platform_state;
		
		protected PlatformView platform_view;


		void Awake() {
			game_manager = GetComponentInParent<GameManager>();
			AssignState();
			AssignView();
		}

		private void AssignState() {
			platform_state = GetComponent<PlatformState>();
		}

		private void AssignView() {
//			platform_view = transform.Find(Values.PLATFORM_BODY_GAMEOBJ_NAME).GetComponent<PlatformView>();
			platform_view = gameObject.FindComponentInChildWithTag<PlatformView>(Values.PLATFORM_BODY_TAG);
			platform_view.Init();
		}

		// Use this for initialization
		void Start () {
			Init();
		}

		protected void Init() {
			InitPath();

			if (init_platform_framework == Framework.GREY)
			{
				init_platform_framework = (Framework) Random.Range(1, 3);
			}

			InitState();
			UpdateFramework(init_platform_framework);
			UpdateSegmentPeriod();
		}

		public void AddPoint(GameObject point) {
			if (platform_view == null) {
				AssignView();
			}

			point.transform.parent = transform.parent.Find(Values.PLATFORM_PATH_GAMEOBJ_NAME);
			point.transform.position = transform.position; // set according to global transform, so assign according to this object's position
			point.name = Values.PLATFORM_PATH_POINT_PREFIX + (points.Count+1).ToString();
			points.Add(point.transform);
		}


		private void InitPath() {
			initial_lerp_time = Time.time;
			if (points.Count > 0) {
				platform_view.Position = points[0].position;
				current_point_idx = 0;
			}
			if (points.Count > 1) {
				target_point_idx = 1;
			}
			reverse_dir = (points.Count<=2);
		}

		private void InitState() {
			platform_state.num_lives = init_num_lives;
		}

		public void UpdateSegmentPeriod() {
			cycle_period = beats_per_cycle*60.0f/GetComponentInParent<GameManager>().BPM; // seconds per cycle
			int num_of_segments = (points.Count -1)*2; //for the whole cycle
			segment_period = cycle_period/num_of_segments; //each segment will have the same period, regardless of distance of segment
		}

		// Update is called once per frame
		void Update () {
			
		}
			

		public bool V3Equal(Vector3 a, Vector3 b){
			return Vector3.SqrMagnitude(a - b) < 0.001;
		}


		protected float GetPathPercentage() {
			return (Time.time - initial_lerp_time)/segment_period;
		}

		// This is used only from editor to mock movement of platform
		public void SetPosition(float cycle_percentage) {
			if (points.Count < 2) {
				return;
			}
			int source_point_idx, target_point_idx;
			source_point_idx = Mathf.FloorToInt(cycle_percentage*(points.Count-1)*2);
			source_point_idx = source_point_idx < points.Count ? source_point_idx : 2*(points.Count-1) - source_point_idx; 
			target_point_idx = Mathf.CeilToInt(cycle_percentage*(points.Count-1)*2);
			target_point_idx = target_point_idx < points.Count ? target_point_idx : 2*(points.Count-1) - target_point_idx;
//			Debug.Log("PlatformManager idx: " + source_point_idx.ToString() + ", " + target_point_idx.ToString() );
			int num_of_paths = (points.Count-1)*2;
			float path_percentage = cycle_percentage*num_of_paths % 1;
			Vector2 pos = Vector2.Lerp(points[source_point_idx].position, points[target_point_idx].position, path_percentage);

//			if (platform_view == null) {
//				AssignView();
//			}
//			platform_view.Position =pos;
			transform.position = pos;

		}

		protected void FixedUpdate() {
			if (points.Count != 0) {
				float path_percentage = GetPathPercentage();
				if (path_percentage <= 1.0f) {
//					Debug.Log(current_point_idx + ") " + points[current_point_idx].position + " , " + target_point_idx + ") " + points[target_point_idx].position);
					Vector2 pos = Vector2.Lerp(points[current_point_idx].position, points[target_point_idx].position, path_percentage);
					platform_state.Position = pos;
					platform_view.Position = platform_state.Position;
				} 
				else {
					UpdateSourceTargetPoints();
				}
			}
		}

		protected void UpdateSourceTargetPoints() {
			current_point_idx = target_point_idx;
			target_point_idx = reverse_dir ? target_point_idx - 1 : target_point_idx + 1;
			if (target_point_idx == 0 || target_point_idx == points.Count-1) {
				reverse_dir = !reverse_dir;
			}
			initial_lerp_time = Time.time;
		}

		public void UpdateHit(Framework platform_framework) {
			platform_state.num_lives--;
			if(platform_state.num_lives <= 0) {
				platform_state.num_lives = init_num_lives;
				UpdateFramework(platform_framework);

				// Slice 5 phase 2b: in a Photon room, broadcast the new color
				// to other peers so their local view of this platform also
				// flips. Without this each peer only sees the platforms
				// they shot themselves; physics diverges.
				if (PhotonNetwork.InRoom && game_manager != null)
				{
					game_manager.BroadcastPaintPlatform(networkId, platform_framework);
				}
			}
		}

		// Slice 5 phase 2b: applied by GameManager.RPCPaintPlatform on the
		// receiving peer. Same effect as UpdateFramework but does NOT
		// re-broadcast (avoids a paint feedback loop).
		public void ApplyPaintFromNetwork(Framework framework)
		{
			SetFramework(framework);
			if (game_manager != null && platform_view != null)
			{
				game_manager.ChangeLayer(platform_view.gameObject, framework);
			}
		}

		// Updates platform state/view
		public void SetFramework(Framework platform_framework) {
//			Debug.Log(gameObject.name + ": SetFramework");
			if (platform_state == null) {
				AssignState();
			}
			platform_state.platform_framework = platform_framework;
			if (platform_view == null) {
				AssignView();
			}
			platform_view.SetColor(platform_framework);
		}

		// This updates the new framework within game_manager as well as updating platform state/view
		private void UpdateFramework(Framework platform_framework) {
			SetFramework(platform_framework);
			game_manager.ChangeLayer(platform_view.gameObject, platform_framework);
		}


	}
}

