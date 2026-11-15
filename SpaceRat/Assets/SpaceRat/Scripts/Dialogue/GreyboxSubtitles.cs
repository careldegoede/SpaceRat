using UnityEngine;

namespace SpaceRat.Dialogue
{
    /// <summary>
    /// Greybox stand-in for the real dialogue panel (milestone 4).
    /// Draws the current speaker + line bottom-center with IMGUI.
    /// </summary>
    public class GreyboxSubtitles : MonoBehaviour
    {
        [SerializeField] Color computerColor = new(0.45f, 0.9f, 1f);
        [SerializeField] Color pilotColor = new(1f, 0.85f, 0.6f);

        string _speaker;
        string _text;
        Color _color;
        GUIStyle _nameStyle, _textStyle;
        Texture2D _bg;

        public void Show(Speaker speaker, string text)
        {
            _speaker = speaker == Speaker.Computer ? "COMPUTER" : "PILOT";
            _color = speaker == Speaker.Computer ? computerColor : pilotColor;
            _text = text;
        }

        public void ShowSystem(string text)
        {
            _speaker = "";
            _color = Color.white;
            _text = text;
        }

        public void Clear() => _text = null;

        void OnDestroy()
        {
            if (_bg) Destroy(_bg);
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(_text)) return;

            if (_nameStyle == null)
            {
                _bg = new Texture2D(1, 1);
                _bg.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.09f, 0.78f));
                _bg.Apply();
                _nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                _textStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }

            float scale = Screen.height / 1080f;
            _nameStyle.fontSize = Mathf.RoundToInt(26 * scale);
            _textStyle.fontSize = Mathf.RoundToInt(32 * scale);

            float w = Mathf.Min(Screen.width - 32f, 1400f * scale);
            float h = 170f * scale;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 40f * scale, w, h);
            GUI.DrawTexture(rect, _bg);

            float pad = 24f * scale;
            _nameStyle.normal.textColor = _color;
            _textStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(rect.x + pad, rect.y + pad * 0.6f, w - pad * 2, 40f * scale), _speaker, _nameStyle);
            GUI.Label(new Rect(rect.x + pad, rect.y + pad * 0.6f + 42f * scale, w - pad * 2, h - 60f * scale), _text, _textStyle);
        }
    }
}
