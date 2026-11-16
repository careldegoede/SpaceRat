using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceRat.Characters
{
    /// <summary>
    /// Finds the best <see cref="Interactable"/> in reach (close, roughly in view), shows its prompt
    /// centre-low on screen and uses it on E / gamepad X. Disabled while <see cref="FirstPersonWalker.InputEnabled"/> is off.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] FirstPersonWalker walker;
        [SerializeField] Camera view;
        [Tooltip("Minimum dot between the view direction and the direction to the target.")]
        [SerializeField] float lookDot = 0.55f;

        Interactable _current;
        GUIStyle _style;
        Texture2D _bg;

        public Interactable Current => _current;

        void Update()
        {
            _current = null;
            if (walker && !walker.InputEnabled) return;
            var eye = view ? view.transform : transform;
            float best = float.MaxValue;
            foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Exclude))
            {
                if (!it.Available) continue;
                Vector3 to = it.FocusPoint - eye.position;
                float d = to.magnitude;
                if (d > it.Range) continue;
                if (d > 0.6f && Vector3.Dot(eye.forward, to / d) < lookDot) continue;
                if (d < best) { best = d; _current = it; }
            }
            if (!_current) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame))
                _current.Use();
        }

        void OnGUI()
        {
            if (!_current || Event.current.type != EventType.Repaint) return;
            float s = Screen.height / 1080f;
            if (_style == null)
                _style = new GUIStyle { font = Resources.Load<Font>("Fonts/Orbitron"), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            if (!_bg) { _bg = new Texture2D(1, 1); _bg.SetPixel(0, 0, Color.black); _bg.Apply(); }
            _style.fontSize = Mathf.RoundToInt(26 * s);
            string text = $"[ E ]   {_current.Prompt}";
            var size = _style.CalcSize(new GUIContent(text));
            var box = new Rect((Screen.width - size.x) * 0.5f - 28 * s, Screen.height * 0.64f, size.x + 56 * s, size.y + 22 * s);
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(box, _bg);
            GUI.color = Color.white;
            _style.normal.textColor = new Color(0.45f, 0.9f, 1f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f));
            GUI.Label(box, text, _style);
            GUI.color = prev;
        }

        void OnDestroy()
        {
            if (_bg) Destroy(_bg);
        }
    }
}
