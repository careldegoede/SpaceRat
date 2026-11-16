using System.Collections;
using SpaceRat.Characters;
using SpaceRat.Dialogue;
using SpaceRat.Flight;
using SpaceRat.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SpaceRat.Scene01
{
    public enum Scene01State
    {
        ExteriorEmergency,
        WakePilot,
        Dialogue,
        ManualRecovery,
        Escape,
        Failure
    }

    /// <summary>
    /// Drives Scene 1 as an explicit state sequence: camera shots, auto-timed dialogue,
    /// emergency effects, the wake-up blink into the cockpit view, the playable manual
    /// escape (W/S pitch, A/D yaw), and the escape or crash outcome.
    /// Dev keys (focused window only): Space = skip current line, R = restart scene.
    /// </summary>
    public class Scene01Director : MonoBehaviour
    {
        [Header("Dialogue")]
        [SerializeField] DialogueSequence beat1Exterior;
        [SerializeField] DialogueSequence beat2Wake;
        [SerializeField] DialogueSequence beat3Amnesia;
        [SerializeField] DialogueSequence beat4Takeover;
        [SerializeField] DialogueSequence beat4HeatCritical;
        [SerializeField] DialogueSequence beat5Escape;
        [SerializeField] DialogueSequence failureImpact;
        [SerializeField] GreyboxSubtitles subtitles;

        [Header("Cameras")]
        [SerializeField] CinemachineBrain brain;
        [SerializeField] CinemachineCamera shotWide;
        [SerializeField] CinemachineCamera shotSide;
        [SerializeField] CinemachineCamera shotClose;
        [SerializeField] CinemachineCamera shotPilot;
        [SerializeField] CinemachineCamera shotPilotPov;

        [Header("Ship")]
        [SerializeField] ShipCinematicMotion shipMotion;
        [SerializeField] ShipFlightController flight;
        [SerializeField] CollisionCourseEvaluator evaluator;
        [SerializeField] ShipEmergencyEffects effects;
        [Tooltip("Hidden during the pilot's first-person shots.")]
        [SerializeField] GameObject pilotModel;
        [Tooltip("Ship exterior mesh; hidden during interior shots so it doesn't block the cockpit cameras.")]
        [SerializeField] GameObject exteriorModel;
        [Tooltip("Cockpit room around the seat; only shown in the pilot interior shot.")]
        [SerializeField] GameObject interiorShell;
        [SerializeField] float cockpitRollRate = 8f;

        [Header("Presentation")]
        [SerializeField] CockpitOverlay overlay;
        [Tooltip("Volume holding the blurred/dark 'eyes closed' look. Weight 1 = eyes closed.")]
        [SerializeField] Volume wakeVolume;

        [Header("Flow")]
        [SerializeField] string NextScene = "Scene02_TheSignal";

        [Header("Timing")]
        [SerializeField] float pauseBetweenLines = 0.4f;
        [Tooltip("Heat level at which the 'heat critical' exchange plays.")]
        [Range(0f, 1f)] [SerializeField] float heatCriticalAt = 0.55f;

        Scene01State _state;
        public Scene01State State
        {
            get => _state;
            private set { _state = value; Debug.Log($"[Scene01] -> {value} at t={Time.time:F1}"); }
        }

        CinemachineCamera[] _shots;
        bool _skipRequested;

        void Start()
        {
            _shots = new[] { shotWide, shotSide, shotClose, shotPilot, shotPilotPov };
            if (wakeVolume) wakeVolume.weight = 0f;
            if (flight) flight.enabled = false;
            StartCoroutine(Run());
        }

        void Update()
        {
            // Dev keys only when the game window has focus (background input events can be spurious).
            var kb = Keyboard.current;
            if (kb == null || !Application.isFocused) return;
            if (kb.spaceKey.wasPressedThisFrame)
            {
                _skipRequested = true;
                Debug.Log($"[Scene01] Skip requested at t={Time.time:F1} ({State})");
            }
            if (kb.rKey.wasPressedThisFrame) Restart();
        }

        IEnumerator Run()
        {
            // ---- Beat 1: external emergency ------------------------------------
            State = Scene01State.ExteriorEmergency;
            effects.SetImmediate(0.25f);
            effects.SetTarget(0.6f);
            Show(shotWide, 0f);
            yield return Wait(1.5f);
            yield return PlayLines(beat1Exterior, 0, 2);
            Show(shotSide, 2.5f);
            yield return PlayLines(beat1Exterior, 2, int.MaxValue);

            // ---- Beat 2: wake-up attempts, cutting between exterior and cockpit -
            State = Scene01State.WakePilot;
            effects.SetTarget(0.8f);
            Show(shotClose, 0f);
            yield return PlayLine(beat2Wake, 0);
            Show(shotPilot, 0f);
            yield return PlayLine(beat2Wake, 1);
            Show(shotSide, 0f);
            yield return PlayLine(beat2Wake, 2);

            // Pilot POV, eyes closed -> blinking open into the cockpit.
            Show(shotPilotPov, 0f);
            if (pilotModel) pilotModel.SetActive(false);
            if (shipMotion) shipMotion.SetRollRate(cockpitRollRate);
            if (overlay) overlay.ShowCockpit(true);
            SetEyesClosed(1f);
            subtitles.Clear();
            yield return Wait(1.2f);
            yield return Blink();

            // The pilot is awake now: un-slump (visible again in the exterior escape shot).
            if (pilotModel && pilotModel.GetComponentInChildren<PilotSeating>() is { } seating) seating.Slump = 0f;
            else if (pilotModel && pilotModel.GetComponentInChildren<SeatedPose>() is { } seated) seated.Slump = 0f;

            // ---- Beat 3: amnesia dialogue in the cockpit ------------------------
            State = Scene01State.Dialogue;
            effects.SetTarget(0.9f);
            yield return PlayLines(beat3Amnesia, 0, int.MaxValue);

            // ---- Beat 4: manual recovery (playable) -----------------------------
            yield return ManualRecovery();
        }

        IEnumerator ManualRecovery()
        {
            State = Scene01State.ManualRecovery;
            if (overlay)
            {
                overlay.SetPrompt("TAKE MANUAL CONTROL\n<size=26>W / S  PITCH   ·   A / D  YAW</size>", 3f);
                overlay.ShowHud(true);
            }

            // Hand over: cinematic stops driving the root; body levels out but stays a bit unstable.
            shipMotion.DriveRoot = false;
            shipMotion.LevelOut();
            shipMotion.SetWobbleScale(0.25f);
            flight.enabled = true;
            evaluator.Begin();

            StartCoroutine(PlayLinesUnskippable(beat4Takeover));
            bool heatLinePlayed = false;
            float promptOffAt = Time.time + 4f;

            while (evaluator.Outcome == EscapeOutcome.InProgress)
            {
                flight.Heat = evaluator.Heat;
                effects.SetTarget(0.55f + 0.45f * evaluator.Heat);
                if (overlay)
                {
                    overlay.SetHeat(evaluator.Heat, evaluator.TimeRemaining);
                    if (Time.time > promptOffAt) overlay.SetPrompt(null);
                }
                if (!heatLinePlayed && evaluator.Heat >= heatCriticalAt)
                {
                    heatLinePlayed = true;
                    StartCoroutine(PlayLinesUnskippable(beat4HeatCritical));
                }
                yield return null;
            }

            flight.enabled = false;
            if (overlay) { overlay.ShowHud(false); overlay.SetPrompt(null); }

            if (evaluator.Outcome == EscapeOutcome.Escaped) yield return Escape();
            else yield return Failure();
        }

        IEnumerator Escape()
        {
            State = Scene01State.Escape;
            StopLineCoroutines();
            effects.SetTarget(0f);

            // Keep flying away under the cinematic drift (parked far from the sun, so it won't slow down).
            shipMotion.DriveRoot = false;
            var cruise = StartCoroutine(Cruise());

            yield return Wait(1.0f);
            if (overlay) overlay.ShowCockpit(false);
            Show(shotWide, 0f);
            if (pilotModel) pilotModel.SetActive(true);
            yield return PlayLines(beat5Escape, 0, int.MaxValue);

            // Fade to black, then title.
            yield return Fade(Color.black, 0f, 1f, 2f);
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                overlay.SetTitle(t / 1.5f);
                yield return null;
            }
            overlay.SetTitle(1f);
            StopCoroutine(cruise);

            // Hand off to Scene 2 (if it's in the build settings).
            yield return new WaitForSeconds(3f);
            if (Application.CanStreamedLevelBeLoaded(NextScene)) SceneManager.LoadScene(NextScene);
        }

        IEnumerator Cruise()
        {
            var t = flight.transform;
            while (true)
            {
                t.position += t.forward * (flight.Speed * Time.deltaTime);
                yield return null;
            }
        }

        IEnumerator Failure()
        {
            State = Scene01State.Failure;
            StopLineCoroutines();
            subtitles.Clear();
            yield return Fade(Color.white, 0f, 1f, 0.6f);
            yield return PlayLinesUnskippable(failureImpact);
            yield return new WaitForSeconds(0.6f);
            Restart();
        }

        static void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        IEnumerator Fade(Color color, float from, float to, float seconds)
        {
            if (!overlay) yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                overlay.SetFlash(color, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            overlay.SetFlash(color, to);
        }

        IEnumerator Blink()
        {
            // Two half-opens then fully open: 1 = eyes closed.
            yield return FadeEyes(1f, 0.55f, 0.6f);
            yield return FadeEyes(0.55f, 1f, 0.25f);
            yield return Wait(0.5f);
            yield return FadeEyes(1f, 0.3f, 0.8f);
            yield return FadeEyes(0.3f, 0.8f, 0.3f);
            yield return FadeEyes(0.8f, 0f, 2.2f);
        }

        IEnumerator FadeEyes(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetEyesClosed(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetEyesClosed(to);
        }

        void SetEyesClosed(float amount)
        {
            if (wakeVolume) wakeVolume.weight = amount;
            if (overlay) overlay.SetEyelid(amount * 0.92f);
        }

        // ---- Dialogue ------------------------------------------------------------

        int _lineToken;   // bumping this cancels background (unskippable) line coroutines

        void StopLineCoroutines() => _lineToken++;

        IEnumerator PlayLines(DialogueSequence seq, int from, int toExclusive)
        {
            if (!seq) yield break;
            int end = Mathf.Min(toExclusive, seq.lines.Count);
            for (int i = from; i < end; i++)
                yield return PlayLine(seq, i);
        }

        IEnumerator PlayLine(DialogueSequence seq, int index)
        {
            if (!seq || index < 0 || index >= seq.lines.Count) yield break;
            var line = seq.lines[index];
            subtitles.Show(line.speaker, line.text);
            yield return Wait(line.duration);
            subtitles.Clear();
            yield return Wait(pauseBetweenLines);
        }

        /// <summary>Lines that play alongside gameplay; Space doesn't skip them.</summary>
        IEnumerator PlayLinesUnskippable(DialogueSequence seq)
        {
            if (!seq) yield break;
            int token = _lineToken;
            foreach (var line in seq.lines)
            {
                if (token != _lineToken) yield break;
                subtitles.Show(line.speaker, line.text);
                yield return new WaitForSeconds(line.duration);
                if (token != _lineToken) yield break;
                subtitles.Clear();
                yield return new WaitForSeconds(pauseBetweenLines);
            }
        }

        /// <summary>Waits for the given time, or until Space is pressed.</summary>
        IEnumerator Wait(float seconds)
        {
            _skipRequested = false;
            for (float t = 0f; t < seconds && !_skipRequested; t += Time.deltaTime)
                yield return null;
            _skipRequested = false;
        }

        void Show(CinemachineCamera shot, float blendSeconds)
        {
            if (brain)
                brain.DefaultBlend = blendSeconds <= 0f
                    ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
                    : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendSeconds);

            foreach (var s in _shots)
                if (s) s.Priority = s == shot ? 20 : 0;

            if (exteriorModel)
                exteriorModel.SetActive(shot != shotPilot && shot != shotPilotPov);
            if (interiorShell)
                interiorShell.SetActive(shot == shotPilot);
        }
    }
}
