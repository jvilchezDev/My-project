using UnityEngine;

namespace Mars
{
    /// <summary>
    /// Rotates a planet around its own axis, tilted by axisTiltDegrees (Mars ≈ 25°).
    /// Speed is set from the Inspector in degrees per second (negative = reverse).
    /// </summary>
    [ExecuteAlways]
    public class PlanetRotator : MonoBehaviour
    {
        [Tooltip("Rotation speed in degrees per second.")]
        [SerializeField] private float rotationSpeed = 5f;

        [Tooltip("Axial tilt relative to the orbital plane (Mars ≈ 25°).")]
        [Range(-90f, 90f)]
        [SerializeField] private float axisTiltDegrees = 25f;

        public float RotationSpeed { get => rotationSpeed; set => rotationSpeed = value; }
        public float AxisTiltDegrees { get => axisTiltDegrees; set => axisTiltDegrees = value; }

        private void Update()
        {
            // Tilt the spin axis, then spin around the planet's local Y axis.
            transform.localRotation = Quaternion.Euler(axisTiltDegrees, 0f, 0f) *
                                      Quaternion.AngleAxis(Time.time * rotationSpeed, Vector3.up);
        }
    }
}
