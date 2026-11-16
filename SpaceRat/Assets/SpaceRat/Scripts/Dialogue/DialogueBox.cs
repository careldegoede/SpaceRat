using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceRat.Dialogue
{
    public enum DialogueBoxStyle
    {
        /// <summary>Glowing cyan capsule, ship AI on the cockpit glass. Used while flying.</summary>
        HoloCapsule,
        /// <summary>Hex badge + violet strip, comset radio link to the ship. Used on foot.</summary>
        Comset
    }

    /// <summary>
    /// The dialogue panel: speaker + line bottom-center, plus the line's voice clip.
    /// Drawn with IMGUI from procedurally baked textures (no art assets needed), in one of two
    /// styles from Images/Chatbox (1 = HoloCapsule for flying, 3 = Comset for walking).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class DialogueBox : MonoBehaviour
    {
        [Tooltip("HoloCapsule = in the ship (flying). Comset = on foot (walking).")]
        [SerializeField] DialogueBoxStyle style = DialogueBoxStyle.HoloCapsule;
        [SerializeField] Color computerColor = new(0.25f, 0.85f, 1f);
        [SerializeField] Color pilotColor = new(0.72f, 0.62f, 1f);
        [SerializeField] Color systemColor = new(0.82f, 0.86f, 0.94f);
        [SerializeField] Color captainColor = new(1f, 0.72f, 0.3f);
        [SerializeField, Range(0f, 1f)] float voiceVolume = 1f;
        [Tooltip("Comset footer, e.g. \"DIST 1.2 KM · SIGNAL 74%\". Empty hides it.")]
        public string comsetStatus = "";

        public DialogueBoxStyle Style { get => style; set => style = value; }

        /// <summary>
        /// Once the computer lives in the service bot: a round portrait of the bot shown in the computer's
        /// icon frame instead of the crosshair (null = crosshair).
        /// </summary>
        public Texture2D ComputerPortrait { get; set; }

        bool UsePortrait => ComputerPortrait && _icon == Icon.Computer;

        /// <summary>True while the current line's voice clip is still playing.</summary>
        public bool IsSpeaking => _src && _src.isPlaying;

        enum Icon { None, Computer, Pilot, Captain }

        AudioSource _src;
        AudioHighPassFilter _highPass;
        AudioLowPassFilter _lowPass;
        readonly float[] _samples = new float[256];
        float _level;

        string _name, _text;
        Icon _icon;
        Color _accent;
        float _shownAt;

        GUIStyle _nameStyle, _tagStyle, _capsuleBody, _comsetBody, _footStyle;
        Texture2D _white;
        readonly Dictionary<string, Texture2D> _cache = new();

        // ---- API ------------------------------------------------------------------------

        /// <summary>Shows the line and plays its voice clip (if any).</summary>
        public void Show(DialogueLine line)
        {
            Show(line.speaker, line.text);
            if (!line.voice) return;
            EnsureAudio();
            // Ship voice through the comset, and recorded station logs, get the thin radio band.
            bool radio = (style == DialogueBoxStyle.Comset && line.speaker == Speaker.Computer) || line.speaker == Speaker.Captain;
            _highPass.enabled = radio;
            _lowPass.enabled = radio;
            _src.clip = line.voice;
            _src.volume = voiceVolume;
            _src.Play();
        }

        /// <summary>Text only (stops any voice that was playing).</summary>
        public void Show(Speaker speaker, string text)
        {
            switch (speaker)
            {
                case Speaker.Computer: Set("COMPUTER", Icon.Computer, computerColor, text); break;
                case Speaker.Captain: Set("CAPT. VOSS", Icon.Captain, captainColor, text); break;
                default: Set("PILOT", Icon.Pilot, pilotColor, text); break;
            }
        }

        public void ShowSystem(string text) => Set("", Icon.None, systemColor, text);

        public void Clear()
        {
            _text = null;
            if (_src) _src.Stop();
        }

        void Set(string name, Icon icon, Color accent, string text)
        {
            if (_src) _src.Stop();
            if (text != _text) _shownAt = Time.unscaledTime;
            _name = name;
            _icon = icon;
            _accent = accent;
            _text = text;
        }

        // ---- Lifecycle ------------------------------------------------------------------

        void Awake() => EnsureAudio();

        void EnsureAudio()
        {
            if (_src) return;
            _src = GetComponent<AudioSource>();
            if (!_src) _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = false;
            _src.spatialBlend = 0f;
            _src.priority = 0;
            // Thin "radio" band for the ship's voice heard through the comset.
            _highPass = GetComponent<AudioHighPassFilter>();
            if (!_highPass) _highPass = gameObject.AddComponent<AudioHighPassFilter>();
            _highPass.cutoffFrequency = 450f;
            _highPass.enabled = false;
            _lowPass = GetComponent<AudioLowPassFilter>();
            if (!_lowPass) _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = 3800f;
            _lowPass.enabled = false;
        }

        void Update()
        {
            float target = 0f;
            if (IsSpeaking)
            {
                _src.GetOutputData(_samples, 0);
                float sum = 0f;
                foreach (var v in _samples) sum += v * v;
                target = Mathf.Clamp01(Mathf.Sqrt(sum / _samples.Length) * 6f);
            }
            _level = Mathf.Lerp(_level, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
        }

        void OnDestroy()
        {
            foreach (var t in _cache.Values) if (t) Destroy(t);
            _cache.Clear();
            if (_white) Destroy(_white);
        }

        // ---- Drawing ----------------------------------------------------------------------

        void OnGUI() => Draw(Screen.width, Screen.height);

        float _sw, _sh;   // canvas size for this draw

        /// <summary>Draws the box onto a canvas of the given size (the screen in game; an offscreen preview in the editor).</summary>
        public void Draw(float canvasWidth, float canvasHeight)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(_text)) return;
            _sw = canvasWidth;
            _sh = canvasHeight;
            EnsureStyles();
            float s = _sh / 1080f;
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((Time.unscaledTime - _shownAt) / 0.18f));
            if (style == DialogueBoxStyle.HoloCapsule) DrawCapsule(s);
            else DrawComset(s);
            GUI.color = prev;
        }

        void DrawCapsule(float s)
        {
            float baseH = Mathf.Round(150f * s);
            float w = Mathf.Round(Mathf.Min(_sw - 32f, 1480f * s));
            float textLeft = baseH * 1.2f, rightPad = baseH * 0.5f;
            float top = 24f * s, headerH = 38f * s;

            _nameStyle.fontSize = Px(24 * s);
            _tagStyle.fontSize = Px(17 * s);
            _capsuleBody.fontSize = Px(36 * s);
            float bodyW = w - textLeft - rightPad;
            var body = new GUIContent(_text);
            float bodyH = _capsuleBody.CalcHeight(body, bodyW);
            float h = Mathf.Round(Mathf.Max(baseH, top + headerH + bodyH + 22f * s));
            float x = Mathf.Round((_sw - w) * 0.5f), y = Mathf.Round(_sh - h - 44f * s);
            float glow = Mathf.Round(24f * s);

            GUI.DrawTexture(new Rect(x - glow, y - glow, w + glow * 2, h + glow * 2),
                CapsuleTex((int)w, (int)h, (int)baseH, (int)glow, s, _accent));

            float iconD = Mathf.Round(baseH * 0.76f), iconGlow = Mathf.Round(12f * s);
            float cx = x + baseH * 0.5f, cy = y + h * 0.5f;
            float iconSize = iconD + iconGlow * 2;
            GUI.DrawTexture(new Rect(cx - iconSize * 0.5f, cy - iconSize * 0.5f, iconSize, iconSize),
                IconTex(UsePortrait ? Icon.None : _icon, false, (int)iconSize, (int)iconSize, (int)iconGlow, s, _accent));
            if (UsePortrait)
            {
                float pd = iconD * 0.86f;
                GUI.DrawTexture(new Rect(cx - pd * 0.5f, cy - pd * 0.5f, pd, pd), ComputerPortrait);
            }

            float tx = x + textLeft;
            _nameStyle.normal.textColor = _accent;
            GUI.Label(new Rect(tx, y + top, bodyW, headerH), _name, _nameStyle);
            if (_icon != Icon.None)
            {
                _tagStyle.normal.textColor = Color.Lerp(_accent, Color.black, 0.25f);
                _tagStyle.alignment = TextAnchor.UpperRight;
                GUI.Label(new Rect(tx, y + top + 4f * s, bodyW + rightPad * 0.15f, headerH),
                    _icon switch
                    {
                        Icon.Computer => "SHIP AI  ·  CH 01",
                        Icon.Captain => "STATION LOG  ·  KESSEL-9",
                        _ => "COCKPIT  ·  CH 01"
                    }, _tagStyle);
            }
            _capsuleBody.normal.textColor = new Color(0.9f, 0.96f, 1f);
            GUI.Label(new Rect(tx, y + top + (_name.Length > 0 ? headerH : 6f * s), bodyW, bodyH), body, _capsuleBody);

            if (!IsSpeaking) DrawContinueArrow(x + w - rightPad * 0.62f, y + h - 34f * s, s);
        }

        void DrawComset(float s)
        {
            float baseH = Mathf.Round(160f * s);
            float hexH = Mathf.Round(baseH * 0.9f), hexW = Mathf.Round(hexH * 1.16f);
            float hexGlow = Mathf.Round(10f * s);
            float w = Mathf.Round(Mathf.Min(_sw - 32f, 1480f * s));
            float panelOffset = hexW * 0.8f, textLeft = hexW * 1.2f, rightPad = 60f * s;
            float top = 24f * s, headerH = 40f * s;
            bool footer = !string.IsNullOrEmpty(comsetStatus);

            _nameStyle.fontSize = Px(24 * s);
            _tagStyle.fontSize = Px(18 * s);
            _comsetBody.fontSize = Px(34 * s);
            _footStyle.fontSize = Px(18 * s);
            float bodyW = w - textLeft - rightPad;
            var body = new GUIContent(_text);
            float bodyH = _comsetBody.CalcHeight(body, bodyW);
            float h = Mathf.Round(Mathf.Max(baseH, top + headerH + bodyH + (footer ? 36f * s : 0f) + 20f * s));
            float x = Mathf.Round((_sw - w) * 0.5f), y = Mathf.Round(_sh - h - 44f * s);

            // Panel strip: violet gradient with thin top/bottom rules.
            var panel = new Rect(x + panelOffset, y, w - panelOffset, h);
            GUI.DrawTexture(panel, GradientTex());
            var rule = Color.Lerp(_accent, Color.black, 0.45f);
            Fill(new Rect(panel.x, panel.y, panel.width, Mathf.Max(1f, 1.5f * s)), rule);
            Fill(new Rect(panel.x, panel.yMax - Mathf.Max(1f, 1.5f * s), panel.width, Mathf.Max(1f, 1.5f * s)), rule);

            // Hex badge overlapping the strip's left edge.
            float bw = hexW + hexGlow * 2, bh = hexH + hexGlow * 2;
            GUI.DrawTexture(new Rect(x - hexGlow, y + h * 0.5f - bh * 0.5f, bw, bh),
                IconTex(UsePortrait ? Icon.None : _icon, true, (int)bw, (int)bh, (int)hexGlow, s, _accent));
            if (UsePortrait)
            {
                float pd = hexH * 0.84f;
                GUI.DrawTexture(new Rect(x - hexGlow + bw * 0.5f - pd * 0.5f, y + h * 0.5f - pd * 0.5f, pd, pd), ComputerPortrait);
            }

            float tx = x + textLeft;
            _nameStyle.normal.textColor = Color.Lerp(_accent, Color.white, 0.75f);
            GUI.Label(new Rect(tx, y + top, bodyW, headerH), _name, _nameStyle);
            if (_icon != Icon.None)
            {
                float nameW = _nameStyle.CalcSize(new GUIContent(_name)).x;
                _tagStyle.normal.textColor = new Color(1f, 0.58f, 0.48f);
                _tagStyle.alignment = TextAnchor.UpperLeft;
                GUI.Label(new Rect(tx + nameW + 26f * s, y + top + 3f * s, bodyW, headerH),
                    _icon switch
                    {
                        Icon.Computer => "●  SHIP AI  ·  COMSET LINK",
                        Icon.Captain => "●  STATION LOG  ·  RECORDED",
                        _ => "●  COMSET LINK  ·  SHIP"
                    }, _tagStyle);
                DrawWaveform(panel.xMax - rightPad - 320f * s, y + top + 12f * s, 320f * s, s);
            }

            float by = y + top + (_name.Length > 0 ? headerH : 6f * s);
            _comsetBody.normal.textColor = new Color(0.93f, 0.92f, 1f);
            GUI.Label(new Rect(tx, by, bodyW, bodyH), body, _comsetBody);
            if (footer)
            {
                _footStyle.normal.textColor = Color.Lerp(_accent, new Color(0.1f, 0.08f, 0.2f), 0.4f);
                GUI.Label(new Rect(tx, by + bodyH + 6f * s, bodyW, 30f * s), comsetStatus, _footStyle);
            }
        }

        static readonly float[] WavePattern = { 0.45f, 0.8f, 0.4f, 0.65f, 0.3f, 1f, 0.5f, 0.75f, 0.35f, 0.6f, 0.9f, 0.4f, 0.7f, 0.3f, 0.55f, 0.35f };

        void DrawWaveform(float x, float yMid, float width, float s)
        {
            var c = Color.Lerp(_accent, Color.white, 0.1f);
            float line = Mathf.Max(1.5f, 3f * s), bar = Mathf.Max(1.5f, 3f * s), gap = 9f * s, maxH = 32f * s;
            float lead = 20f * s;
            Fill(new Rect(x, yMid - line * 0.5f, lead, line), c);
            float bx = x + lead + gap;
            for (int i = 0; i < WavePattern.Length; i++, bx += bar + gap)
            {
                float live = Mathf.PerlinNoise(Time.unscaledTime * 9f, i * 1.7f);
                float k = Mathf.Lerp(WavePattern[i] * 0.55f, 0.2f + live * 1.1f, _level);
                float bh = Mathf.Max(line, maxH * Mathf.Clamp01(k));
                Fill(new Rect(bx, yMid - bh * 0.5f, bar, bh), c);
            }
            if (bx < x + width) Fill(new Rect(bx, yMid - line * 0.5f, x + width - bx, line), c);
        }

        void DrawContinueArrow(float cx, float cy, float s)
        {
            int size = Mathf.Max(8, Px(22 * s));
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, prev.a * (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f)));
            GUI.DrawTexture(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), ArrowTex(size, _accent));
            GUI.color = prev;
        }

        void Fill(Rect r, Color c)
        {
            if (!_white) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
            var prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        static int Px(float v) => Mathf.Max(8, Mathf.RoundToInt(v));

        void EnsureStyles()
        {
            if (_nameStyle != null) return;
            Font Load(string n) => Resources.Load<Font>("Fonts/" + n);
            var orbitron = Load("Orbitron");
            var rajdhani = Load("Rajdhani-SemiBold");
            var rajdhaniMed = Load("Rajdhani-Medium");
            var exo = Load("Exo2");
            GUIStyle Make(Font f, bool wrap) => new(GUI.skin.label)
            {
                font = f ? f : GUI.skin.font, wordWrap = wrap, richText = false,
                alignment = TextAnchor.UpperLeft, padding = new RectOffset(), margin = new RectOffset()
            };
            _nameStyle = Make(orbitron, false);
            _nameStyle.fontStyle = FontStyle.Bold;
            _tagStyle = Make(rajdhaniMed, false);
            _capsuleBody = Make(rajdhani, true);
            _comsetBody = Make(exo, true);
            _footStyle = Make(exo, false);
        }

        // ---- Texture baking (signed distance fields, cached per size/colour) -------------

        Texture2D Cached(string key, int w, int h, Func<float, float, Color> shade)
        {
            if (_cache.TryGetValue(key, out var t) && t) return t;
            if (_cache.Count > 24)
            {
                foreach (var old in _cache.Values) if (old) Destroy(old);
                _cache.Clear();
            }
            t = new Texture2D(Mathf.Max(1, w), Mathf.Max(1, h), TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
            };
            var px = new Color[t.width * t.height];
            for (int j = 0; j < t.height; j++)
            for (int i = 0; i < t.width; i++)
                px[j * t.width + i] = shade(i + 0.5f - t.width * 0.5f, j + 0.5f - t.height * 0.5f);
            t.SetPixels(px);
            t.Apply(false, true);
            _cache[key] = t;
            return t;
        }

        Texture2D CapsuleTex(int w, int h, int baseH, int glow, float s, Color accent)
        {
            string key = $"cap{w}x{h}r{baseH}g{glow}{ColorUtility.ToHtmlStringRGB(accent)}";
            float r = baseH * 0.5f, stroke = Mathf.Max(2f, 3.2f * s), divX = -w * 0.5f + baseH;
            var hi = Color.Lerp(accent, Color.white, 0.2f);
            float th = h + glow * 2f;
            return Cached(key, w + glow * 2, h + glow * 2, (px, py) =>
            {
                float d = RoundBox(px, py, w * 0.5f, h * 0.5f, r);
                var c = Color.clear;
                if (d > 0f) Over(ref c, accent, 0.5f * Mathf.Exp(-d / (glow * 0.32f)));
                float inside = 1f - Smooth(-0.75f, 0.75f, d);
                float v = (py + th * 0.5f) / th;
                Over(ref c, Color.Lerp(new Color(0.015f, 0.05f, 0.07f), new Color(0.04f, 0.1f, 0.13f), v), 0.9f * inside);
                if (d < 0f) Over(ref c, accent, 0.22f * Mathf.Exp(d / (glow * 0.35f)) * inside);
                Over(ref c, hi, Line(Mathf.Abs(px - divX), stroke) * inside);
                Over(ref c, hi, Line(Mathf.Abs(d), stroke));
                return c;
            });
        }

        Texture2D IconTex(Icon icon, bool hex, int w, int h, int glow, float s, Color accent)
        {
            string key = $"icon{icon}{hex}{w}x{h}g{glow}{ColorUtility.ToHtmlStringRGB(accent)}";
            float stroke = Mathf.Max(2f, (hex ? 3.6f : 3.2f) * s), thin = Mathf.Max(1.5f, 2.6f * s);
            float frameR = (hex ? h : Mathf.Min(w, h)) * 0.5f - glow;     // circle radius / hex inradius
            float R = frameR * (hex ? 0.95f : 1f);                          // content scale
            var hi = Color.Lerp(accent, Color.white, 0.2f);
            var dark = hex ? new Color(0.06f, 0.05f, 0.14f) : new Color(0.02f, 0.06f, 0.08f);
            return Cached(key, w, h, (px, py) =>
            {
                float d = hex ? Hexagon(px, py, frameR) : Mathf.Sqrt(px * px + py * py) - frameR;
                var c = Color.clear;
                if (d > 0f) Over(ref c, accent, 0.4f * Mathf.Exp(-d / (glow * 0.35f)));
                float inside = 1f - Smooth(-0.75f, 0.75f, d);
                float radial = Mathf.Clamp01(Mathf.Sqrt(px * px + py * py) / frameR);
                Over(ref c, Color.Lerp(Color.Lerp(dark, accent, 0.18f), dark, radial), 0.96f * inside);
                Over(ref c, hi, Line(Mathf.Abs(d), stroke));
                float g = IconGlyph(icon, px / R, py / R, thin / R);
                Over(ref c, hi, g * inside);
                return c;
            });
        }

        /// <summary>Coverage (0..1) of the speaker glyph at p (in units of the frame radius).</summary>
        static float IconGlyph(Icon icon, float x, float y, float sw)
        {
            float aa = 0.012f;
            float Stroke(float d) => 1f - Smooth(sw * 0.5f - aa, sw * 0.5f + aa, Mathf.Abs(d));
            float Solid(float d) => 1f - Smooth(-aa, aa, d);
            switch (icon)
            {
                case Icon.Computer:
                {
                    float len = Mathf.Sqrt(x * x + y * y);
                    float a = Mathf.Max(Stroke(len - 0.4f), Solid(len - 0.17f));
                    float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                    a = Mathf.Max(a, Stroke(Segment(ax, ay, 0.3f, 0f, 0.62f, 0f)));
                    a = Mathf.Max(a, Stroke(Segment(ax, ay, 0f, 0.3f, 0f, 0.62f)));
                    return a;
                }
                case Icon.Pilot:
                {
                    // Helmet: dome shell, visor slot, base line.
                    float shell = RoundBox4(x, y - 0.05f, 0.4f, 0.42f, 0.4f, 0.04f);
                    float visor = RoundBox(x, y + 0.02f, 0.27f, 0.08f, 0.03f);
                    float a = Mathf.Max(Stroke(shell), Stroke(visor));
                    return Mathf.Max(a, Stroke(Segment(x, y, -0.5f, -0.52f, 0.5f, -0.52f)));
                }
                case Icon.Captain:
                {
                    // Recorded log: a play triangle inside a thin ring.
                    float len = Mathf.Sqrt(x * x + y * y);
                    float tri = Mathf.Max(-(x + 0.16f), Mathf.Abs(y) * 1.05f + x * 0.62f - 0.2f);
                    return Mathf.Max(Stroke(len - 0.46f), Solid(tri));
                }
                default:
                    return Solid(Mathf.Sqrt(x * x + y * y) - 0.12f);
            }
        }

        Texture2D ArrowTex(int size, Color accent)
        {
            float h = size * 0.5f;
            return Cached($"arrow{size}{ColorUtility.ToHtmlStringRGB(accent)}", size, size, (px, py) =>
            {
                // Downward triangle: top edge at +0.45h, tip at -0.4h.
                float d = Mathf.Max(py - 0.45f * h, Mathf.Abs(px) * 0.88f - (py + 0.4f * h) * 0.47f);
                var c = accent;
                c.a = 1f - Smooth(-0.75f, 0.75f, d);
                return c;
            });
        }

        Texture2D GradientTex()
        {
            return Cached("comsetGradient", 256, 1, (px, py) =>
            {
                float t = (px + 128f) / 256f;
                return Color.Lerp(new Color(0.13f, 0.1f, 0.28f, 0.94f), new Color(0.07f, 0.06f, 0.16f, 0.86f), t);
            });
        }

        // ---- SDF helpers ----------------------------------------------------------------

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
        static float Line(float dist, float width) => 1f - Smooth(width * 0.5f - 0.7f, width * 0.5f + 0.7f, dist);

        static float RoundBox(float x, float y, float hx, float hy, float r)
        {
            r = Mathf.Min(r, Mathf.Min(hx, hy));
            float qx = Mathf.Abs(x) - hx + r, qy = Mathf.Abs(y) - hy + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>Rounded box with separate top / bottom corner radii (y up).</summary>
        static float RoundBox4(float x, float y, float hx, float hy, float rTop, float rBottom)
        {
            float r = y > 0f ? rTop : rBottom;
            return RoundBox(x, y, hx, hy, r);
        }

        /// <summary>Flat-top hexagon (points left/right) with inradius r.</summary>
        static float Hexagon(float x, float y, float r)
        {
            const float kx = -0.866025404f, ky = 0.5f, kz = 0.577350269f;
            float px = Mathf.Abs(x), py = Mathf.Abs(y);
            float dot = Mathf.Min(kx * px + ky * py, 0f);
            px -= 2f * dot * kx;
            py -= 2f * dot * ky;
            px -= Mathf.Clamp(px, -kz * r, kz * r);
            py -= r;
            return Mathf.Sqrt(px * px + py * py) * Mathf.Sign(py);
        }

        static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static void Over(ref Color dst, Color src, float a)
        {
            if (a <= 0f) return;
            float oa = a + dst.a * (1f - a);
            float k = dst.a * (1f - a);
            dst = new Color((src.r * a + dst.r * k) / oa, (src.g * a + dst.g * k) / oa, (src.b * a + dst.b * k) / oa, oa);
        }
    }
}
