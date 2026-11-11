using UnityEngine;
using UnityEngine.InputSystem;

namespace Mars
{
    /// <summary>
    /// Orbital camera around a target. Left/right mouse drag rotates, scroll wheel zooms.
    /// Uses the new Input System (the project's active input handler).
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 6f;
        [SerializeField] private float minDistance = 2.5f;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] private float rotateSensitivity = 0.25f;
        [SerializeField] private float zoomSensitivity = 0.01f;
        [SerializeField] private float yaw = 20f;
        [SerializeField] private float pitch = 10f;
        [SerializeField] private float smoothing = 12f;

        private float targetYaw, targetPitch, targetDistance;

        private void Start()
        {
            targetYaw = yaw;
            targetPitch = pitch;
            targetDistance = distance;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.isPressed || mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    targetYaw += delta.x * rotateSensitivity;
                    targetPitch = Mathf.Clamp(targetPitch - delta.y * rotateSensitivity, -85f, 85f);
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSensitivity, minDistance, maxDistance);
                }
            }

            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, targetYaw, t);
            pitch = Mathf.Lerp(pitch, targetPitch, t);
            distance = Mathf.Lerp(distance, targetDistance, t);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = target.position + rot * new Vector3(0f, 0f, -distance);
            transform.LookAt(target.position);
        }
    }
}
