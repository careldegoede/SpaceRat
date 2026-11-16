using System.Collections;
using SpaceRat.Dialogue;
using SpaceRat.Flight;
using SpaceRat.Scene01;
using SpaceRat.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SpaceRat.Scene02
{
    public enum Scene02State
    {
        Recovery,
        Scan,
        Travel,
        Malfunction,
        Crash,
        Complete
    }

    /// <summary>
    /// Scene 2 "The Signal": recovery banter → passive scan finds a signal → playable ~2.5 min flight to
    /// the planet (tutorial, banter, off-course nags, asteroid bumps, a signal glitch) → scripted
    /// malfunction near the planet → atmospheric entry → exterior fall → cut to black.
    /// No fail state. Dev keys (focused window): Space skip line, R restart, J jump near the planet.
    /// </summary>
    public class Scene02Director : MonoBehaviour
    {
        [Header("Dialogue")]
        [SerializeField] DialogueBox subtitles;
        [SerializeField] DialogueSequence recovery;
        [SerializeField] DialogueSequence scan;
        [SerializeField] DialogueSequence tutorial;
        [SerializeField] DialogueSequence flightMemory;
        [SerializeField] DialogueSequence flightMusic;
        [SerializeField] DialogueSequence flightSignal;
        [SerializeField] DialogueSequence nags;
        [SerializeField] DialogueSequence nagSun;
        [SerializeField] DialogueSequence asteroidBump;
        [SerializeField] DialogueSequence malfunction;
        [SerializeField] DialogueSequence crash;

        [Header("Ship & world")]
        [SerializeField] CruiseFlightController flight;
        [SerializeField] SignalTracker tracker;
        [SerializeField] AsteroidField asteroids;
        [SerializeField] ShipEmergencyEffects effects;
        [SerializeField] Transform sun;
        [SerializeField] GameObject exteriorModel;

        [Header("Cameras")]
        [SerializeField] CinemachineBrain brain;
        [SerializeField] CinemachineCamera shotPov;
        [SerializeField] CinemachineCamera shotCrashExterior;

        [Header("Presentation")]
        [SerializeField] CockpitOverlay overlay;
        [SerializeField] Scene02Hud hud;
        [Tooltip("Orange/bright 'atmospheric entry' look; weight driven during the crash.")]
        [SerializeField] Volume entryVolume;

        [Header("Tuning")]
        [SerializeField] float malfunctionDistance = 2200f;
        [SerializeField] float offCourseAngle = 35f;
        [SerializeField] float offCourseSeconds = 4f;
        [SerializeField] float nagCooldown = 14f;
        [SerializeField] float pauseBetweenLines = 0.35f;
        [Tooltip("During the malfunction the ship hovers at this distance from the surface until the exterior fall.")]
        [SerializeField] float holdOffDistance = 900f;

        Scene02State _state;
        public Scene02State State
        {
            get => _state;
            private set { _state = value; Debug.Log($"[Scene02] -> {value} at t={Time.time:F1}"); }
        }

        bool _skip;
        bool _talking;          // a background line sequence is playing
        int _lineToken;
        int _nagIndex;
        float _nextNag;
        float _offCourseFor;
        bool _jumpRequested;
        float _autoSpeed;
        float _autoTurn;
        bool _freeFall;      // exterior dive: no hold-off

        void Start()
        {
            if (entryVolume) entryVolume.weight = 0f;
            flight.ControlsEnabled = false;
            if (asteroids) asteroids.Bumped += OnAsteroidBump;
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            if (asteroids) asteroids.Bumped -= OnAsteroidBump;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && Application.isFocused)
            {
                if (kb.spaceKey.wasPressedThisFrame) _skip = true;
                if (kb.rKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                if (kb.jKey.wasPressedThisFrame && State == Scene02State.Travel) _jumpRequested = true;
            }

            if ((State == Scene02State.Malfunction || State == Scene02State.Crash) && flight.enabled)
            {
                // Drag the ship toward the planet, but keep it off the surface until the exterior shot.
                float dist = tracker.DistanceToSurface;
                float spd = _freeFall ? _autoSpeed : Mathf.Lerp(8f, _autoSpeed, Mathf.Clamp01((dist - holdOffDistance) / 500f));
                flight.SetAutopilot(tracker.Target.position, _autoTurn, spd);
            }

            if (State == Scene02State.Travel || State == Scene02State.Malfunction)
            {
                hud.SetReadouts(tracker.SignalStrength, tracker.DistanceToSurface, flight.Speed, tracker.Distorted);
                hud.SetBoost(flight.BoostEnergy, flight.Boosting);
                hud.SetRadarText(tracker.Distorted ? "??.? km" : $"{tracker.DistanceToSurface / 1000f:0.0} km");
            }
        }

        IEnumerator Run()
        {
            // ---- 1. Breathing room -----------------------------------------------
            State = Scene02State.Recovery;
            Show(shotPov);
            overlay.ShowCockpit(true);
            overlay.SetFlash(Color.black, 1f);
            effects.AlarmMuted = true;          // no beeping until flight control is lost
            effects.SetImmediate(0.45f);
            effects.SetTarget(0.12f);           // warning lights wind down
            flight.SetSpeed(5f);                // barely drifting away from the star
            yield return Fade(Color.black, 1f, 0f, 2.5f);
            yield return PlayLines(recovery);

            // ---- 2. Search for help ----------------------------------------------
            State = Scene02State.Scan;
            yield return PlayLine(scan, 0);      // "Scan the area..."
            yield return PlayLine(scan, 1);      // "Initiating passive scan..."
            hud.SetScanning(true);
            yield return PlayLine(scan, 2);      // "Join the club."
            yield return Wait(2.5f);
            tracker.Distorted = true;
            yield return Wait(0.8f);
            tracker.Distorted = false;
            hud.SetScanning(false);
            hud.SetTarget(tracker.Target);       // contact appears
            yield return PlayLines(scan, 3, int.MaxValue);

            // ---- 3. Playable flight ----------------------------------------------
            State = Scene02State.Travel;
            hud.ShowFlight(true);
            flight.SetSpeed(40f);
            flight.ControlsEnabled = true;
            overlay.SetPrompt("FIND THE SIGNAL — CHECK YOUR RADAR\n<size=24>W/S PITCH · A/D YAW · Q/E ROLL · ↑/↓ THROTTLE · SHIFT BOOST</size>", 4f);
            _nextNag = Time.time + 15f;           // give the pilot time to find it before nagging
            yield return Travel();

            // ---- 4. Malfunction --------------------------------------------------
            State = Scene02State.Malfunction;
            StopBackgroundLines();
            flight.ControlsEnabled = false;
            flight.BoostAvailable = false;
            _autoTurn = 9f; _autoSpeed = 75f;
            effects.AlarmMuted = false;         // the alarm starts as the controls go
            effects.SetTarget(1f);
            hud.SetStatus("WARNING: POWER FLUCTUATION\nWARP DRIVE: OFFLINE");
            overlay.SetPrompt("FLIGHT CONTROL LOST", 3f, 5f);
            tracker.Distorted = true;
            StartCoroutine(FlickerHud(6f));
            yield return PlayLinesUnskippable(malfunction, 0, 8);   // up to "This is a terrible time to be precise!"
            tracker.Distorted = false;

            // ---- 5. Crash --------------------------------------------------------
            State = Scene02State.Crash;
            overlay.SetPrompt(null);
            _autoTurn = 20f; _autoSpeed = 140f;
            StartCoroutine(EntryGlow(0f, 1f, 3f));
            yield return PlayLinesUnskippable(malfunction, 8, int.MaxValue); // "Brace for impact." / "Bracing!"
            // Exterior: the ship falling toward the planet.
            overlay.ShowCockpit(false);
            hud.gameObject.SetActive(false);
            if (exteriorModel) exteriorModel.SetActive(true);
            Show(shotCrashExterior);
            if (entryVolume) entryVolume.weight = 0.6f;
            _freeFall = true;
            _autoSpeed = 260f;
            flight.SetSpeed(200f);              // dive starts at speed (no slow ramp from the hover)
            yield return WaitUntilOrTimeout(() => tracker.DistanceToSurface < 40f, 6f);

            // Impact: hard cut to black, near-silence.
            State = Scene02State.Complete;
            overlay.SetFlash(Color.black, 1f);
            effects.SetImmediate(0f);
            flight.SetSpeed(0f);
            flight.enabled = false;
            subtitles.Clear();
            yield return new WaitForSeconds(3f);
            yield return PlayLinesUnskippable(crash, 0, int.MaxValue);   // "Pilot? ... Please respond."
            // Scene 3 handoff goes here once it exists.
        }

        IEnumerator Travel()
        {
            float tutorialAt = Time.time + 7f;
            float promptOffAt = Time.time + 5f;
            bool tutorialDone = false, memoryDone = false, musicDone = false, signalDone = false, glitchDone = false;

            while (tracker.DistanceToSurface > malfunctionDistance)
            {
                if (_jumpRequested)
                {
                    _jumpRequested = false;
                    var t = flight.transform;
                    Vector3 dir = (t.position - tracker.Target.position).normalized;
                    t.position = tracker.Target.position + dir * (tracker.TargetRadius + malfunctionDistance + 600f);
                    t.rotation = Quaternion.LookRotation(-dir, Vector3.up);
                }

                if (promptOffAt > 0f && Time.time > promptOffAt) { overlay.SetPrompt(null); promptOffAt = -1f; }
                float p = tracker.Progress;
                if (!_talking)
                {
                    if (!tutorialDone && Time.time > tutorialAt) { tutorialDone = true; StartBackground(tutorial); }
                    else if (!memoryDone && p > 0.25f) { memoryDone = true; StartBackground(flightMemory); }
                    else if (!musicDone && p > 0.45f) { musicDone = true; StartBackground(flightMusic); }
                    else if (!signalDone && p > 0.7f) { signalDone = true; StartBackground(flightSignal); }
                }
                if (!glitchDone && p > 0.55f)
                {
                    glitchDone = true;
                    StartCoroutine(SignalGlitch());
                }

                // Off-course nags (rate limited, never over other lines).
                bool off = tracker.BearingAngle > offCourseAngle;
                _offCourseFor = off ? _offCourseFor + Time.deltaTime : 0f;
                if (_offCourseFor > offCourseSeconds && Time.time > _nextNag && !_talking)
                {
                    _nextNag = Time.time + nagCooldown;
                    _offCourseFor = 0f;
                    bool towardSun = sun && tracker.AngleTo(sun.position) < 30f;
                    if (towardSun && nagSun) StartBackground(nagSun);
                    else if (nags && nags.lines.Count > 0) StartCoroutine(PlayOneLine(nags.lines[_nagIndex++ % nags.lines.Count]));
                }
                yield return null;
            }
        }

        void OnAsteroidBump()
        {
            if (State != Scene02State.Travel) return;
            effects.SetImmediate(Mathf.Max(effects.Intensity, 0.6f));
            effects.SetTarget(0.12f);
            if (!_talking && asteroidBump) StartBackground(asteroidBump);
        }

        IEnumerator SignalGlitch()
        {
            tracker.Distorted = true;
            effects.SetImmediate(0.5f);
            effects.SetTarget(0.12f);
            yield return FlickerHud(1.6f);
            tracker.Distorted = false;
        }

        IEnumerator FlickerHud(float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.08f)
            {
                hud.gameObject.SetActive(Random.value > 0.25f);
                yield return new WaitForSeconds(0.08f);
            }
            hud.gameObject.SetActive(true);
        }

        IEnumerator EntryGlow(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float w = Mathf.Lerp(from, to, t / seconds);
                if (entryVolume) entryVolume.weight = w;
                overlay.SetFlash(new Color(1f, 0.45f, 0.1f), 0.18f * w * (0.7f + 0.3f * Mathf.Sin(Time.time * 30f)));
                yield return null;
            }
        }

        // ---- Dialogue -------------------------------------------------------------

        void StartBackground(DialogueSequence seq) => StartCoroutine(PlayLinesUnskippable(seq, 0, int.MaxValue));

        void StopBackgroundLines()
        {
            _lineToken++;
            _talking = false;
            subtitles.Clear();
        }

        IEnumerator PlayOneLine(DialogueLine line)
        {
            _talking = true;
            int token = _lineToken;
            subtitles.Show(line);
            yield return new WaitForSeconds(line.Seconds);
            if (token == _lineToken) { subtitles.Clear(); _talking = false; }
        }

        IEnumerator PlayLinesUnskippable(DialogueSequence seq, int from, int toExclusive)
        {
            if (!seq) yield break;
            _talking = true;
            int token = _lineToken;
            int end = Mathf.Min(toExclusive, seq.lines.Count);
            for (int i = from; i < end; i++)
            {
                if (token != _lineToken) yield break;
                subtitles.Show(seq.lines[i]);
                yield return new WaitForSeconds(seq.lines[i].Seconds);
                if (token != _lineToken) yield break;
                subtitles.Clear();
                yield return new WaitForSeconds(pauseBetweenLines);
            }
            if (token == _lineToken) _talking = false;
        }

        IEnumerator PlayLines(DialogueSequence seq, int from = 0, int toExclusive = int.MaxValue)
        {
            if (!seq) yield break;
            int end = Mathf.Min(toExclusive, seq.lines.Count);
            for (int i = from; i < end; i++) yield return PlayLine(seq, i);
        }

        IEnumerator PlayLine(DialogueSequence seq, int i)
        {
            if (!seq || i >= seq.lines.Count) yield break;
            subtitles.Show(seq.lines[i]);
            yield return Wait(seq.lines[i].Seconds);
            subtitles.Clear();
            yield return Wait(pauseBetweenLines);
        }

        IEnumerator Wait(float seconds)
        {
            _skip = false;
            for (float t = 0f; t < seconds && !_skip; t += Time.deltaTime) yield return null;
            _skip = false;
        }

        static IEnumerator WaitUntilOrTimeout(System.Func<bool> done, float timeout)
        {
            for (float t = 0f; t < timeout && !done(); t += Time.deltaTime) yield return null;
        }

        IEnumerator Fade(Color c, float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                overlay.SetFlash(c, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            overlay.SetFlash(c, to);
        }

        void Show(CinemachineCamera shot)
        {
            if (brain) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            shotPov.Priority = shot == shotPov ? 20 : 0;
            shotCrashExterior.Priority = shot == shotCrashExterior ? 20 : 0;
            if (exteriorModel) exteriorModel.SetActive(shot != shotPov);
        }
    }
}
