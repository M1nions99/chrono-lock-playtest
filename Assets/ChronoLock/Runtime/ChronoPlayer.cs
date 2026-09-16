using UnityEngine;
using UnityEngine.InputSystem;

namespace ChronoLock
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ChronoPlayer : MonoBehaviour
    {
        public Camera view;
        public ChronoGame game;
        CharacterController controller;
        float pitch, vertical, bob, landing;
        bool wasGrounded, sprinting;
        public float sensitivity = .085f;
        void Awake()
        {
            controller = GetComponent<CharacterController>();
            // The body remains physical, but must never occlude its own interaction ray.
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }
        void Update()
        {
            var k = Keyboard.current;
            var m = Mouse.current;
            sprinting = false;
            if (!game) return;
            if (k != null && k.escapeKey.wasPressedThisFrame) { game.Interface?.Back(); return; }
            if (game.IsPlaying && k != null && k.rKey.wasPressedThisFrame) { game.Interface?.RequestRestart(); return; }
            if (!game.IsPlaying || !game.InputReady || k == null || m == null) return;
            var options = ChronoPreferences.Runtime;
            Vector2 look = m.delta.ReadValue() * sensitivity * options.sensitivity;
            transform.Rotate(0, look.x, 0);
            pitch = Mathf.Clamp(pitch + look.y * (options.invertY ? 1 : -1), -82, 82);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
            float z = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
            Vector3 move = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1);
            if (controller.isGrounded && vertical < 0) vertical = -2;
            if (controller.isGrounded && k.spaceKey.wasPressedThisFrame) vertical = 5.2f;
            vertical -= 18 * Time.deltaTime;
            bool sprint = k.leftShiftKey.isPressed && move.sqrMagnitude > .1f;
            sprinting = sprint;
            controller.Move((move * (sprint ? 7 : 4.5f) + Vector3.up * vertical) * Time.deltaTime);
            if (options.cameraMotion && controller.isGrounded && !wasGrounded && vertical < -5) landing = -.045f;
            wasGrounded = controller.isGrounded;
            if (options.cameraMotion)
            {
                landing = Mathf.Lerp(landing, 0, 10 * Time.deltaTime);
                bob += Time.deltaTime * (sprint ? 13 : 9) * move.magnitude;
            }
            else { landing = 0; bob = 0; }
            float offset = options.cameraMotion && controller.isGrounded ? Mathf.Sin(bob) * .012f * move.magnitude : 0;
            view.transform.localPosition = new Vector3(0, 1.62f + offset + landing, 0);
            game.HandleInput(k, m);
        }
        void LateUpdate()
        {
            if (!view) return;
            var options = ChronoPreferences.Runtime;
            bool playing = game && game.IsPlaying;
            float targetFov = options.fov + (options.cameraMotion && playing && sprinting ? 5 : 0);
            // Settings must take effect while the pause/settings menu is open too.
            view.fieldOfView = playing && options.cameraMotion ? Mathf.Lerp(view.fieldOfView, targetFov, 7 * Time.unscaledDeltaTime) : targetFov;
            if (!options.cameraMotion)
            { landing = 0; bob = 0; view.transform.localPosition = new Vector3(0, 1.62f, 0); }
        }
        public void Teleport(Vector3 position)
        {
            if (!controller) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            pitch = 0; vertical = 0; landing = 0; bob = 0; wasGrounded = false; sprinting = false;
            if (view) { view.transform.localRotation = Quaternion.identity; view.transform.localPosition = new Vector3(0, 1.62f, 0); }
            controller.enabled = true;
        }
        void OnApplicationFocus(bool focus) { if (!focus && game && game.HasStarted && !game.Paused && !game.Completed) game.Pause(); }
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
