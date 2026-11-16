using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceRat.Dialogue
{
    public enum Speaker
    {
        Computer,
        Pilot,
        /// <summary>Captain Voss, heard only through recorded station logs (Scene 3).</summary>
        Captain
    }

    [Serializable]
    public class DialogueLine
    {
        public Speaker speaker = Speaker.Computer;
        [TextArea(1, 3)] public string text;
        [Tooltip("Seconds the line stays on screen before auto-advancing.")]
        [Min(0.5f)] public float duration = 3f;
        [Tooltip("Voice-over clip. Filled by SpaceRat > Link Voice Clips.")]
        public AudioClip voice;

        /// <summary>How long the line holds: never shorter than its voice clip, so audio always completes.</summary>
        public float Seconds => voice ? Mathf.Max(duration, voice.length + 0.25f) : duration;
    }

    /// <summary>Ordered, auto-timed lines for one beat of a scene.</summary>
    [CreateAssetMenu(menuName = "SpaceRat/Dialogue Sequence", fileName = "DialogueSequence")]
    public class DialogueSequence : ScriptableObject
    {
        public List<DialogueLine> lines = new();
    }
}
