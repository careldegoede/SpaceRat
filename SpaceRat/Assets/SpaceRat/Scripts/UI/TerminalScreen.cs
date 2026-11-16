using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceRat.UI
{
    /// <summary>
    /// Sci-fi text on a world-space monitor canvas. Idles with a standby prompt and a blinking cursor;
    /// <see cref="Boot"/> runs a short boot sequence; <see cref="ShowLine"/> types a line in (typewriter)
    /// under a header, scrolling older lines up. A "status" screen just shows its fixed lines.
    /// </summary>
    public class TerminalScreen : MonoBehaviour
    {
        [SerializeField] Text header;
        [SerializeField] Text body;
        [SerializeField] Text footer;
        [SerializeField] string idleHeader = "KESSEL-9  //  STATION TERMINAL";
        [SerializeField] string idleBody = "> STANDBY";
        [SerializeField] float charsPerSecond = 55f;
        [SerializeField] int maxLines = 7;
        [Tooltip("Status screens show fixed lines with a slow flicker instead of typing.")]
        [SerializeField] bool statusScreen;
        [TextArea] [SerializeField] string statusText = "";

        readonly List<string> _lines = new();
        string _typing = "";
        float _typed;
        bool _active;

        void Start() => ShowIdle();

        public void ShowIdle()
        {
            _active = false;
            _lines.Clear();
            _typing = "";
            if (header) header.text = idleHeader;
            if (statusScreen && body) body.text = statusText;
        }

        /// <summary>Clears the screen and prints a quick boot sequence.</summary>
        public void Boot(string title)
        {
            _active = true;
            _lines.Clear();
            if (header) header.text = title;
            _lines.Add("> LINK ESTABLISHED  ·  SHIP AI");
            _lines.Add("> AUTH: OVERRIDE ACCEPTED");
            _typing = "";
            _typed = 0f;
        }

        /// <summary>Types a new line in (the previous one is committed and scrolls up).</summary>
        public void ShowLine(string title, string text)
        {
            _active = true;
            if (!string.IsNullOrEmpty(_typing)) _lines.Add(_typing);
            while (_lines.Count >= maxLines) _lines.RemoveAt(0);
            if (header && !string.IsNullOrEmpty(title)) header.text = title;
            _typing = "> " + (text ?? "").ToUpperInvariant();
            _typed = 0f;
        }

        void Update()
        {
            float t = Time.unscaledTime;
            bool cursorOn = Mathf.Repeat(t, 1f) < 0.55f;
            if (statusScreen)
            {
                if (footer) footer.text = "SYS " + (cursorOn ? "●" : "○") + "  " + System.DateTime.Now.ToString("HH:mm:ss");
                return;
            }
            if (!body) return;
            if (!_active)
            {
                body.text = idleBody + (cursorOn ? "_" : " ");
                if (footer) footer.text = "BEACON: TRANSMITTING  ·  ALL FREQUENCIES";
                return;
            }
            _typed = Mathf.Min(_typing.Length, _typed + charsPerSecond * Time.deltaTime);
            var sb = new StringBuilder();
            foreach (var l in _lines) sb.AppendLine(l);
            sb.Append(_typing, 0, (int)_typed);
            if ((int)_typed >= _typing.Length && cursorOn) sb.Append('_');
            body.text = sb.ToString();
            if (footer) footer.text = "LOG PLAYBACK  ·  " + (cursorOn ? "▮" : "▯") + "  REC 115-F";
        }
    }
}
