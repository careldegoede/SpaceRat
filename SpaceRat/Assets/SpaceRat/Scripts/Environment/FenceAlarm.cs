using System.Collections.Generic;
using UnityEngine;

namespace SpaceRat.Environment
{
    /// <summary>
    /// Sci-fi fence alarm: while <see cref="Alarm"/> is on, every glow / field renderer in this piece flashes
    /// between the blue and red materials. All fences flash in sync (driven by Time.time).
    /// Off = each renderer shows the material it was built with. Play mode only.
    /// </summary>
    public class FenceAlarm : MonoBehaviour
    {
        [SerializeField] bool alarm;
        [Tooltip("Colour changes per second.")]
        [SerializeField] float flashRate = 4f;
        [Tooltip("Blink the lights off briefly now and then while the alarm is on.")]
        [SerializeField] bool flicker = true;
        [SerializeField] Material blueGlow, blueField, redGlow, redField;

        struct Slot { public Renderer renderer; public Material original, blue, red; }
        readonly List<Slot> _slots = new();
        int _shown = -1; // 0 = original, 1 = blue, 2 = red, 3 = off

        public bool Alarm
        {
            get => alarm;
            set => alarm = value;
        }

        void Awake()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (!m) continue;
                if (m == blueGlow || m == redGlow) _slots.Add(new Slot { renderer = r, original = m, blue = blueGlow, red = redGlow });
                else if (m == blueField || m == redField) _slots.Add(new Slot { renderer = r, original = m, blue = blueField, red = redField });
            }
        }

        void Update()
        {
            int want = 0;
            if (alarm)
            {
                float t = Time.time * flashRate;
                want = Mathf.FloorToInt(t) % 2 == 0 ? 2 : 1;
                // Short dropouts: a deterministic hash per flash, so every fence flickers together.
                if (flicker && Hash(Mathf.FloorToInt(t * 6f)) < 0.12f) want = 3;
            }
            if (want == _shown) return;
            _shown = want;
            foreach (var s in _slots)
            {
                if (!s.renderer) continue;
                s.renderer.enabled = want != 3;
                if (want == 3) continue;
                s.renderer.sharedMaterial = want == 0 ? s.original : want == 1 ? s.blue : s.red;
            }
        }

        static float Hash(int n)
        {
            uint h = (uint)n * 2654435761u;
            h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }
    }
}
