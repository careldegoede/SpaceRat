using System.Collections;
using System.Globalization;
using SpaceRat.Characters;
using SpaceRat.Dialogue;
using SpaceRat.UI;
using UnityEngine;

namespace SpaceRat.Scene04
{
    /// <summary>
    /// Scene 4 — The Search. Continues Scene 3 at the outpost: the pilot stands at the foot of the roof staircase
    /// (where the service bot was found) while the bot, carrying the navigation module, lifts off and flies to the
    /// ship. The goal is to find Captain Voss. The outpost's doors stay open to the pilot, so he can go in and out.
    /// Prototype scaffold: opening beat only; the search itself comes next.
    /// </summary>
    public class Scene04Director : MonoBehaviour
    {
        [SerializeField] DialogueBox dialogue;
        [SerializeField] FirstPersonWalker pilot;
        [SerializeField] CompassHud compass;
        [SerializeField] PlayerHud hud;
        [SerializeField] Transform crashSite;
        [Tooltip("Hovering service unit (parented nav module included): flies to the ship at the start.")]
        [SerializeField] ServiceBot bot;
        [SerializeField] SpaceRat.Environment.DoorOpener doors;
        [SerializeField] DialogueSequence intro;
        [SerializeField] DialogueSequence afterLiftoff;
        [SerializeField] float botClimb = 2.6f;
        [SerializeField] float botMaxSpeed = 36f;
        [SerializeField] float botFlightSeconds = 70f;

        float _black = 1f;
        Texture2D _blackTex;

        void Start()
        {
            if (pilot) pilot.InputEnabled = false;
            if (doors) doors.Locked = false;
            if (dialogue) dialogue.Style = DialogueBoxStyle.Comset;
            StartCoroutine(Run());
        }

        void Update()
        {
            if (!pilot || !dialogue || !crashSite) return;
            Vector3 d = crashSite.position - pilot.transform.position; d.y = 0f;
            dialogue.comsetStatus = string.Format(CultureInfo.InvariantCulture, "SHIP {0:0.0} KM  ·  SIGNAL 80%", d.magnitude / 1000f);
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(0.8f);
            StartCoroutine(Fade(0f, 2.5f));
            if (hud) hud.Visible = true;
            if (pilot) pilot.InputEnabled = true;     // free to move from the first frame; dialogue is background
            if (bot) StartCoroutine(BotFlight());
            yield return new WaitForSeconds(1.5f);
            yield return Play(intro);
            yield return new WaitForSeconds(4f);
            yield return Play(afterLiftoff);
        }

        /// <summary>The bot rises off the roof and flies to the ship, then switches off.</summary>
        IEnumerator BotFlight()
        {
            bot.Activate();
            bot.Following = false;
            yield return new WaitForSeconds(1.5f);
            Vector3 start = bot.transform.position;
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 2.2f);
                bot.transform.position = start + Vector3.up * (k * botClimb + Mathf.Sin(t * 5f) * 0.06f);
                bot.transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
                yield return null;
            }
            float speed = 2f;
            for (float t = 0f; t < botFlightSeconds; t += Time.deltaTime)
            {
                Vector3 to = crashSite ? crashSite.position - bot.transform.position : bot.transform.forward;
                Vector3 flat = new(to.x, 0f, to.z);
                if (crashSite && flat.magnitude < 40f) break;
                Vector3 dir = flat.sqrMagnitude > 0.01f ? flat.normalized : bot.transform.forward;
                speed = Mathf.Min(speed + 9f * Time.deltaTime, botMaxSpeed);
                bot.transform.position += (dir * speed + Vector3.up * Mathf.Lerp(2f, 0.2f, t / 9f)) * Time.deltaTime;
                bot.transform.rotation = Quaternion.RotateTowards(bot.transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 180f * Time.deltaTime);
                yield return null;
            }
            bot.gameObject.SetActive(false);
        }

        IEnumerator Play(DialogueSequence seq)
        {
            if (!seq || !dialogue) yield break;
            foreach (var line in seq.lines)
            {
                dialogue.Show(line);
                yield return new WaitForSeconds(line.Seconds);
                dialogue.Clear();
                yield return new WaitForSeconds(0.35f);
            }
        }

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
            if (Event.current.type != EventType.Repaint || _black <= 0.001f) return;
            if (!_blackTex) { _blackTex = new Texture2D(1, 1); _blackTex.SetPixel(0, 0, Color.black); _blackTex.Apply(); }
            GUI.depth = 10;
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _black);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _blackTex);
            GUI.color = prev;
        }

        void OnDestroy() { if (_blackTex) Destroy(_blackTex); }
    }
}
