using System.Collections;
using SpaceRat.Dialogue;
using SpaceRat.Flight;
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
    /// Drives Scene 1 as an explicit state sequence: camera shots, auto-timed
    /// dialogue, emergency effects and the wake-up blur.
    /// Dev keys: Space = skip current line, R = restart scene.
    /// </summary>
    public class Scene01Director : MonoBehaviour
    {
        [Header("Dialogue")]
        [SerializeField] DialogueSequence beat1Exterior;
        [SerializeField] DialogueSequence beat2Wake;
        [SerializeField] DialogueSequence beat3Amnesia;
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
        [SerializeField] ShipEmergencyEffects effects;
        [Tooltip("Hidden during the pilot's first-person shots.")]
        [SerializeField] GameObject pilotModel;
        [SerializeField] float cockpitRollRate = 8f;

        [Header("Post FX")]
        [Tooltip("Volume holding the blurred/dark 'eyes closed' look. Weight 1 = eyes closed.")]
        [SerializeField] Volume wakeVolume;

        [Header("Timing")]
        [SerializeField] float pauseBetweenLines = 0.4f;

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
            if (kb.rKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

            // Pilot POV, eyes closed -> blinking open.
            Show(shotPilotPov, 0f);
            if (pilotModel) pilotModel.SetActive(false);
            if (shipMotion) shipMotion.SetRollRate(cockpitRollRate);
            if (wakeVolume) wakeVolume.weight = 1f;
            subtitles.Clear();
            yield return Wait(1.2f);
            yield return Blink();

            // ---- Beat 3: amnesia dialogue in the cockpit ------------------------
            State = Scene01State.Dialogue;
            effects.SetTarget(0.9f);
            yield return PlayLines(beat3Amnesia, 0, int.MaxValue);

            // ---- Beat 4+: manual recovery (milestone 5) -------------------------
            State = Scene01State.ManualRecovery;
            subtitles.ShowSystem("[ TAKE MANUAL CONTROL — not implemented yet. Press R to replay. ]");
        }

        IEnumerator Blink()
        {
            // Two half-opens then fully open: weight is 1 when eyes are closed.
            yield return FadeWake(1f, 0.55f, 0.6f);
            yield return FadeWake(0.55f, 1f, 0.25f);
            yield return Wait(0.5f);
            yield return FadeWake(1f, 0.3f, 0.8f);
            yield return FadeWake(0.3f, 0.8f, 0.3f);
            yield return FadeWake(0.8f, 0f, 2.2f);
        }

        IEnumerator FadeWake(float from, float to, float seconds)
        {
            if (!wakeVolume) yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                wakeVolume.weight = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            wakeVolume.weight = to;
        }

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
        }
    }
}
