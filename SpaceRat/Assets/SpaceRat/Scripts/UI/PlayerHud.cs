using SpaceRat.Characters;
using UnityEngine;

namespace SpaceRat.UI
{
    /// <summary>
    /// Top-left status bar (Images/Chrome Sci-Fi HUD_ Health, Stamina, Weapon Charge.png, keyed into
    /// Art/HUD/HudBar.png with the bar slots cut out). The coloured fills are drawn underneath the frame:
    /// health (with a trailing "lost" sliver and a portrait flash on damage), stamina (blinks when
    /// exhausted) and weapon charge (full blue until there is a weapon that uses it).
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        [SerializeField] Texture2D frame;
        [SerializeField] PlayerVitals vitals;
        [Tooltip("HUD width at 1080p (scales with screen height).")]
        [SerializeField] float width = 700f;
        [SerializeField] Vector2 margin = new(24f, 20f);

        [Header("Fill colours")]
        [SerializeField] Color healthColor = new(1f, 0.16f, 0.12f);
        [SerializeField] Color staminaColor = new(0.42f, 1f, 0.18f);
        [SerializeField] Color weaponColor = new(0.2f, 0.72f, 1f);

        public bool Visible { get; set; }

        // Layout in frame-texture UV (x right, y down), measured from the art.
        static readonly Vector2[] HealthSlot = { new(0.2047f, 0.4953f), new(0.4048f, 0.4953f), new(0.3923f, 0.6372f), new(0.1941f, 0.6372f) };
        static readonly Vector2[] StaminaSlot = { new(0.4427f, 0.4953f), new(0.6654f, 0.4953f), new(0.6534f, 0.6372f), new(0.4316f, 0.6372f) };
        static readonly Vector2[] WeaponSlot = { new(0.7043f, 0.4953f), new(0.9016f, 0.4953f), new(0.9016f, 0.6372f), new(0.6927f, 0.6372f) };
        static readonly Vector2 PortraitCenter = new(0.1137f, 0.5721f);
        const float PortraitRadius = 0.3302f;                       // in units of the frame's height

        float _alpha, _health = 1f, _healthLag = 1f, _lagHoldUntil, _stamina = 1f, _weapon = 1f, _hitFlash;
        Texture2D _healthFill, _staminaFill, _weaponFill, _lagFill, _disc, _white;

        void OnEnable() { if (vitals) vitals.Damaged += OnDamaged; }
        void OnDisable() { if (vitals) vitals.Damaged -= OnDamaged; }

        void OnDamaged(float amount)
        {
            _hitFlash = 1f;
            _lagHoldUntil = Time.time + 0.6f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _alpha = Mathf.MoveTowards(_alpha, Visible ? 1f : 0f, Time.unscaledDeltaTime / 0.6f);
            if (!vitals) return;
            _health = Mathf.MoveTowards(_health, vitals.Health01, dt * 2f);
            if (Time.time > _lagHoldUntil) _healthLag = Mathf.MoveTowards(_healthLag, _health, dt * 0.35f);
            if (_healthLag < _health) _healthLag = _health;
            _stamina = Mathf.Lerp(_stamina, vitals.Stamina01, 1f - Mathf.Exp(-dt * 12f));
            _weapon = Mathf.Lerp(_weapon, vitals.WeaponCharge01, 1f - Mathf.Exp(-dt * 8f));
            _hitFlash = Mathf.MoveTowards(_hitFlash, 0f, dt * 2.2f);
        }

        void OnDestroy()
        {
            foreach (var t in new[] { _healthFill, _staminaFill, _weaponFill, _lagFill, _disc, _white })
                if (t) Destroy(t);
        }

        void OnGUI() => Draw(Screen.width, Screen.height);

