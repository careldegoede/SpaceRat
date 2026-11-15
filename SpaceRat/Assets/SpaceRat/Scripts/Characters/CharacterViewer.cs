using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace SpaceRat.Characters
{
    /// <summary>
    /// Look-dev viewer for humanoid characters.
    /// Orbit: hold left/right mouse + drag · Zoom: scroll · F: first-person (eyes) view ·
    /// C: cycle character variants · R: reset view.
    /// In first person the head meshes switch to shadows-only, so you see your own body.
    /// </summary>
    public class CharacterViewer : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] List<GameObject> variants = new();
        [Tooltip("Animator state names played by number keys 1-9 (empty = no animations).")]
        [SerializeField] string[] animStates = new string[0];
        [Tooltip("Variant shown first (the pilot's default outfit).")]
        [SerializeField] int startIndex;

        [Header("Orbit")]
        [SerializeField] float distance = 3.2f;
        [SerializeField] Vector2 distanceRange = new(1.2f, 8f);
        [SerializeField] float yaw = 200f;
        [SerializeField] float pitch = 8f;
        [SerializeField] float orbitSpeed = 0.25f;
        [SerializeField] float targetHeight = 1.1f;

        [Header("First person")]
        [Tooltip("Eye offset in front of the head bone. Must clear the face/chest surface, or you look out from inside the mesh.")]
        [SerializeField] float eyeForward = 0.09f;
        [SerializeField] float eyeUp = 0.06f;
        [Tooltip("Wider lens in first person so you can see your own chest and legs.")]
        [SerializeField] float firstPersonFov = 78f;
        [Tooltip("Renderer names containing any of these are hidden (shadows only) in first person.")]
        [SerializeField] string[] headMeshKeywords = { "Head", "Jaw", "Eyes", "Balaclava", "Face" };

        int _index;
        int _anim;
        bool _firstPerson;
        float _fpYaw, _fpPitch;
        float _orbitFov;
        Transform _shrunkHead;
        Vector3 _headScale;

        GameObject Current => variants.Count > 0 ? variants[_index] : null;

        void Start()
        {
            if (cam) _orbitFov = cam.fieldOfView;
            _index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, variants.Count - 1));
            for (int i = 0; i < variants.Count; i++) variants[i].SetActive(i == _index);
        }

        void LateUpdate()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null)
            {
                if (kb.fKey.wasPressedThisFrame) SetFirstPerson(!_firstPerson);
                if (kb.cKey.wasPressedThisFrame) Cycle();
                for (int i = 0; i < animStates.Length && i < 9; i++)
                    if (kb[(UnityEngine.InputSystem.Key)((int)UnityEngine.InputSystem.Key.Digit1 + i)].wasPressedThisFrame)
                        PlayAnim(i);
                if (kb.rKey.wasPressedThisFrame) { yaw = 200f; pitch = 8f; distance = 3.2f; _fpYaw = _fpPitch = 0f; }
            }

            Vector2 drag = Vector2.zero;
            float scroll = 0f;
            if (mouse != null)
            {
                if (mouse.leftButton.isPressed || mouse.rightButton.isPressed) drag = mouse.delta.ReadValue();
                scroll = mouse.scroll.ReadValue().y;
            }

            var current = Current;
            if (!current || !cam) return;

            if (_firstPerson)
            {
                var head = Head(current);
                if (!head) { SetFirstPerson(false); return; }
                _fpYaw = Mathf.Clamp(_fpYaw + drag.x * orbitSpeed, -100f, 100f);
                _fpPitch = Mathf.Clamp(_fpPitch - drag.y * orbitSpeed, -70f, 85f);
                // Body-relative look so you can glance down at your own chest and hands.
                Quaternion bodyRot = Quaternion.Euler(0f, current.transform.eulerAngles.y, 0f);
                Quaternion look = bodyRot * Quaternion.Euler(_fpPitch, _fpYaw, 0f);
                cam.transform.SetPositionAndRotation(
                    head.position + bodyRot * new Vector3(0f, eyeUp, eyeForward), look);
                cam.nearClipPlane = 0.03f;
            }
            else
            {
                yaw += drag.x * orbitSpeed;
                pitch = Mathf.Clamp(pitch - drag.y * orbitSpeed, -20f, 75f);
                distance = Mathf.Clamp(distance - scroll * 0.002f * distance, distanceRange.x, distanceRange.y);
                Vector3 target = current.transform.position + Vector3.up * targetHeight;
                Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
                cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
                cam.nearClipPlane = 0.1f;
            }
        }

        public void PlayAnim(int i)
        {
            if (i < 0 || i >= animStates.Length) return;
            _anim = i;
            var anim = Current ? Current.GetComponentInChildren<Animator>() : null;
            if (anim && anim.runtimeAnimatorController) anim.CrossFadeInFixedTime(animStates[i], 0.2f);
        }

        void Cycle()
        {
            if (variants.Count < 2) return;
            bool fp = _firstPerson;
            SetFirstPerson(false);
            variants[_index].SetActive(false);
            _index = (_index + 1) % variants.Count;
            variants[_index].SetActive(true);
            if (animStates.Length > 0) PlayAnim(_anim);
            SetFirstPerson(fp);
        }

        /// <summary>Sets the view angles: orbit yaw/pitch, or look yaw/pitch in first person.</summary>
        public void SetLook(float lookYaw, float lookPitch)
        {
            if (_firstPerson) { _fpYaw = lookYaw; _fpPitch = lookPitch; }
            else { yaw = lookYaw; pitch = lookPitch; }
        }

        public void SetFirstPerson(bool on)
        {
            _firstPerson = on;
            _fpYaw = _fpPitch = 0f;
            if (cam) cam.fieldOfView = on ? firstPersonFov : (_orbitFov > 0f ? _orbitFov : cam.fieldOfView);

            // Collapse the head bone in first person (head may be part of the body mesh), restore after.
            if (_shrunkHead) { _shrunkHead.localScale = _headScale; _shrunkHead = null; }
            var current = Current;
            if (!current) return;
            if (on)
            {
                var head = Head(current);
                if (head) { _shrunkHead = head; _headScale = head.localScale; head.localScale = Vector3.one * 0.001f; }
            }
            foreach (var r in current.GetComponentsInChildren<Renderer>(true))
            {
                bool isHead = false;
                foreach (var k in headMeshKeywords)
                    if (r.name.Contains(k)) { isHead = true; break; }
                if (isHead)
                    r.shadowCastingMode = on ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            }
        }

        static Transform Head(GameObject go)
        {
            var anim = go.GetComponentInChildren<Animator>();
            return anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        }

        void OnGUI()
        {
            GUI.Label(new Rect(16, 12, 900, 24),
                $"{(Current ? Current.name : "-")}{(animStates.Length > 0 ? "  [" + animStates[_anim] + "]  1-" + Mathf.Min(9, animStates.Length) + ": anims" : "")}   ·   drag: look/orbit   ·   scroll: zoom   ·   F: {(_firstPerson ? "orbit view" : "first person")}   ·   C: next outfit   ·   R: reset");
        }
    }
}
