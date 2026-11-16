using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace SpaceRat.Scene01
{
    /// <summary>
    /// Alarm light, alarm audio and camera shake, all driven by one 0..1 Intensity.
    /// The alarm sound is generated procedurally so the greybox needs no audio assets.
    /// </summary>
    public class ShipEmergencyEffects : MonoBehaviour
    {
        [SerializeField] Light alarmLight;
        [SerializeField] AudioSource alarmSource;
        [SerializeField] List<CinemachineBasicMultiChannelPerlin> shakers = new();

        [Header("Tuning")]
        [SerializeField] float maxLightIntensity = 60f;
        [SerializeField] float flashRateMin = 0.8f;
        [SerializeField] float flashRateMax = 2.2f;
        [SerializeField] float maxShake = 1.6f;
        [SerializeField] float maxVolume = 0.15f;
        [Tooltip("How fast Intensity eases toward its target (per second).")]
        [SerializeField] float easeSpeed = 0.5f;

        float _intensity;
        float _target;
        float _phase;

        public float Intensity => _intensity;

        /// <summary>Silences the alarm beep while keeping the light and shake (Scene 2 before the malfunction).</summary>
        public bool AlarmMuted { get; set; }

        public void SetTarget(float value) => _target = Mathf.Clamp01(value);

        public void SetImmediate(float value) => _intensity = _target = Mathf.Clamp01(value);

        void Awake()
        {
            if (alarmSource && !alarmSource.clip)
            {
                alarmSource.clip = BuildAlarmClip();
                alarmSource.loop = true;
                alarmSource.playOnAwake = false;
                alarmSource.spatialBlend = 0f;
            }
        }

        void Update()
        {
            _intensity = Mathf.MoveTowards(_intensity, _target, easeSpeed * Time.deltaTime);

            _phase += Mathf.Lerp(flashRateMin, flashRateMax, _intensity) * Time.deltaTime;
            float pulse = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(_phase * Mathf.PI * 2f), 3f);

            if (alarmLight)
                alarmLight.intensity = pulse * maxLightIntensity * _intensity;

            if (alarmSource)
            {
                bool audible = !AlarmMuted && _intensity > 0.01f;
                alarmSource.volume = maxVolume * _intensity;
                if (audible && !alarmSource.isPlaying) alarmSource.Play();
                else if (!audible && alarmSource.isPlaying) alarmSource.Stop();
            }

            foreach (var s in shakers)
                if (s) s.AmplitudeGain = maxShake * _intensity;
        }

        static AudioClip BuildAlarmClip()
        {
            const int rate = 44100;
            const float length = 1f;
            int samples = Mathf.RoundToInt(rate * length);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                // Two-tone klaxon: 0.0-0.35s high, 0.5-0.85s low, silence between.
                float freq, env;
                if (t < 0.35f) { freq = 880f; env = Envelope(t, 0.35f); }
                else if (t >= 0.5f && t < 0.85f) { freq = 660f; env = Envelope(t - 0.5f, 0.35f); }
                else { data[i] = 0f; continue; }
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t));
                float sine = Mathf.Sin(2f * Mathf.PI * freq * t);
                data[i] = (0.35f * sq + 0.65f * sine) * env * 0.8f;
            }
            var clip = AudioClip.Create("ProceduralAlarm", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Envelope(float t, float len)
        {
            const float attack = 0.01f, release = 0.04f;
            if (t < attack) return t / attack;
            if (t > len - release) return Mathf.Clamp01((len - t) / release);
            return 1f;
        }
    }
}
