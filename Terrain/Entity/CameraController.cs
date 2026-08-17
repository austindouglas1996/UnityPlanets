namespace MarchingTerrain.Player
{
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Very basic FPS-style camera controller.
    ///
    /// This exists mainly to make testing and moving around the terrain easier.
    /// Regular Unity physics/collisions are avoided here since they get expensive,
    /// and anything that would require reading data back from the GPU is a no-go.
    ///
    /// Not meant to be fancy or final just something that works well enough
    /// while developing the terrain system.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform playerBody;

        [Header("Settings")]
        public float mouseSensitivity = 2.5f;
        public float minPitch = -85f;
        public float maxPitch = 85f;

        float yaw;
        float pitch;

        private void Awake()
        {
            // Lock and hide the cursor for FPS-style mouse look
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Initialize rotation values from the current transforms
            yaw = playerBody.eulerAngles.y;

            pitch = transform.localEulerAngles.x;
            if (pitch > 180f)
                pitch -= 360f;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
                return;

            Vector2 mouseDelta = mouse.delta.ReadValue();

            float mouseX = mouseDelta.x * mouseSensitivity;
            float mouseY = mouseDelta.y * mouseSensitivity;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Rotate the player body on the Y axis.
            playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Rotate the camera on the X axis.
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
