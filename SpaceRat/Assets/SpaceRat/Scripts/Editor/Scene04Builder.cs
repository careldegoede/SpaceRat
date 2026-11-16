using System.Collections.Generic;
using SpaceRat.Dialogue;
using SpaceRat.Scene03;
using SpaceRat.Scene04;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using B = SpaceRat.EditorTools.Scene01GreyboxBuilder;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Scene 4 "The Search" (menu: SpaceRat > Build Scene04). Copies Scene 3 (island, outpost, wreck), removes the
    /// cockpit opening, puts the pilot at the foot of the roof staircase, and swaps in <see cref="Scene04Director"/>:
    /// the bot (with the navigation module) flies to the ship while the pilot starts the search for Captain Voss.
    /// The outpost and its doors are untouched, so the pilot can still enter and exit.
    /// </summary>
    public static class Scene04Builder
    {
        const string Src = B.Root + "/Scenes/Scene03_Terrain.unity";
        const string Dst = B.Root + "/Scenes/Scene04_TheSearch.unity";

        static (Speaker, string, float) C(string t) => (Speaker.Computer, t, Dur(t));
        static (Speaker, string, float) P(string t) => (Speaker.Pilot, t, Dur(t));
        static float Dur(string t) => Mathf.Clamp(1.1f + t.Split(' ').Length * 0.3f, 1.6f, 4.8f);

        [MenuItem("SpaceRat/Build Scene04")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.CopyAsset(Src, Dst) && !AssetDatabase.LoadMainAssetAtPath(Dst))
            { Debug.LogError("[SpaceRat] Could not copy " + Src); return; }
            var scene = EditorSceneManager.OpenScene(Dst);

            // ---- Remove the Scene 3 opening / scripted flow ------------------------------------------
            var old = Object.FindAnyObjectByType<Scene03Director>();
            var sw = Object.FindAnyObjectByType<SpaceRat.Characters.FirstPersonWalker>();
            if (!old || !sw) { Debug.LogError("[SpaceRat] Scene03Director / pilot not found in the copy."); return; }
            var dso = new SerializedObject(old);
            foreach (var f in new[] { "cockpitSet", "overlay" })
            {
                var o = dso.FindProperty(f).objectReferenceValue as Component;
                var g = o ? o.gameObject : dso.FindProperty(f).objectReferenceValue as GameObject;
                if (g) Object.DestroyImmediate(g);
            }
            var box = old.GetComponent<DialogueBox>();
            var compass = old.GetComponent<SpaceRat.UI.CompassHud>();
            var hud = old.GetComponent<SpaceRat.UI.PlayerHud>();
            var go = old.gameObject;
            Object.DestroyImmediate(old);
            go.name = "Scene04Director";

            // ---- Pilot: bottom of the roof staircase, facing up it -------------------------------------
            var terrain = Object.FindAnyObjectByType<Terrain>();
            var my = GameObject.Find("MyOutpost");
            Transform stairs = null;
            if (my) foreach (var t in my.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("stairs_big")) { stairs = t; break; }
            Vector3 foot = stairs ? FootOf(stairs) : new Vector3(393.7f, 0f, 429.7f);
            var cc = sw.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            float gy = terrain ? terrain.SampleHeight(foot) + terrain.transform.position.y : foot.y;
            sw.transform.SetPositionAndRotation(new Vector3(foot.x, Mathf.Max(gy, foot.y) + 0.1f, foot.z), Quaternion.Euler(0f, 45f, 0f));
            if (cc) cc.enabled = true;

            // ---- Bot (hovering with the nav module) + gameplay leftovers --------------------------------
            var gameplay = GameObject.Find("OutpostGameplay");
            var bot = Object.FindAnyObjectByType<SpaceRat.Characters.ServiceBot>(FindObjectsInactive.Include);
            var nav = GameObject.Find("NavModule");
            if (bot)
            {
                bot.gameObject.SetActive(true);
                foreach (var it in bot.GetComponents<SpaceRat.Characters.Interactable>()) it.Available = false;
                if (nav)
                {
                    nav.SetActive(true);
                    foreach (var c in nav.GetComponentsInChildren<Collider>()) c.enabled = false;
                    foreach (var it in nav.GetComponents<SpaceRat.Characters.Interactable>()) Object.DestroyImmediate(it);
                    nav.transform.SetParent(bot.transform, true);
                    nav.transform.localPosition = new Vector3(0f, -0.35f, 0f);
                }
            }
            var chair = GameObject.Find("CaptainChair");
            if (chair) chair.SetActive(false);   // Scene 3's terminal scene is done
            var ph = new List<GameObject>();
            if (my) foreach (var t in my.GetComponentsInChildren<Transform>(true))
                if (t.name.IndexOf("Place Bot here", System.StringComparison.OrdinalIgnoreCase) >= 0
                 || t.name.IndexOf("computer access", System.StringComparison.OrdinalIgnoreCase) >= 0
                 || t.name.IndexOf("navigation part", System.StringComparison.OrdinalIgnoreCase) >= 0) ph.Add(t.gameObject);
            foreach (var g in ph) g.SetActive(false);

            // ---- Director --------------------------------------------------------------------------------
            var dlg = EnsureDialogue();
            var director = go.AddComponent<Scene04Director>();
            var crash = GameObject.Find("CrashSite");
            B.SetRefs(director,
                ("dialogue", box), ("pilot", sw), ("compass", compass), ("hud", hud),
                ("crashSite", crash ? crash.transform : null), ("bot", bot),
                ("doors", gameplay ? gameplay.GetComponent<SpaceRat.Environment.DoorOpener>() : null),
                ("intro", dlg["Intro"]), ("afterLiftoff", dlg["AfterLiftoff"]));
            if (compass) compass.Visible = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Dst);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == Dst);
            int s3 = scenes.FindIndex(s => s.path.EndsWith("Scene03_Terrain.unity"));
            scenes.Insert(s3 >= 0 ? s3 + 1 : scenes.Count, new EditorBuildSettingsScene(Dst, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[SpaceRat] Built {Dst} (pilot at {sw.transform.position})");
        }

        /// <summary>Foot of the staircase: its lowest end, a couple of metres out from the first step.</summary>
        static Vector3 FootOf(Transform stairs)
        {
            var b = new Bounds(stairs.position, Vector3.zero);
            foreach (var r in stairs.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            // The stairs' forward points away from the climb (down end); fall back to the min corner.
            Vector3 down = new Vector3(stairs.forward.x, 0f, stairs.forward.z).normalized;
            Vector3 p = new Vector3(b.center.x, b.min.y, b.center.z) + down * (b.extents.x + 1.6f);
            return p;
        }

        static Dictionary<string, DialogueSequence> EnsureDialogue()
        {
            B.EnsureFolder(B.DialogueDir + "/Scene04");
            return new Dictionary<string, DialogueSequence>
            {
                ["Intro"] = B.EnsureSequence("Scene04/Intro",
                    C("Navigation module secured. I am taking it to the ship. Do not do anything interesting."),
                    P("Define interesting."),
                    C("Anything that ends with me explaining why it was a bad idea."),
                    P("So, my entire personality.")),
                ["AfterLiftoff"] = B.EnsureSequence("Scene04/AfterLiftoff",
                    C("Once the module is installed I can follow her rover's trail. Until then, you may look around the station."),
                    P("Captain Voss. Missing rover, missing crew, missing sense of self-preservation."),
                    C("The doors still open for you. It is the only welcome this planet has offered."),
                    P("Wow. I'll treasure it.")),
            };
        }
    }
}
