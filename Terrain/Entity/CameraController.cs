using UnityEngine;

namespace GingerVoxelSystem.Player
{
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Transform playerBody; // the object with SdfPlayerController

        [Header("Settings")]
        public float mouseSensitivity = 2.5f;
        public float minPitch = -85f;
        public float maxPitch = 85f;

        float yaw;
        float pitch;

        void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            yaw = playerBody.eulerAngles.y;
            pitch = transform.localEulerAngles.x;
        }

        void Update()
        {
            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity * 100f * Time.deltaTime;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity * 100f * Time.deltaTime;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Rotate player body (Y only)
            playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Rotate camera (X only)
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
