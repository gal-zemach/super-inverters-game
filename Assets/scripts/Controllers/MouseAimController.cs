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
        public override bool shoot() => Input.GetMouseButton(0);
        public override bool getDown() => false;
        public override bool pauseMenu() => false;
    }
}
