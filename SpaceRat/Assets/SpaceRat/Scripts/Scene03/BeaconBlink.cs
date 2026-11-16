using UnityEngine;

namespace SpaceRat.Scene03
{
    /// <summary>Outpost antenna beacon: a double red blink (light + emissive bulb), like an aviation strobe.</summary>
    public class BeaconBlink : MonoBehaviour
    {
        [SerializeField] Light beaconLight;
        [SerializeField] Renderer beaconRenderer;
        [SerializeField] float period = 1.8f;
        [SerializeField] float lightIntensity = 6f;
        [SerializeField] Color emission = new(4f, 0.3f, 0.2f);

        MaterialPropertyBlock _block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Update()
        {
            float t = Mathf.Repeat(Time.time, period);
            bool on = t < 0.12f || (t > 0.3f && t < 0.42f);       // blink-blink ... pause
            if (beaconLight) beaconLight.intensity = on ? lightIntensity : 0f;
            if (beaconRenderer)
            {
                _block ??= new MaterialPropertyBlock();
                beaconRenderer.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, on ? emission : emission * 0.05f);
                beaconRenderer.SetPropertyBlock(_block);
            }
        }
    }
}
