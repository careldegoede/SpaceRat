using UnityEngine;

namespace SpaceRat.Environment
{
    /// <summary>
    /// Put on a fence root (e.g. the FenceLayout object) to switch the alarm on every
    /// <see cref="FenceAlarm"/> piece underneath it at once. Can be toggled from the Inspector in play mode,
    /// or from code / UnityEvents via <see cref="SetAlarm"/>.
    /// </summary>
    public class FenceAlarmController : MonoBehaviour
    {
        [SerializeField] bool alarm;

        bool _applied;

        public bool Alarm => alarm;

        public void SetAlarm(bool on)
        {
            alarm = on;
            Apply();
        }

        public void ToggleAlarm() => SetAlarm(!alarm);

        void Start() => Apply();

        void OnValidate()
        {
            if (Application.isPlaying) Apply();
        }

        void Apply()
        {
            foreach (var piece in GetComponentsInChildren<FenceAlarm>(true)) piece.Alarm = alarm;
            _applied = alarm;
        }

        void Update()
        {
            if (_applied != alarm) Apply();
        }
    }
}
