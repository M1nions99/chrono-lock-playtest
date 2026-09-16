using UnityEngine;
using UnityEngine.InputSystem;
using ChronoLock;

namespace ChronoStation
{
    [DefaultExecutionOrder(40)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class StationPlayer : MonoBehaviour
    {
        public StationGame game;
        public Camera view;
        public Camera View => view;
        public float MovementAmount { get; private set; }
        public bool Grounded => controller && controller.isGrounded;
        public float VerticalSpeed => velocityY * (game && game.SelfActive == StationAbility.Slow ? SlowAirTimeScale : 1);
        const float JumpSpeed = 6.2f, Gravity = 20f, SlowAirTimeScale = .2f;
        CharacterController controller;
        float pitch, velocityY, motion;
        Transform support;
        Vector3 supportPoint;
        public bool AutomatedInput { get; set; }
        public Vector2 TestMove { get; set; }
        public bool TestJump { get; set; }

        void Awake() { controller = GetComponent<CharacterController>(); }
        void Update()
        {
            var settings = ChronoPreferences.Runtime;
            if (view) view.fieldOfView = settings.fov;
            MovementAmount = 0;
            if (!game || !game.IsPlaying || !game.AcceptsInput) return;
            if (!AutomatedInput)
            {
                var mouse = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
                transform.Rotate(0, mouse.x * settings.sensitivity * .12f, 0);
                pitch = Mathf.Clamp(pitch + mouse.y * settings.sensitivity * (settings.invertY ? .12f : -.12f), -80, 80);
            }
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            if (game.Rewinding || game.SelfActive == StationAbility.Stop) { support = null; return; }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (support)
            {
                var current = support.TransformPoint(supportPoint);
                controller.Move(current - transform.position);
            }
            bool slow = game.SelfActive == StationAbility.Slow;
            float speed = game.SelfActive == StationAbility.Accelerate ? 9.6f : slow ? 2.9f : 4.2f;
            var keyboard = Keyboard.current;
            Vector2 input = AutomatedInput ? TestMove : keyboard == null ? Vector2.zero : new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            input = Vector2.ClampMagnitude(input, 1);
            bool jump = AutomatedInput ? TestJump : keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
            TestJump = false;
            if (controller.isGrounded)
            {
                if (velocityY < 0) velocityY = -2;
                if (jump) velocityY = JumpSpeed;
            }
            // Keep velocity in the player's own time. Changing the rate changes airtime,
            // never the remaining height of the trajectory, even when toggled mid-jump.
            float verticalDt = dt * (slow ? SlowAirTimeScale : 1);
            float verticalDistance = velocityY * verticalDt - .5f * Gravity * verticalDt * verticalDt;
            velocityY -= Gravity * verticalDt;
            if (slow)
            {
                velocityY = Mathf.Max(velocityY, -1.65f / SlowAirTimeScale);
                verticalDistance = Mathf.Max(verticalDistance, -1.65f * dt);
            }
            Vector3 movement = (transform.right * input.x + transform.forward * input.y) * speed;
            var collision = controller.Move(movement * dt + Vector3.up * verticalDistance);
            if ((collision & CollisionFlags.Above) != 0 && velocityY > 0) velocityY = 0;
            MovementAmount = input.magnitude;
            support = null;
            if (velocityY <= 0 && Physics.Raycast(transform.position + Vector3.up * .12f, Vector3.down, out var hit, .42f, ~(1 << 30), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<StationTemporalObject>())
                { support = hit.transform; supportPoint = support.InverseTransformPoint(transform.position); }
            }
            if (settings.cameraMotion && controller.isGrounded) motion += dt * MovementAmount * 8;
            view.transform.localPosition = new Vector3(0, 1.62f + (settings.cameraMotion ? Mathf.Sin(motion) * .018f * MovementAmount : 0), 0);
        }
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (!controller) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            velocityY = 0; pitch = 0; support = null; motion = 0;
            view.transform.localPosition = new Vector3(0, 1.62f, 0);
            view.transform.localRotation = Quaternion.identity;
            Physics.SyncTransforms();
        }
        public void LookAt(Vector3 target)
        {
            Vector3 direction = target - view.transform.position;
            transform.rotation = Quaternion.Euler(0, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0);
            pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
    }
}
