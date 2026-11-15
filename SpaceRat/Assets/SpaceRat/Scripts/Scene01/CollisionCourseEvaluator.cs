using UnityEngine;

namespace SpaceRat.Scene01
{
    public enum EscapeOutcome
    {
        InProgress,
        Escaped,
        Crashed,
        Overheated
    }

    /// <summary>
    /// Judges the manual escape against a designed corridor rather than real gravity:
    /// - Escaped: heading points away from the star (angle &gt; safeAngle) for holdSeconds.
    /// - Crashed: the ship enters the hazard radius.
    /// - Overheated: the time limit runs out first.
    /// Heat (0..1) rises as the ship gets closer and as time runs out.
    /// </summary>
    public class CollisionCourseEvaluator : MonoBehaviour
    {
        [SerializeField] Transform ship;
        [SerializeField] Transform sun;

        [Header("Corridor")]
        [Tooltip("Entering this distance from the sun centre = crash.")]
        [SerializeField] float hazardRadius = 450f;
        [Tooltip("Angle between ship heading and the sun direction that counts as 'heading away'.")]
        [SerializeField] float safeAngle = 100f;
        [SerializeField] float holdSeconds = 2f;
        [Tooltip("Seconds before the heat becomes unsurvivable.")]
        [SerializeField] float timeLimit = 25f;

        float _startDistance;
        float _elapsed;
        float _safeFor;
        bool _running;

        public EscapeOutcome Outcome { get; private set; }
        public float Heat { get; private set; }
        public float TimeRemaining => Mathf.Max(0f, timeLimit - _elapsed);
        public float TimeLimit => timeLimit;
        /// <summary>0..1 progress toward the safe heading (for HUD guidance).</summary>
        public float AwayProgress { get; private set; }

        public void Begin()
        {
            _startDistance = Mathf.Max(hazardRadius + 1f, Vector3.Distance(ship.position, sun.position));
            _elapsed = 0f;
            _safeFor = 0f;
            Outcome = EscapeOutcome.InProgress;
            _running = true;
        }

        public void Stop() => _running = false;

        void Update()
        {
            if (!_running || Outcome != EscapeOutcome.InProgress) return;
            _elapsed += Time.deltaTime;

            Vector3 toSun = sun.position - ship.position;
            float dist = toSun.magnitude;
            float angle = Vector3.Angle(ship.forward, toSun);
            AwayProgress = Mathf.Clamp01(angle / safeAngle);

            float proximity = 1f - Mathf.Clamp01((dist - hazardRadius) / (_startDistance - hazardRadius));
            float timeHeat = Mathf.Clamp01(_elapsed / timeLimit);
            Heat = Mathf.Clamp01(Mathf.Max(proximity, timeHeat * 0.9f));

            if (dist <= hazardRadius) { Finish(EscapeOutcome.Crashed); return; }

            _safeFor = angle >= safeAngle ? _safeFor + Time.deltaTime : 0f;
            if (_safeFor >= holdSeconds) { Finish(EscapeOutcome.Escaped); return; }

            if (_elapsed >= timeLimit) Finish(EscapeOutcome.Overheated);
        }

        void Finish(EscapeOutcome outcome)
        {
            Outcome = outcome;
            _running = false;
            Debug.Log($"[Scene01] Escape outcome: {outcome} after {_elapsed:F1}s (heat {Heat:F2})");
        }
    }
}
