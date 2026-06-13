using UnityEngine;

namespace GingerVoxelSystem.Player
{
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
        }

        private void Update()
        {
            // Raw mouse input for direct, no-frills control
            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity * 100f * Time.deltaTime;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity * 100f * Time.deltaTime;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Rotate the player body on the Y axis (left/right)
            playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Rotate the camera on the X axis (up/down)
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
