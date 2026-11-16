using System.Globalization;
using UnityEngine;

namespace SpaceRat.UI
{
    /// <summary>
    /// Top-right compass (Images/Futuristic Cyan-Orange Sci-Fi Compass HUD.png, split into a dial and a
    /// side panel). The dial turns with the viewer's heading (N/E/S/W stay true to the world), an orange
    /// blip marks the target's bearing, and the fixed side panel shows the target's name and distance.
    /// </summary>
    public class CompassHud : MonoBehaviour
    {
        [SerializeField] Texture2D dial;
        [SerializeField] Texture2D panel;
        [SerializeField] Transform viewer;
        [SerializeField] Transform target;
        [SerializeField] string targetLabel = "STATION";
        [Tooltip("Dial diameter at 1080p.")]
        [SerializeField] float dialSize = 230f;
        [SerializeField] Color blipColor = new(1f, 0.55f, 0.12f);

        public bool Visible { get; set; }
        public Transform Target { get => target; set => target = value; }
        public string TargetLabel { get => targetLabel; set => targetLabel = value; }

        float _alpha;
        GUIStyle _label, _value;
        Texture2D _dot, _ring;

        void Update() =>
            _alpha = Mathf.MoveTowards(_alpha, Visible ? 1f : 0f, Time.unscaledDeltaTime / 0.6f);

        void OnDestroy()
        {
            if (_dot) Destroy(_dot);
            if (_ring) Destroy(_ring);
        }

        /// <summary>Editor preview: show fully, without waiting for the fade.</summary>
        public void ShowImmediately() { Visible = true; _alpha = 1f; }

        void OnGUI() => Draw(Screen.width, Screen.height);

        /// <summary>Draws onto a canvas of the given size (the screen in game; an offscreen preview in the editor).</summary>
        public void Draw(float screenWidth, float screenHeight)
        {
            if (_alpha <= 0.001f || Event.current.type != EventType.Repaint || !dial || !viewer) return;
            EnsureStyles();
            float s = screenHeight / 1080f;
            float d = dialSize * s;
            float ph = d * 0.5f, pw = ph * 370f / 318f;
            float margin = 34f * s;
            var center = new Vector2(screenWidth - margin - pw - d * 0.42f, margin + d * 0.5f);

            var prevColor = GUI.color;
            var prevMatrix = GUI.matrix;
            GUI.color = new Color(1f, 1f, 1f, _alpha);

            // Side panel (fixed), tucked against the dial's right edge.
            var panelRect = new Rect(center.x + d * 0.42f, center.y - ph * 0.5f, pw, ph);
            if (panel) GUI.DrawTexture(panelRect, panel);

            // Dial turns with heading: facing east puts N on the left.
            float yaw = viewer.eulerAngles.y;
            GUIUtility.RotateAroundPivot(-yaw, center);
            GUI.DrawTexture(new Rect(center.x - d * 0.5f, center.y - d * 0.5f, d, d), dial);
            GUI.matrix = prevMatrix;

            if (target)
            {
                Vector3 to = target.position - viewer.position;
                float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float rel = (bearing - yaw) * Mathf.Deg2Rad;
                var p = center + new Vector2(Mathf.Sin(rel), -Mathf.Cos(rel)) * d * 0.3f;

                float t = Time.unscaledTime;
                float ping = Mathf.Repeat(t / 1.6f, 1f);
                float rs = d * (0.1f + 0.22f * ping);
                GUI.color = new Color(blipColor.r, blipColor.g, blipColor.b, _alpha * (1f - ping) * 0.9f);
                GUI.DrawTexture(new Rect(p.x - rs * 0.5f, p.y - rs * 0.5f, rs, rs), RingTex());
                float bs = d * (0.11f + 0.015f * Mathf.Sin(t * 6f));
                GUI.color = new Color(blipColor.r, blipColor.g, blipColor.b, _alpha * 0.55f);
                GUI.DrawTexture(new Rect(p.x - bs, p.y - bs, bs * 2f, bs * 2f), DotTex());       // glow
                GUI.color = new Color(blipColor.r, blipColor.g, blipColor.b, _alpha);
                GUI.DrawTexture(new Rect(p.x - bs * 0.5f, p.y - bs * 0.5f, bs, bs), DotTex());
                GUI.color = new Color(1f, 0.92f, 0.75f, _alpha);
                GUI.DrawTexture(new Rect(p.x - bs * 0.22f, p.y - bs * 0.22f, bs * 0.44f, bs * 0.44f), DotTex());   // hot core

                float km = new Vector2(to.x, to.z).magnitude / 1000f;
                GUI.color = new Color(1f, 1f, 1f, _alpha);
                // Text inside the panel's dark window (art: x 8..86 %, y 22..66 %), shrunk to fit.
                var inner = new Rect(panelRect.x + pw * 0.11f, panelRect.y + ph * 0.23f, pw * 0.72f, ph * 0.43f);
                string dist = km >= 1f ? km.ToString("0.0", CultureInfo.InvariantCulture) + " KM"
                                       : Mathf.RoundToInt(km * 1000f) + " M";
                _label.fontSize = Fit(_label, targetLabel, inner.width, inner.height * 0.34f);
                _value.fontSize = Fit(_value, dist, inner.width, inner.height * 0.48f);
                // Stack the two lines from the top using each font's real line height (Orbitron sits tall).
                float lh = _label.CalcHeight(new GUIContent(targetLabel), inner.width);
                float vh = _value.CalcHeight(new GUIContent(dist), inner.width);
                float top = inner.y + Mathf.Max(0f, (inner.height - lh * 0.85f - vh) * 0.5f);
                GUI.Label(new Rect(inner.x, top, inner.width, lh), targetLabel, _label);
                GUI.Label(new Rect(inner.x, top + lh * 0.85f, inner.width, vh), dist, _value);
            }

            GUI.color = prevColor;
        }

        static int Fit(GUIStyle style, string text, float width, float height)
        {
            int size = Mathf.Max(8, Mathf.RoundToInt(height));
            style.fontSize = size;
            float w = style.CalcSize(new GUIContent(text)).x;
            if (w > width) size = Mathf.Max(8, Mathf.FloorToInt(size * width / w));
            return size;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            var orbitron = Resources.Load<Font>("Fonts/Orbitron");
            var rajdhani = Resources.Load<Font>("Fonts/Rajdhani-SemiBold");
            // Built from scratch (not GUI.skin.label) so editor and player skins can't add offsets.
            _label = new GUIStyle
            {
                font = rajdhani ? rajdhani : GUI.skin.font, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Overflow,
                normal = { textColor = new Color(0.45f, 0.9f, 1f) }
            };
            _value = new GUIStyle(_label)
            {
                font = orbitron ? orbitron : GUI.skin.font, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.72f, 0.35f) }
            };
        }

        Texture2D DotTex() => _dot ? _dot : _dot = Radial(64, r => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, r)));
        Texture2D RingTex() => _ring ? _ring : _ring = Radial(128, r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) / 0.1f));

        static Texture2D Radial(int size, System.Func<float, float> alpha)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude / (size * 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, r > 1f ? 0f : alpha(r));
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }
}
