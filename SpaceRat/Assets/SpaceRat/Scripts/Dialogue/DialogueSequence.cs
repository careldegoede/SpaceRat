using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceRat.Dialogue
{
    public enum Speaker
    {
        Computer,
        Pilot
    }

    [Serializable]
    public class DialogueLine
    {
        public Speaker speaker = Speaker.Computer;
        [TextArea(1, 3)] public string text;
        [Tooltip("Seconds the line stays on screen before auto-advancing.")]
        [Min(0.5f)] public float duration = 3f;
    }

    /// <summary>Ordered, auto-timed lines for one beat of a scene.</summary>
    [CreateAssetMenu(menuName = "SpaceRat/Dialogue Sequence", fileName = "DialogueSequence")]
    public class DialogueSequence : ScriptableObject
    {
        public List<DialogueLine> lines = new();
    }
}
