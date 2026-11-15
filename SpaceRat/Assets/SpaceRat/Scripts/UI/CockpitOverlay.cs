using UnityEngine;
using UnityEngine.UI;

namespace SpaceRat.UI
{
    /// <summary>
    /// Screen-space cockpit presentation for Scene 1: the transparent cockpit artwork
    /// (3D sun visible through the windows), an eyelid fade for the wake-up, a white-out
    /// flash for impact, a minimal heat/time HUD, a centre prompt and the title card.
    /// All elements are created by the scene builder and only driven from here.
    /// </summary>
    public class CockpitOverlay : MonoBehaviour
    {
        [SerializeField] Image cockpit;
        [SerializeField] Image eyelid;
        [SerializeField] Image flash;
        [SerializeField] GameObject hud;
        [SerializeField] Image heatFill;
        [SerializeField] Text timerText;
        [SerializeField] Text promptText;
        [SerializeField] Text titleText;

        [SerializeField] Color hotTint = new(1f, 0.62f, 0.5f);
        [SerializeField] Color heatCold = new(0.35f, 0.9f, 1f);
        [SerializeField] Color heatHot = new(1f, 0.25f, 0.1f);

        float _promptBlinkUntil;
        float _promptBlinkRate;

        void Awake()
        {
            ShowCockpit(false);
            SetEyelid(0f);
            SetFlash(Color.white, 0f);
            ShowHud(false);
            SetPrompt(null);
            SetTitle(0f);
        }

        public void ShowCockpit(bool on) { if (cockpit) cockpit.enabled = on; }

        public void SetEyelid(float alpha) => SetAlpha(eyelid, alpha);

        public void SetFlash(Color color, float alpha)
        {
            if (!flash) return;
            color.a = Mathf.Clamp01(alpha);
            flash.color = color;
            flash.enabled = alpha > 0.001f;
        }

        public void ShowHud(bool on) { if (hud) hud.SetActive(on); }

        public void SetHeat(float heat, float secondsLeft)
        {
            heat = Mathf.Clamp01(heat);
            if (heatFill)
            {
                var rt = heatFill.rectTransform;
                rt.anchorMax = new Vector2(Mathf.Max(0.001f, heat), rt.anchorMax.y);
                heatFill.color = Color.Lerp(heatCold, heatHot, heat);
            }
            if (timerText) timerText.text = $"HULL HEAT {Mathf.RoundToInt(heat * 100f)}%   ·   {secondsLeft:0.0}s";
            if (cockpit) cockpit.color = Color.Lerp(Color.white, hotTint, heat * 0.8f);
        }

        /// <summary>Shows a centre prompt; null hides it. Optional blink for a few seconds.</summary>
        public void SetPrompt(string text, float blinkSeconds = 0f, float blinkRate = 3f)
        {
            if (!promptText) return;
            promptText.text = text ?? "";
            promptText.enabled = !string.IsNullOrEmpty(text);
            _promptBlinkUntil = Time.time + blinkSeconds;
            _promptBlinkRate = blinkRate;
        }

        public void SetTitle(float alpha) => SetAlpha(titleText, alpha);

        void Update()
        {
            if (promptText && promptText.enabled && Time.time < _promptBlinkUntil)
            {
                var c = promptText.color;
                c.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.time * _promptBlinkRate * Mathf.PI * 2f));
                promptText.color = c;
            }
            else if (promptText)
            {
                var c = promptText.color;
                c.a = 1f;
                promptText.color = c;
            }
        }

        static void SetAlpha(Graphic g, float alpha)
        {
            if (!g) return;
            var c = g.color;
            c.a = Mathf.Clamp01(alpha);
            g.color = c;
            g.enabled = alpha > 0.001f;
        }
    }
}
