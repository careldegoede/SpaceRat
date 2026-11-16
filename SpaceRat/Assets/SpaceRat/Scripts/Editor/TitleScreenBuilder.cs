using System.Collections.Generic;
using SpaceRat.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Builds Scene00_MainMenu: the Space Rat poster (Images/Space Rat - Unknown.png, prepared into
    /// Art/Title) on a blurred backdrop, a sci-fi menu panel with PLAY NOW, and the game icon.
    /// The scene is put first in the build settings. Safe to re-run.
    /// </summary>
    public static class TitleScreenBuilder
    {
        public const string ScenePath = "Assets/SpaceRat/Scenes/Scene00_MainMenu.unity";
        const string Art = "Assets/SpaceRat/Art/Title/";

        static readonly Color Cyan = new(0.45f, 0.85f, 1f);
        static readonly Color Gold = new(1f, 0.78f, 0.36f);

        [MenuItem("SpaceRat/Build Main Menu")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ImportArt();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;

            var canvasGo = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            var root = (RectTransform)canvasGo.transform;

            // Backdrop: blurred poster filling the screen (drifts slowly).
            var backdrop = Img("Backdrop", root, Spr("Backdrop"), Color.white);
            Stretch(backdrop.rectTransform, -60f);
            var fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            var stars = new GameObject("Stars", typeof(RectTransform)).GetComponent<RectTransform>();
            stars.SetParent(root, false);
            Stretch(stars, 0f);

            // Poster, full height, centred (slightly left so the menu corner stays clear).
            var posterSprite = Spr("Poster");
            var poster = Img("Poster", root, posterSprite, Color.white);
            poster.preserveAspect = true;
            var prt = poster.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            float ph = 1080f, pw = ph * posterSprite.rect.width / posterSprite.rect.height;
            prt.sizeDelta = new Vector2(pw, ph);
            prt.anchoredPosition = new Vector2(-110f, 0f);

            // ---- Menu panel (compact, bottom-right corner) --------------------------------
            var panel = Img("MenuPanel", root, null, new Color(0.02f, 0.04f, 0.09f, 0.55f));
            var mrt = panel.rectTransform;
            mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(1f, 0f);
            mrt.sizeDelta = new Vector2(500f, 300f);
            mrt.anchoredPosition = new Vector2(-48f, 72f);
            CornerBrackets(mrt, 26f, 3f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f));

            var header = Txt("Header", mrt, "SPARROW  //  FLIGHT SYSTEMS", "Orbitron", 19, new Color(0.75f, 0.88f, 1f), TextAnchor.MiddleLeft);
            Place(header.rectTransform, new Vector2(34f, -22f), new Vector2(432f, 30f));
            var rule = Img("HeaderRule", mrt, null, new Color(Gold.r, Gold.g, Gold.b, 0.85f));
            Place(rule.rectTransform, new Vector2(34f, -54f), new Vector2(432f, 2f));

            var button = Img("PlayNow", mrt, Spr("ButtonSelected"), Color.white);
            button.type = Image.Type.Sliced;
            button.raycastTarget = false;
            Place(button.rectTransform, new Vector2(40f, -72f), new Vector2(420f, 92f));
            var label = Txt("Label", button.rectTransform, "PLAY NOW", "Orbitron", 36, Gold, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 0f);
            label.fontStyle = FontStyle.Bold;

            var prompt = Txt("Prompt", mrt, "PRESS ENTER", "Rajdhani-SemiBold", 21, Color.white, TextAnchor.MiddleCenter);
            Place(prompt.rectTransform, new Vector2(40f, -170f), new Vector2(420f, 26f));

            var quip = Txt("Quip", mrt, "", "Rajdhani-Medium", 22, Cyan, TextAnchor.UpperLeft);
            Place(quip.rectTransform, new Vector2(34f, -210f), new Vector2(432f, 76f));
            quip.horizontalOverflow = HorizontalWrapMode.Wrap;

            var version = Txt("Version", root, "PROTOTYPE  ·  ESC TO QUIT", "Rajdhani-Medium", 20, new Color(0.55f, 0.62f, 0.75f, 0.8f), TextAnchor.LowerRight);
            var vrt = version.rectTransform;
            vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(1f, 0f);
            vrt.sizeDelta = new Vector2(600f, 30f);
            vrt.anchoredPosition = new Vector2(-40f, 28f);

            var fade = Img("Fade", root, null, Color.black);
            Stretch(fade.rectTransform, 0f);

            var title = canvasGo.AddComponent<TitleScreen>();
            var so = new SerializedObject(title);
            so.FindProperty("poster").objectReferenceValue = prt;
            so.FindProperty("backdrop").objectReferenceValue = backdrop.rectTransform;
            var b = so.FindProperty("buttons"); b.arraySize = 1; b.GetArrayElementAtIndex(0).objectReferenceValue = button;
            var l = so.FindProperty("labels"); l.arraySize = 1; l.GetArrayElementAtIndex(0).objectReferenceValue = label;
            so.FindProperty("buttonIdle").objectReferenceValue = Spr("ButtonIdle");
            so.FindProperty("buttonSelected").objectReferenceValue = Spr("ButtonSelected");
            so.FindProperty("quip").objectReferenceValue = quip;
            so.FindProperty("prompt").objectReferenceValue = prompt;
            so.FindProperty("fade").objectReferenceValue = fade;
            so.FindProperty("starsRoot").objectReferenceValue = stars;
            so.FindProperty("starDot").objectReferenceValue = Spr("StarDot");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            PutFirstInBuild();
            SetIcon();
            Debug.Log("[SpaceRat] Main menu built: " + ScenePath);
        }

        // ---- Art import -----------------------------------------------------------------

        static void ImportArt()
        {
            foreach (var name in new[] { "Poster", "Backdrop", "StarDot", "ButtonIdle", "ButtonSelected" })
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(Art + name + ".png");
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.maxTextureSize = 2048;
                imp.textureCompression = name == "Poster" ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
                if (name.StartsWith("Button")) imp.spriteBorder = new Vector4(44f, 0f, 44f, 0f);
                imp.SaveAndReimport();
            }
            var icon = (TextureImporter)AssetImporter.GetAtPath(Art + "Icon.png");
            icon.textureType = TextureImporterType.Default;
            icon.mipmapEnabled = false;
            icon.textureCompression = TextureImporterCompression.Uncompressed;
            icon.SaveAndReimport();
        }

        static void SetIcon()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Icon.png");
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);
        }

        static void PutFirstInBuild()
        {
            var scenes = new List<EditorBuildSettingsScene> { new(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---- UI helpers -----------------------------------------------------------------

        static Sprite Spr(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");

        static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text Txt(string name, Transform parent, string text, string font, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = Resources.Load<Font>("Fonts/" + font);
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Top-left anchored placement inside the parent (y goes down).</summary>
        static void Place(RectTransform rt, Vector2 topLeft, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, float outset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-outset, -outset);
            rt.offsetMax = new Vector2(outset, outset);
        }

        static void CornerBrackets(RectTransform parent, float len, float thick, Color c)
        {
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i % 2, i / 2);
                for (int k = 0; k < 2; k++)
                {
                    var img = Img($"Bracket{i}{k}", parent, null, c);
                    var rt = img.rectTransform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = corner;
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = k == 0 ? new Vector2(len, thick) : new Vector2(thick, len);
                }
            }
        }
    }
}
