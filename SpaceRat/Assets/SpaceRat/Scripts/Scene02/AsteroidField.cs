using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceRat.Scene02
{
    /// <summary>
    /// Scatters asteroid prefabs along the route (deterministic seed) as scenery, and reports bumps:
    /// flying into one nudges the ship and raises <see cref="Bumped"/> — no damage, no physics.
    /// </summary>
    public class AsteroidField : MonoBehaviour
    {
        [SerializeField] List<GameObject> prefabs = new();
        [SerializeField] Transform ship;
        [SerializeField] Vector3 routeStart;
        [SerializeField] Vector3 routeEnd;
        [SerializeField] int count = 45;
        [SerializeField] Vector2 sizeRange = new(10f, 45f);
        [Tooltip("Lateral spread around the route line (min, max).")]
        [SerializeField] Vector2 spread = new(40f, 650f);
        [Tooltip("Fraction of the route (start, end) that has asteroids.")]
        [SerializeField] Vector2 routeSpan = new(0.15f, 0.8f);
        [SerializeField] int seed = 2026;
        [SerializeField] float tumbleDegPerSec = 8f;

        readonly List<(Transform t, float radius, Vector3 spin)> _rocks = new();
        float _cooldown;

        /// <summary>Raised when the ship touches an asteroid (rate-limited).</summary>
        public event Action Bumped;

        void Start()
        {
            if (prefabs.Count == 0) return;
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            Vector3 dir = (routeEnd - routeStart).normalized;
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, dir).normalized;

            for (int i = 0; i < count; i++)
            {
                var prefab = prefabs[rng.Next(prefabs.Count)];
                var go = Instantiate(prefab, transform);
                float along = R(routeSpan.x, routeSpan.y);
                float ang = R(0f, Mathf.PI * 2f);
                float off = R(spread.x, spread.y);
                go.transform.position = Vector3.Lerp(routeStart, routeEnd, along) + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * off;
                go.transform.rotation = Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360));

                // Normalise the (tiny) prefab mesh to the wanted size.
                var mf = go.GetComponentInChildren<MeshFilter>();
                float meshSize = mf && mf.sharedMesh ? mf.sharedMesh.bounds.size.magnitude : 1f;
                float size = R(sizeRange.x, sizeRange.y);
                go.transform.localScale = Vector3.one * (size / Mathf.Max(0.0001f, meshSize));
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);

                _rocks.Add((go.transform, size * 0.45f, new Vector3(R(-1, 1), R(-1, 1), R(-1, 1)) * tumbleDegPerSec));
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _cooldown -= dt;
            foreach (var (t, radius, spin) in _rocks)
            {
                t.Rotate(spin * dt, Space.Self);
                if (!ship || _cooldown > 0f) continue;
                Vector3 d = ship.position - t.position;
                if (d.sqrMagnitude < (radius + 6f) * (radius + 6f))
                {
                    _cooldown = 3f;
                    if (ship.TryGetComponent<Flight.CruiseFlightController>(out var flight))
                        flight.Bump(d.normalized * 25f);
                    Bumped?.Invoke();
                }
            }
        }
    }
}
