using UnityEngine;
using UnityEngine.UI;

namespace SpaceRat.UI
{
    /// <summary>
    /// Scene 2 flight HUD on the cockpit canvas: bearing marker on the signal source (clamped to the
    /// screen edge when off-screen), signal / distance / speed readouts, boost bar and a permanent
    /// WARP DRIVE: OFFLINE status. Keeps the centre of the windshield clear.
    /// </summary>
    public class Scene02Hud : MonoBehaviour
    {
        [SerializeField] RectTransform canvasRect;
        [SerializeField] RectTransform marker;
        [SerializeField] Text markerLabel;
        [SerializeField] Text navText;
        [SerializeField] Text statusText;
        [SerializeField] Image boostFill;
        [SerializeField] GameObject boostRoot;
        [SerializeField] float edgeMargin = 60f;

        [Header("Radar (nose radar: centre = where the nose points, rim = behind you)")]
        [SerializeField] RectTransform radar;
        [SerializeField] RectTransform radarDot;
        [SerializeField] Text radarText;
        [SerializeField] Transform ship;
        [Tooltip("Radar screen centre and radius in the radar image, normalised to the image rect (0-1, bottom-left origin).")]
        [SerializeField] Vector2 radarCentre = new(0.423f, 0.512f);
        [SerializeField] float radarRadius = 0.205f;

        Camera _cam;
        Transform _target;
        bool _scanning;

        void Awake()
        {
            SetTarget(null);
            ShowFlight(false);
            SetStatus("WARP DRIVE: OFFLINE");
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            if (marker) marker.gameObject.SetActive(target);
            if (radarDot) radarDot.gameObject.SetActive(target);
        }

        public void ShowRadar(bool on) { if (radar) radar.gameObject.SetActive(on); }

        public void SetRadarText(string text) { if (radarText) radarText.text = text; }

        public void ShowFlight(bool on)
        {
            if (navText) navText.enabled = on || _scanning;
            if (boostRoot) boostRoot.SetActive(on);
        }

        public void SetScanning(bool on)
        {
            _scanning = on;
            if (navText) { navText.enabled = on || navText.enabled; if (on) navText.text = "PASSIVE SCAN..."; }
        }

        public void SetStatus(string text)
        {
            if (statusText) statusText.text = text;
        }

        public void SetReadouts(float signal01, float distanceMeters, float speed, bool distorted)
        {
            if (!navText || _scanning) return;
            string sig = distorted ? "S#G%AL ??%" : $"SIGNAL {Mathf.RoundToInt(signal01 * 100f)}%";
            navText.text = $"{sig}\nDIST {distanceMeters / 1000f:0.0} km\nSPEED {Mathf.RoundToInt(speed)} m/s";
        }

        public void SetBoost(float energy01, bool boosting)
        {
            if (!boostFill) return;
            var rt = boostFill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Max(0.001f, energy01), rt.anchorMax.y);
            boostFill.color = boosting ? new Color(1f, 0.75f, 0.3f) : new Color(0.35f, 0.9f, 1f);
        }

        void LateUpdate()
        {
            if (!_target) return;
            UpdateRadar();
            if (!marker) return;
            if (!_cam) _cam = Camera.main;
            if (!_cam) return;

            // On-screen marker only when the planet is actually in view — the pilot has to find it.
            Vector3 vp = _cam.WorldToViewportPoint(_target.position);
            Vector2 size = canvasRect.rect.size;
            Vector2 p = new((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
            Vector2 half = size * 0.5f - Vector2.one * edgeMargin;
            bool visible = vp.z > 0f && Mathf.Abs(p.x) < half.x && Mathf.Abs(p.y) < half.y;
            marker.gameObject.SetActive(visible);
            if (visible) marker.anchoredPosition = p;
        }

        void UpdateRadar()
        {
            if (!radar || !radarDot || !ship || !radarDot.gameObject.activeSelf) return;
            Vector3 local = ship.InverseTransformDirection((_target.position - ship.position).normalized);
            float angle = Vector3.Angle(Vector3.forward, local);                  // 0 = dead ahead, 180 = behind
            Vector2 dir = new Vector2(local.x, local.y);
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.down;
            Vector2 rsize = radar.rect.size;
            Vector2 centre = new((radarCentre.x - 0.5f) * rsize.x, (radarCentre.y - 0.5f) * rsize.y);
            float r = radarRadius * rsize.x * Mathf.Clamp01(angle / 180f);
            radarDot.anchoredPosition = centre + dir * r;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            radarDot.localScale = Vector3.one * pulse;
        }
    }
}
