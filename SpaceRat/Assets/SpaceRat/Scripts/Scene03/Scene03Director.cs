using System.Collections;
using System.Globalization;
using SpaceRat.Characters;
using SpaceRat.Dialogue;
using SpaceRat.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SpaceRat.Scene03
{
    /// <summary>
    /// Scene 3 flow:
    /// Cockpit (wrecked, sparks; holo-capsule box) — computer wakes the slumped pilot, summarises the
    /// "landing", the remote station it detected and the cracked fusion cell → fade out →
    /// behind the ship (comset box, compass with the station blip) → Exit lines → Walk (banter by
    /// progress, off-course nags, live distance/signal) → station in range → fade out → main menu.
    /// Space skips lines during the cockpit opening only.
    /// </summary>
    public class Scene03Director : MonoBehaviour
    {
        [SerializeField] DialogueBox dialogue;
        [SerializeField] FirstPersonWalker pilot;
        [SerializeField] Transform outpost;
        [SerializeField] Transform crashSite;

        [Header("Cockpit opening")]
        [SerializeField] GameObject cockpitSet;
        [SerializeField] Camera cockpitCamera;
        [Tooltip("The pilot's eyes: used once he starts talking (no lip-sync, so we never watch him speak).")]
        [SerializeField] Camera cockpitPovCamera;
        [SerializeField] GameObject seatedPilotModel;
        [Tooltip("Scene 1's cockpit overlay (controls art, eyelids, flash) for the pilot's view.")]
        [SerializeField] CockpitOverlay overlay;
        [Tooltip("Cockpit walls etc. — only for the shot of the pilot in his chair; hidden in his own view.")]
        [SerializeField] GameObject cockpitShell;
        [Tooltip("Dark earth behind the overlay's windows — only in the pilot's view.")]
        [SerializeField] GameObject povBackdrop;
        [SerializeField] Camera mainCamera;
        [SerializeField] PilotSeating seatedPilot;
        [SerializeField] CompassHud compass;
        [SerializeField] float cockpitAmbient = 0.25f;

        [Header("Dialogue")]
        [SerializeField] DialogueSequence wakeCall;
        [SerializeField] DialogueSequence wake;
        [SerializeField] DialogueSequence exit;
        [SerializeField] DialogueSequence walkFusionCell;
        [SerializeField] DialogueSequence walkNavCore;
        [SerializeField] DialogueSequence walkSignal;
        [SerializeField] DialogueSequence nags;
        [SerializeField] DialogueSequence nagBackToShip;
        [SerializeField] DialogueSequence settlement;

        [Header("Tuning")]
        [SerializeField] float pauseBetweenLines = 0.35f;
        [SerializeField] float settlementRange = 330f;     // metres from the outpost
        [SerializeField] float offCourseAngle = 75f;       // degrees between movement and the outpost bearing
        [SerializeField] float offCourseSeconds = 6f;
        [SerializeField] float nagCooldown = 22f;
        [SerializeField] float signalRange = 1500f;        // comset signal hits 0 % this far from the ship
        [SerializeField] string endScene = "Scene00_MainMenu";

        float _black = 1f;            // full-screen black overlay (0..1)
        bool _skip, _talking, _walking, _ended;
        int _lineToken, _nagIndex;
        float _startDist, _offCourseFor, _nextNag;
        Vector3 _lastPos;
        Texture2D _blackTex;

        void Start()
        {
            if (pilot) pilot.InputEnabled = false;
            SetCockpit(true);
            StartCoroutine(Run());
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) _skip = true;
            if (!pilot || !outpost) return;

            var pos = pilot.transform.position;
            float dist = Flat(outpost.position - pos).magnitude;
            float fromShip = crashSite ? Flat(crashSite.position - pos).magnitude : 0f;
            if (dialogue)
            {
                int signal = Mathf.RoundToInt(Mathf.Clamp01(1f - fromShip / signalRange) * 70f + 25f);
                dialogue.comsetStatus = string.Format(CultureInfo.InvariantCulture, "DIST {0:0.0} KM  ·  SIGNAL {1}%", dist / 1000f, signal);
            }
            if (!_walking || _ended) { _lastPos = pos; return; }

            float progress = _startDist > 1f ? 1f - dist / _startDist : 0f;
            if (!_talking)
            {
                if (dist < settlementRange) { StartCoroutine(End()); return; }
                if (Play(walkFusionCell, progress > 0.12f)) { }
                else if (Play(walkNavCore, progress > 0.4f)) { }
                else Play(walkSignal, progress > 0.65f);
            }

            // Off course: moving, but not towards the outpost, for a while.
            Vector3 moved = Flat(pos - _lastPos);
            _lastPos = pos;
            if (moved.sqrMagnitude > 0.0004f)
            {
                float angle = Vector3.Angle(moved, Flat(outpost.position - pos));
                _offCourseFor = angle > offCourseAngle ? _offCourseFor + Time.deltaTime : 0f;
            }
            if (_offCourseFor > offCourseSeconds && Time.time > _nextNag && !_talking)
            {
                _offCourseFor = 0f;
                _nextNag = Time.time + nagCooldown;
                bool backToShip = crashSite && Vector3.Angle(moved, Flat(crashSite.position - pos)) < 40f;
                if (backToShip && nagBackToShip && nagBackToShip.lines.Count > 0) StartCoroutine(PlayOne(nagBackToShip.lines[0]));
                else if (nags && nags.lines.Count > 0) StartCoroutine(PlayOne(nags.lines[_nagIndex++ % nags.lines.Count]));
            }
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        readonly System.Collections.Generic.HashSet<DialogueSequence> _played = new();

        /// <summary>Starts a one-shot background sequence when its condition is met. True if it started.</summary>
        bool Play(DialogueSequence seq, bool condition)
        {
            if (!seq || !condition || _played.Contains(seq)) return false;
            _played.Add(seq);
            StartCoroutine(PlayBackground(seq));
            return true;
        }

        IEnumerator Run()
        {
            _lastPos = pilot ? pilot.transform.position : Vector3.zero;
            if (outpost && pilot) _startDist = Flat(outpost.position - pilot.transform.position).magnitude;

            // Cockpit: fade in on the slumped pilot while the computer tries to wake him.
            yield return new WaitForSeconds(0.8f);
            StartCoroutine(Fade(0f, 3f));
            yield return new WaitForSeconds(1.2f);
            yield return PlaySkippable(wakeCall);
            if (seatedPilot) seatedPilot.Slump = 0f;            // he comes round and sits up
            yield return Wait(1.4f);

            // Just before he speaks: into his eyes, exactly like Scene 1 — cockpit controls overlay,
            // eyelids blinking open (no lip-sync, so we never watch him talk). The canopy is buried: dark.
            yield return Fade(1f, 0.25f);
            SetCockpitPov(true);
            if (overlay) overlay.SetEyelid(1f);
            _black = 0f;
            yield return Blink();
            yield return PlaySkippable(wake);

            // Outside: fade out, swap to the pilot behind the ship, fade in with the compass up.
            yield return Fade(1f, 1.6f);
            SetCockpit(false);
            yield return new WaitForSeconds(0.8f);
            if (compass) compass.Visible = true;
            yield return Fade(0f, 2.5f);
            if (pilot) pilot.InputEnabled = true;
            _walking = true;
            yield return PlayBackground(exit);
        }

        IEnumerator End()
        {
            _ended = true;
            StopBackground();
            // The signal reveal is story-critical: if the player outran its trigger, play it now.
            if (walkSignal && !_played.Contains(walkSignal))
            {
                _played.Add(walkSignal);
                yield return PlayBackground(walkSignal);
            }
            yield return PlayBackground(settlement);
            yield return new WaitForSeconds(1f);
            if (pilot) pilot.InputEnabled = false;
            if (compass) compass.Visible = false;
            yield return Fade(1f, 2.5f);
            yield return new WaitForSeconds(1.5f);
            if (Application.CanStreamedLevelBeLoaded(endScene)) SceneManager.LoadScene(endScene);
        }

        float _ambient;
        Light _sun;

        /// <summary>Cockpit shot on/off: cameras, set, box style, and a dim, sunless interior.</summary>
        void SetCockpit(bool on)
        {
            if (cockpitSet) cockpitSet.SetActive(on);
            if (cockpitCamera) cockpitCamera.enabled = on;
            if (cockpitPovCamera) cockpitPovCamera.enabled = false;
            if (!on) SetCockpitPov(false);
            if (mainCamera) mainCamera.enabled = !on;
            if (dialogue) dialogue.Style = on ? DialogueBoxStyle.HoloCapsule : DialogueBoxStyle.Comset;
            if (on)
            {
                _ambient = RenderSettings.ambientIntensity;
                _sun = RenderSettings.sun;
                RenderSettings.ambientIntensity = cockpitAmbient;
                if (_sun) _sun.enabled = false;
            }
            else if (_sun || _ambient > 0f)
            {
                RenderSettings.ambientIntensity = _ambient;
                if (_sun) _sun.enabled = true;
            }
        }

        bool _pov;

        void SetCockpitPov(bool pov)
        {
            _pov = pov;
            if (cockpitPovCamera) cockpitPovCamera.enabled = pov;
            if (cockpitCamera) cockpitCamera.enabled = !pov;
            if (seatedPilotModel) seatedPilotModel.SetActive(!pov);
            if (cockpitShell) cockpitShell.SetActive(!pov);
            if (povBackdrop) povBackdrop.SetActive(pov);
            if (overlay)
            {
                overlay.ShowCockpit(pov);
                if (!pov) { overlay.SetEyelid(0f); overlay.SetFlash(Color.red, 0f); }
            }
        }

        /// <summary>Scene 1's wake blink: two half-opens, then fully open (1 = eyes closed).</summary>
        IEnumerator Blink()
        {
            yield return FadeEyes(1f, 0.55f, 0.6f);
            yield return FadeEyes(0.55f, 1f, 0.25f);
            yield return new WaitForSeconds(0.4f);
            yield return FadeEyes(1f, 0.3f, 0.8f);
            yield return FadeEyes(0.3f, 0.8f, 0.3f);
            yield return FadeEyes(0.8f, 0f, 1.6f);
        }

        IEnumerator FadeEyes(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (overlay) overlay.SetEyelid(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)) * 0.97f);
                yield return null;
            }
            if (overlay) overlay.SetEyelid(to * 0.97f);
        }

        void LateUpdate()
        {
            // Emergency red pulse over the pilot's view (the cockpit's red light).
            if (_pov && overlay) overlay.SetFlash(new Color(1f, 0.05f, 0.03f), 0.03f + 0.06f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3.2f)));
        }

        // ---- Dialogue ---------------------------------------------------------------------

        void StopBackground()
        {
            _lineToken++;
            _talking = false;
            if (dialogue) dialogue.Clear();
        }

        IEnumerator PlaySkippable(DialogueSequence seq)
        {
            if (!seq) yield break;
            foreach (var line in seq.lines)
            {
                dialogue.Show(line);
                yield return Wait(line.Seconds);
                dialogue.Clear();
                yield return Wait(pauseBetweenLines);
            }
        }

        IEnumerator PlayBackground(DialogueSequence seq)
        {
            if (!seq) yield break;
            _talking = true;
            int token = _lineToken;
            foreach (var line in seq.lines)
            {
                if (token != _lineToken) yield break;
                dialogue.Show(line);
                yield return new WaitForSeconds(line.Seconds);
                if (token != _lineToken) yield break;
                dialogue.Clear();
                yield return new WaitForSeconds(pauseBetweenLines);
            }
            if (token == _lineToken) _talking = false;
        }

        IEnumerator PlayOne(DialogueLine line)
        {
            _talking = true;
            int token = _lineToken;
            dialogue.Show(line);
            yield return new WaitForSeconds(line.Seconds);
            if (token == _lineToken) { dialogue.Clear(); _talking = false; }
        }

        IEnumerator Wait(float seconds)
        {
            _skip = false;
            for (float t = 0f; t < seconds && !_skip; t += Time.deltaTime) yield return null;
            _skip = false;
        }

        // ---- Black overlay -------------------------------------------------------------------

        IEnumerator Fade(float to, float seconds)
        {
            float from = _black;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                _black = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            _black = to;
        }

        void OnGUI()
        {
            if (_black <= 0.001f || Event.current.type != EventType.Repaint) return;
            if (!_blackTex) { _blackTex = new Texture2D(1, 1); _blackTex.SetPixel(0, 0, Color.black); _blackTex.Apply(); }
            GUI.depth = 10;    // behind the dialogue box (lower depth draws on top)
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _black);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _blackTex);
            GUI.color = prev;
        }

        void OnDestroy()
        {
            if (_blackTex) Destroy(_blackTex);
        }
    }
}
