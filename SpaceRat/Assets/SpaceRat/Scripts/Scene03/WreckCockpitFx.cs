using UnityEngine;

namespace SpaceRat.Scene03
{
    /// <summary>
    /// Wrecked-cockpit ambience for the Scene 3 opening: pulsing red emergency light, random spark
    /// bursts from the console (with a flash light) and a light handheld shake on the shot cameras.
    /// </summary>
    public class WreckCockpitFx : MonoBehaviour
    {
        [SerializeField] Light redLight;
        [SerializeField] float redIntensity = 3f;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] Light sparkLight;
        [SerializeField] float sparkFlash = 5f;
        [SerializeField] Vector2 sparkInterval = new(0.35f, 2.2f);
        [SerializeField] Transform[] shakeCameras;
        [SerializeField] float shakePosition = 0.008f;
        [SerializeField] float shakeAngle = 0.35f;

        float _nextSpark;
        Vector3[] _camPos;
        Quaternion[] _camRot;

        void Awake()
        {
            int n = shakeCameras?.Length ?? 0;
            _camPos = new Vector3[n];
            _camRot = new Quaternion[n];
            for (int i = 0; i < n; i++)
                if (shakeCameras[i]) { _camPos[i] = shakeCameras[i].localPosition; _camRot[i] = shakeCameras[i].localRotation; }
            if (sparkLight) sparkLight.intensity = 0f;
            _nextSpark = Time.time + 0.6f;
        }

        void Update()
        {
            float t = Time.time;
            if (redLight) redLight.intensity = redIntensity * (0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(t * 3.2f)));

            if (t >= _nextSpark)
            {
                _nextSpark = t + Random.Range(sparkInterval.x, sparkInterval.y);
                if (sparks) sparks.Emit(Random.Range(10, 34));
                if (sparkLight) sparkLight.intensity = sparkFlash * Random.Range(0.6f, 1f);
            }
            if (sparkLight) sparkLight.intensity = Mathf.MoveTowards(sparkLight.intensity, 0f, Time.deltaTime * sparkFlash * 5f);

            float nx = Mathf.PerlinNoise(t * 0.7f, 0.1f) - 0.5f, ny = Mathf.PerlinNoise(0.3f, t * 0.6f) - 0.5f;
            float nr = Mathf.PerlinNoise(t * 0.5f, 7.7f) - 0.5f;
            for (int i = 0; i < _camPos.Length; i++)
            {
                var cam = shakeCameras[i];
                if (!cam) continue;
                cam.localPosition = _camPos[i] + new Vector3(nx, ny, 0f) * (2f * shakePosition);
                cam.localRotation = _camRot[i] * Quaternion.Euler(ny * 2f * shakeAngle, nx * 2f * shakeAngle, nr * 2f * shakeAngle);
            }
        }
    }
}
