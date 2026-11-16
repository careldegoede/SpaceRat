using UnityEngine;

namespace SpaceRat.Scene02
{
    /// <summary>
    /// Navigation readouts toward the signal source (the planet): distance to the surface, how far off
    /// the bearing the nose is, a flickering signal strength that rises with proximity, and travel progress.
    /// </summary>
    public class SignalTracker : MonoBehaviour
    {
        [SerializeField] Transform ship;
        [SerializeField] Transform target;
        [SerializeField] float targetRadius = 700f;
        [Tooltip("Signal reads ~100% at this distance from the surface.")]
        [SerializeField] float fullSignalDistance = 1500f;

        float _startDistance = -1f;

        /// <summary>When set, the signal readout glitches (scripted distortion moments).</summary>
        public bool Distorted { get; set; }

        public Transform Target => target;
        public float TargetRadius => targetRadius;
        public float DistanceToSurface => Mathf.Max(0f, Vector3.Distance(ship.position, target.position) - targetRadius);
        public float BearingAngle => Vector3.Angle(ship.forward, target.position - ship.position);
        public float Progress => _startDistance <= 0f ? 0f : Mathf.Clamp01(1f - (DistanceToSurface - fullSignalDistance) / (_startDistance - fullSignalDistance));

        public float SignalStrength
        {
            get
            {
                float s = Mathf.Lerp(0.04f, 1f, Progress);
                float flicker = (Mathf.PerlinNoise(Time.time * 2.3f, 0.5f) - 0.5f) * 0.12f;
                if (Distorted) flicker = (Mathf.PerlinNoise(Time.time * 25f, 0.1f) - 0.5f) * 0.9f;
                return Mathf.Clamp01(s + flicker);
            }
        }

        void Start() => _startDistance = DistanceToSurface;

        /// <summary>Angle between the nose and a world position (e.g. the star, for "wrong way" nags).</summary>
        public float AngleTo(Vector3 worldPos) => Vector3.Angle(ship.forward, worldPos - ship.position);
    }
}
