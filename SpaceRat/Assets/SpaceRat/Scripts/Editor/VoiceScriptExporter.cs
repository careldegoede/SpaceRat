using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SpaceRat.Dialogue;
using UnityEditor;
using UnityEngine;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Regenerates the "In-game lines" section of Docs/Voice.md from every DialogueSequence asset,
    /// so the voice script never drifts from what the game actually plays.
    /// Everything outside the BEGIN/END GENERATED markers (voice direction, planned lines) is kept.
    /// Menu: SpaceRat > Export Voice Script
    /// </summary>
    public static class VoiceScriptExporter
    {
        const string DocPath = "Docs/Voice.md";   // relative to the Unity project root
        const string Begin = "<!-- BEGIN GENERATED: in-game lines (SpaceRat > Export Voice Script) -->";
        const string End = "<!-- END GENERATED -->";

        [MenuItem("SpaceRat/Export Voice Script")]
        public static void Export()
        {
            var seqs = AssetDatabase.FindAssets("t:DialogueSequence", new[] { "Assets/SpaceRat" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(p => (path: p, seq: AssetDatabase.LoadAssetAtPath<DialogueSequence>(p)))
                .Where(x => x.seq)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine(Begin);
            sb.AppendLine();
            sb.AppendLine($"_Generated from {seqs.Count} DialogueSequence assets. Do not edit by hand — edit the assets, then re-export._");
            sb.AppendLine();
            var counts = new Dictionary<Speaker, int>();
            foreach (var (path, seq) in seqs)
            {
                string scene = SceneTag(path);
                string beat = Path.GetFileNameWithoutExtension(path);
                sb.AppendLine($"### {scene} · {beat}");
                sb.AppendLine();
                sb.AppendLine("| ID | Speaker | Line | Delivery | File |");
                sb.AppendLine("|---|---|---|---|---|");
                for (int i = 0; i < seq.lines.Count; i++)
                {
                    var l = seq.lines[i];
                    counts[l.speaker] = counts.TryGetValue(l.speaker, out var c) ? c + 1 : 1;
                    string id = $"{scene}_{beat}_{i + 1:00}";
                    string file = $"{id}_{l.speaker.ToString().ToLowerInvariant()}.wav";
                    sb.AppendLine($"| `{id}` | {l.speaker} | {Escape(l.text)} | {Delivery(l)} | `{file}` |");
                }
                sb.AppendLine();
            }
            sb.AppendLine("**Totals:** " + string.Join(", ", counts.Select(kv => $"{kv.Key} {kv.Value}")));
            sb.AppendLine();
            sb.AppendLine(End);

            string full = Path.GetFullPath(DocPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            string doc = File.Exists(full) ? File.ReadAllText(full) : "";
            int b = doc.IndexOf(Begin);
            int e = doc.IndexOf(End);
            doc = b >= 0 && e > b
                ? doc.Substring(0, b) + sb.ToString().TrimEnd() + doc.Substring(e + End.Length)
                : doc.TrimEnd() + "\n\n## In-game lines\n\n" + sb;
            File.WriteAllText(full, doc.Replace("\r\n", "\n"));
            Debug.Log($"[SpaceRat] Voice script exported: {full}");
        }

        const string VoiceDir = "Assets/SpaceRat/Audio/Voices";

        /// <summary>
        /// Assigns each DialogueLine.voice from Audio/Voices by the same file name the voice script lists
        /// (generate the WAVs with Audio/Kokoro/generate_voices.py first). Lines without a file are cleared.
        /// </summary>
        [MenuItem("SpaceRat/Link Voice Clips")]
        public static void LinkVoiceClips()
        {
            int linked = 0, missing = 0;
            foreach (var path in AssetDatabase.FindAssets("t:DialogueSequence", new[] { "Assets/SpaceRat" })
                         .Select(AssetDatabase.GUIDToAssetPath))
            {
                var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
                if (!seq) continue;
                string scene = SceneTag(path), beat = Path.GetFileNameWithoutExtension(path);
                bool dirty = false;
                for (int i = 0; i < seq.lines.Count; i++)
                {
                    var l = seq.lines[i];
                    string file = $"{VoiceDir}/{scene}_{beat}_{i + 1:00}_{l.speaker.ToString().ToLowerInvariant()}.wav";
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(file);
                    if (clip) linked++;
                    else { missing++; Debug.LogWarning($"[SpaceRat] No voice clip for {file}"); }
                    if (l.voice != clip) { l.voice = clip; dirty = true; }
                }
                if (dirty) EditorUtility.SetDirty(seq);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpaceRat] Voice clips linked: {linked}, missing: {missing}");
        }

        static string SceneTag(string path)
        {
            // Assets/SpaceRat/ScriptableObjects/Dialogue[/SceneXX]/... -> S01 by default
            var dir = Path.GetDirectoryName(path)!.Replace('\\', '/');
            var last = dir.Substring(dir.LastIndexOf('/') + 1);
            if (last.StartsWith("Scene") && last.Length >= 7) return "S" + last.Substring(5, 2);
            return "S01";
        }

        static string Delivery(DialogueLine l)
        {
            var t = l.text;
            if (t.EndsWith("!")) return "urgent";
            if (t.Contains("...")) return "hesitant";
            if (l.speaker == Speaker.Computer) return "deadpan";
            return "dry";
        }

        static string Escape(string s) => (s ?? "").Replace("|", "\\|").Replace("\n", " ");
    }
}