        /// <summary>Draws onto a canvas of the given size (the screen in game; an offscreen preview in the editor).</summary>
        public void Draw(float screenWidth, float screenHeight)
        {
            if (_alpha <= 0.001f || Event.current.type != EventType.Repaint || !frame) return;
            float s = screenHeight / 1080f;
            float w = width * s, h = w * frame.height / frame.width;
            var r = new Rect(margin.x * s, margin.y * s, w, h);
            var prev = GUI.color;
            float t = Time.time;

            // Fills first (they show through the cut-out slots), frame on top.
            Fill(r, HealthSlot, ref _lagFill, new Color(1f, 0.85f, 0.7f), _healthLag, 0.85f);
            bool lowHealth = vitals && vitals.Health01 < 0.25f;
            float healthPulse = lowHealth ? 0.65f + 0.35f * Mathf.Sin(t * 7f) : 1f;
            Fill(r, HealthSlot, ref _healthFill, healthColor, _health, healthPulse);

            bool exhausted = vitals && vitals.Exhausted;
            float staminaAlpha = exhausted ? 0.45f + 0.35f * Mathf.Sin(t * 9f) : 1f;
            Fill(r, StaminaSlot, ref _staminaFill, staminaColor, _stamina, staminaAlpha);

            Fill(r, WeaponSlot, ref _weaponFill, weaponColor, _weapon, 1f);

            GUI.color = new Color(1f, 1f, 1f, _alpha);
            GUI.DrawTexture(r, frame);

            // Portrait: red flash when hit, slow red pulse when health is low.
            float red = Mathf.Max(_hitFlash * 0.6f, lowHealth ? 0.18f + 0.14f * Mathf.Sin(t * 4f) : 0f);
            if (red > 0.001f)
            {
                float pr = PortraitRadius * h;
                var c = new Vector2(r.x + PortraitCenter.x * w, r.y + PortraitCenter.y * h);
                GUI.color = new Color(1f, 0.08f, 0.05f, red * _alpha);
                GUI.DrawTexture(new Rect(c.x - pr, c.y - pr, pr * 2f, pr * 2f), Disc());
            }
            GUI.color = prev;
        }

        void Fill(Rect hud, Vector2[] slot, ref Texture2D tex, Color color, float fraction, float alpha)
        {
            if (fraction <= 0.001f) return;
            Vector2 min = slot[0], max = slot[0];
            foreach (var p in slot) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            var area = new Rect(hud.x + min.x * hud.width, hud.y + min.y * hud.height,
                                (max.x - min.x) * hud.width, (max.y - min.y) * hud.height);
            if (!tex) tex = FillTexture(slot, min, max, color);
            GUI.color = new Color(1f, 1f, 1f, alpha * _alpha);
            GUI.BeginGroup(new Rect(area.x, area.y, area.width * Mathf.Clamp01(fraction), area.height));
            GUI.DrawTexture(new Rect(0f, 0f, area.width, area.height), tex);
            GUI.EndGroup();
        }

        /// <summary>The slot's parallelogram, filled with a glossy vertical gradient (anti-aliased).</summary>
        static Texture2D FillTexture(Vector2[] slot, Vector2 min, Vector2 max, Color color)
        {
            const int tw = 256, th = 40;
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[tw * th];
            var size = max - min;
            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float cover = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    // Texture rows go bottom-up; slot UV y goes down.
                    var uv = new Vector2(min.x + (x + 0.25f + sx * 0.5f) / tw * size.x,
                                         min.y + (1f - (y + 0.25f + sy * 0.5f) / th) * size.y);
                    if (Inside(uv, slot)) cover += 0.25f;
                }
                float v = (y + 0.5f) / th;                                    // 0 bottom .. 1 top
                var c = Color.Lerp(color * 0.55f, Color.Lerp(color, Color.white, 0.35f), v);
                if (v > 0.62f && v < 0.78f) c = Color.Lerp(c, Color.white, 0.25f); // glossy stripe
                c.a = cover;
                px[y * tw + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        Texture2D White()
        {
            if (_white) return _white;
            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();
            return _white;
        }

        Texture2D Disc()
        {
            if (_disc) return _disc;
            const int n = 128;
            _disc = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x + 0.5f - n * 0.5f, y + 0.5f - n * 0.5f).magnitude / (n * 0.5f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 6f) * Mathf.Lerp(1f, 0.55f, d));
            }
            _disc.SetPixels(px);
            _disc.Apply();
            return _disc;
        }
    }
}
