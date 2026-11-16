using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpaceRat.UI
{
    /// <summary>
    /// Main menu: poster, a sci-fi menu panel (PLAY NOW, CHAPTERS) and a deadpan line from the
    /// ship's computer. CHAPTERS swaps the panel for a chapter list that loads any scene directly
    /// (for play-testing). Keyboard (arrows/W/S, Enter/Space, Esc = back / quit), mouse and gamepad.
    /// No EventSystem needed.
    /// All elements are created by TitleScreenBuilder and only driven from here.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [SerializeField] RectTransform poster;
        [SerializeField] RectTransform backdrop;
        [System.Serializable]
        public class MenuButton
        {
            public Image image;
            public Text label;
            [Tooltip("play, chapters, back, quit, or scene:<SceneName>")]
            public string action;
        }

        [SerializeField] GameObject mainPanel;
        [SerializeField] MenuButton[] mainButtons;
        [SerializeField] GameObject chaptersPanel;
        [SerializeField] MenuButton[] chapterButtons;
        [SerializeField] Sprite buttonIdle;
        [SerializeField] Sprite buttonSelected;
        [SerializeField] Text quip;
        [SerializeField] Text[] prompts;
        [SerializeField] Image fade;
        [SerializeField] RectTransform starsRoot;
        [SerializeField] Sprite starDot;
        [SerializeField] string firstScene = "Scene01_Awakening";
        [SerializeField] int starCount = 70;

        static readonly Color Gold = new(1f, 0.78f, 0.36f);
        static readonly Color Pale = new(0.72f, 0.8f, 0.92f);

        static readonly string[] Quips =
        {
            "Ship status: critical. Pilot status: unknown. Morale: not measured.",
            "Press Enter to begin. I would do it myself, but I do not have hands.",
            "All systems nominal. That was a lie. Most systems are on fire.",
            "Identity unknown. Location unknown. Snack inventory: also unknown.",
            "Welcome back, pilot. Statistically, you will be fine. The statistics are made up.",
        };

        int _selected;
        bool _leaving;
        Vector2 _lastMouse;
        MenuButton[] _buttons;
        Image[] _stars;
        float[] _starPhase, _starSpeed;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (quip) quip.text = $"COMPUTER:  “{Quips[Random.Range(0, Quips.Length)]}”";
            BuildStars();
            ShowPanel(false);
            if (Mouse.current != null) _lastMouse = Mouse.current.position.ReadValue();
            StartCoroutine(FadeTo(0f, 1.6f));
        }

        void BuildStars()
        {
            if (!starsRoot || !starDot) return;
            _stars = new Image[starCount];
            _starPhase = new float[starCount];
            _starSpeed = new float[starCount];
            var size = starsRoot.rect.size;
            for (int i = 0; i < starCount; i++)
            {
                var go = new GameObject("Star", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(starsRoot, false);
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.anchoredPosition = new Vector2(Random.value * size.x, Random.value * size.y);
                float d = Random.Range(6f, 18f);
                rt.sizeDelta = new Vector2(d, d);
                var img = go.GetComponent<Image>();
                img.sprite = starDot;
                img.raycastTarget = false;
                img.color = Color.Lerp(Color.white, new Color(0.6f, 0.75f, 1f), Random.value);
                _stars[i] = img;
                _starPhase[i] = Random.value * 10f;
                _starSpeed[i] = Random.Range(0.4f, 1.6f);
            }
        }

        void Update()
        {
            float t = Time.time;

            // Slow push-in on the poster, gentle drift on the blurred backdrop.
            if (poster) poster.localScale = Vector3.one * (1f + 0.025f * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / 30f)));
            if (backdrop) backdrop.anchoredPosition = new Vector2(Mathf.Sin(t * 0.05f) * 30f, Mathf.Cos(t * 0.04f) * 14f);

            if (_stars != null)
                for (int i = 0; i < _stars.Length; i++)
                {
                    var c = _stars[i].color;
                    float tw = 0.5f + 0.5f * Mathf.Sin(_starPhase[i] + t * _starSpeed[i] * 2f);
                    c.a = 0.1f + 0.8f * tw * tw;
                    _stars[i].color = c;
                }

            if (prompts != null)
                foreach (var pr in prompts)
                    if (pr) pr.color = new Color(Pale.r, Pale.g, Pale.b, 0.45f + 0.35f * Mathf.Sin(t * 2.4f));

            if (_leaving) return;
            HandleInput();
        }

        void HandleInput()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            var mouse = Mouse.current;

            bool up = kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame);
            bool down = kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame);
            bool confirm = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
            if (pad != null)
            {
                up |= pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame;
                down |= pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame;
                confirm |= pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame;
            }

            int n = _buttons.Length;
            if (up) Select((_selected + n - 1) % n);
            if (down) Select((_selected + 1) % n);

            if (mouse != null)
            {
                var pos = mouse.position.ReadValue();
                int hover = -1;
                for (int i = 0; i < n; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(_buttons[i].image.rectTransform, pos, null)) hover = i;
                if (hover >= 0 && (pos - _lastMouse).sqrMagnitude > 1f) Select(hover);
                if (hover >= 0 && mouse.leftButton.wasPressedThisFrame) { Select(hover); confirm = true; }
                _lastMouse = pos;
            }

            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (confirm) Activate(_selected);
            else if (back && _buttons == chapterButtons) ShowPanel(false);
            else if (back && kb != null && kb.escapeKey.wasPressedThisFrame) { _leaving = true; StartCoroutine(Leave(Quit, false)); }
        }

        /// <summary>Main menu or chapter list.</summary>
        void ShowPanel(bool chapters)
        {
            if (mainPanel) mainPanel.SetActive(!chapters);
            if (chaptersPanel) chaptersPanel.SetActive(chapters);
            _buttons = chapters ? chapterButtons : mainButtons;
            Select(0);
        }

        void Select(int index)
        {
            _selected = index;
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool on = i == index;
                _buttons[i].image.sprite = on ? buttonSelected : buttonIdle;
                if (_buttons[i].label) _buttons[i].label.color = on ? Gold : Pale;
            }
        }

        /// <summary>Starts the game (same as choosing PLAY NOW).</summary>
        public void PlayNow()
        {
            if (!_leaving) Run("play");
        }

        /// <summary>Loads a chapter's scene by name (same as picking it in CHAPTERS).</summary>
        public void PlayChapter(string sceneName)
        {
            if (!_leaving) Run("scene:" + sceneName);
        }

        void Activate(int index) => Run(_buttons[index].action);

        void Run(string action)
        {
            switch (action)
            {
                case "chapters": ShowPanel(true); return;
                case "back": ShowPanel(false); return;
                case "quit": _leaving = true; StartCoroutine(Leave(Quit)); return;
                case "play": _leaving = true; StartCoroutine(Leave(() => SceneManager.LoadScene(firstScene))); return;
            }
            if (action != null && action.StartsWith("scene:"))
            {
                string scene = action.Substring(6);
                if (!Application.CanStreamedLevelBeLoaded(scene)) { Debug.LogWarning($"[SpaceRat] Chapter scene not in build: {scene}"); return; }
                _leaving = true;
                StartCoroutine(Leave(() => SceneManager.LoadScene(scene)));
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        IEnumerator Leave(System.Action then, bool flash = true)
        {
            // Quick gold flash on the chosen button, then fade to black.
            var label = _buttons[_selected].label;
            for (int i = 0; flash && label && i < 3; i++)
            {
                label.color = Color.white; yield return new WaitForSeconds(0.06f);
                label.color = Gold; yield return new WaitForSeconds(0.06f);
            }
            yield return FadeTo(1f, 0.9f);
            then();
        }

        IEnumerator FadeTo(float target, float seconds)
        {
            if (!fade) yield break;
            float from = fade.color.a;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            fade.color = new Color(0f, 0f, 0f, target);
        }
    }
}
