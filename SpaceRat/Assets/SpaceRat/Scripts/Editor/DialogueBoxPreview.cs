using System.IO;
using System.Reflection;
using SpaceRat.Dialogue;
using UnityEditor;
using UnityEngine;

namespace SpaceRat.EditorTools
{
    /// <summary>Edit-mode preview of the DialogueBox styles (SpaceRat > Dialogue Box Preview).</summary>
    public class DialogueBoxPreview : EditorWindow
    {
        DialogueBoxStyle _style;
        Speaker _speaker;
        string _text = "We are on a collision course with a star. Red and hot are its main features.";
        string _status = "DIST 1.2 KM  ·  SIGNAL 74%";
        DialogueBox _box;
        SpaceRat.UI.CompassHud _compass;
        static readonly System.Collections.Generic.Queue<(DialogueBoxStyle, Speaker, string, string)> Pending = new();
        [System.NonSerialized] string _pendingCapture;

        [MenuItem("SpaceRat/Dialogue Box Preview")]
        static void Open() => GetWindow<DialogueBoxPreview>("Dialogue Box").minSize = new Vector2(640, 360);

        /// <summary>Renders one style/speaker into a PNG (used for automated checks).</summary>
        public static void Capture(DialogueBoxStyle style, Speaker speaker, string text, string pngPath)
        {
            Pending.Enqueue((style, speaker, text, pngPath));
            GetWindow<DialogueBoxPreview>("Dialogue Box").Repaint();
        }

        void OnDisable()
        {
            if (_box) DestroyImmediate(_box.gameObject);
            if (_compass) DestroyImmediate(_compass.gameObject);
        }

        /// <summary>Comset preview also shows the compass: viewer facing north-ish, station to the north-east.</summary>
        void EnsureCompass()
        {
            if (_compass) return;
            var root = new GameObject("CompassPreview") { hideFlags = HideFlags.HideAndDontSave };
            var viewer = new GameObject("Viewer") { hideFlags = HideFlags.HideAndDontSave }.transform;
            viewer.SetParent(root.transform);
            viewer.rotation = Quaternion.Euler(0f, 20f, 0f);
            var target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave }.transform;
            target.SetParent(root.transform);
            target.position = new Vector3(850f, 0f, 850f);
            _compass = root.AddComponent<SpaceRat.UI.CompassHud>();
            var so = new SerializedObject(_compass);
            so.FindProperty("dial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SpaceRat/Art/HUD/CompassDial.png");
            so.FindProperty("panel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SpaceRat/Art/HUD/CompassPanel.png");
            so.FindProperty("viewer").objectReferenceValue = viewer;
            so.FindProperty("target").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
            _compass.ShowImmediately();
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(_pendingCapture) && Pending.Count > 0 && Event.current.type == EventType.Layout)
            {
                (_style, _speaker, _text, _pendingCapture) = Pending.Dequeue();
            }
            if (!_box)
            {
                var go = new GameObject("DialogueBoxPreview") { hideFlags = HideFlags.HideAndDontSave };
                _box = go.AddComponent<DialogueBox>();
            }
            EnsureCompass();
            _box.Style = _style;
            _box.comsetStatus = _status;
            _box.Show(_speaker, _text);
            typeof(DialogueBox).GetField("_shownAt", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(_box, -100f);

            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.02f, 0.02f, 0.05f));
            _box.Draw(position.width, position.height);
            if (_style == DialogueBoxStyle.Comset) _compass.Draw(position.width, position.height);

            GUILayout.BeginArea(new Rect(8, 8, 420, 140));
            _style = (DialogueBoxStyle)EditorGUILayout.EnumPopup("Style", _style);
            _speaker = (Speaker)EditorGUILayout.EnumPopup("Speaker", _speaker);
            _text = EditorGUILayout.TextField("Text", _text);
            _status = EditorGUILayout.TextField("Comset status", _status);
            GUILayout.EndArea();

            if (!string.IsNullOrEmpty(_pendingCapture) && Event.current.type == EventType.Repaint)
            {
                var path = _pendingCapture;
                _pendingCapture = null;
                // Offscreen: redraw the box into a RenderTexture (never reads the desktop).
                int w = Mathf.Min(1280, (int)position.width), h = w * 9 / 16;
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var prevRt = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Clear(true, true, new Color(0.02f, 0.02f, 0.05f));
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, w, h, 0);
                _box.Draw(w, h);
                if (_style == DialogueBoxStyle.Comset) _compass.Draw(w, h);
                GL.PopMatrix();
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prevRt;
                RenderTexture.ReleaseTemporary(rt);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                DestroyImmediate(tex);
                Debug.Log("[SpaceRat] Dialogue box preview saved: " + path);
                if (Pending.Count > 0) Repaint();
            }
        }
    }
}
