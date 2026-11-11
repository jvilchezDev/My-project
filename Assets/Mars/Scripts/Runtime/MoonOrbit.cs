using UnityEngine;

namespace Mars
{
    /// <summary>
    /// Simple circular orbit around a center transform (used for Phobos and Deimos).
    /// Radius is in world units; period in seconds.
    /// </summary>
    public class MoonOrbit : MonoBehaviour
    {
        [SerializeField] private Transform center;
        [SerializeField] private float radius = 3f;
        [Tooltip("Orbital period in seconds (real time).")]
        [SerializeField] private float period = 30f;
        [SerializeField] private float inclinationDegrees = 1.8f;
        [SerializeField] private float startAngleDegrees = 0f;

        private void Update()
        {
            if (center == null) return;
            float angle = (startAngleDegrees + 360f * Time.time / Mathf.Max(0.01f, period)) * Mathf.Deg2Rad;
            Vector3 local = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            transform.position = center.position + Quaternion.Euler(inclinationDegrees, 0f, 0f) * local;
        }
    }
}
