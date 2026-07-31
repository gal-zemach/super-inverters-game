using UnityEngine;

namespace Controllers
{
    // Mouse-only aim. Movement and actions stay on keyboard/gamepad controllers.
    public class MouseAimController : Controller
    {
        [SerializeField, Tooltip("World-space distance below which aim is treated as zero")]
        private float aimDeadzone = 0.15f;

        protected override float update_moving_direction() => 0f;

        protected override Vector2 update_aim_direction()
        {
            var cam = Camera.main;
            if (cam == null) return Vector2.zero;

            Vector3 screen = Input.mousePosition;
            screen.z = cam.WorldToScreenPoint(transform.position).z;
            Vector2 world = cam.ScreenToWorldPoint(screen);
            Vector2 delta = world - (Vector2)transform.position;
            if (delta.sqrMagnitude < aimDeadzone * aimDeadzone)
                return Vector2.zero;
            return delta.normalized;
        }

        public override bool jump() => false;
        // Gate on `enabled`: PlayerManager.FixedUpdate polls shoot() on ALL
        // controllers without an enabled check, and this getter reads live input,
        // so a DISABLED MouseAimController would otherwise still fire its player
        // on LMB — e.g. the human's click firing the AI-owned player in single
        // player (its MouseAimController is disabled but still present).
        public override bool shoot() => enabled && Input.GetMouseButton(0);
        public override bool getDown() => false;
        public override bool pauseMenu() => false;
    }
}
