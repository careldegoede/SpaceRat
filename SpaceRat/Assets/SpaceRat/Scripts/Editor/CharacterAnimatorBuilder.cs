using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Builds Assets/SpaceRat/Animation/CharacterPreview.controller from the imported animation packs
    /// (Human Basic Motions FREE, Basic Character Animations FREE). State order = viewer number keys.
    /// Looping states stay; one-shot states (Jump, Talk) return to Idle.
    /// </summary>
    public static class CharacterAnimatorBuilder
    {
        public const string ControllerPath = "Assets/SpaceRat/Animation/CharacterPreview.controller";
        const string Kevin = "Assets/Kevin Iglesias/Human Animations/Animations/Male/";
        const string Burak = "Assets/Burak Uzun/BasicCharacterAnimations/Character Animations/";

        // (state name, fbx path, clip name, returns to idle)
        static readonly (string state, string fbx, string clip, bool oneShot)[] States =
        {
            ("Idle",        Kevin + "Idles/HumanM@Idle01.fbx",                    "HumanM@Idle01",            false),
            ("Idle Alt",    Burak + "Idles/Idle_InPlace.fbx",                     "Rig|Idle_Alt_InPlace",     false),
            ("Walk",        Kevin + "Movement/Walk/HumanM@Walk01_Forward.fbx",    "HumanM@Walk01_Forward",    false),
            ("Run",         Kevin + "Movement/Run/HumanM@Run01_Forward.fbx",      "HumanM@Run01_Forward",     false),
            ("Sprint",      Kevin + "Movement/Sprint/HumanM@Sprint01_Forward.fbx","HumanM@Sprint01_Forward",  false),
            ("Crouch Walk", Burak + "Movement/Crouch/Crouch_InPlace.fbx",         "Rig|Crouch_Walk_Forward",  false),
            ("Jump",        Kevin + "Movement/Jump/HumanM@Jump01.fbx",            "HumanM@Jump01",            true),
            ("Talk",        Kevin + "Social/Conversation/HumanM@Talk01.fbx",      "HumanM@Talk01",            true),
            ("Swim",        Burak + "Movement/Swim/Swim_InPlace.fbx",             "Rig|Swim_Forward_InPlace", false),
        };

        public static string[] StateNames => States.Select(s => s.state).ToArray();

        [MenuItem("SpaceRat/Build Character Preview Animator")]
        public static AnimatorController Build()
        {
            string dir = Path.GetDirectoryName(ControllerPath)!.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/SpaceRat", "Animation");

            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;

            AnimatorState idle = null;
            var oneShots = new System.Collections.Generic.List<AnimatorState>();
            int y = 0;
            foreach (var (stateName, fbx, clipName, oneShot) in States)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
                if (!clip)
                {
                    Debug.LogWarning($"[SpaceRat] Clip '{clipName}' not found in {fbx} — state '{stateName}' skipped.");
                    continue;
                }
                var st = sm.AddState(stateName, new Vector3(300, y += 60, 0));
                st.motion = clip;
                st.writeDefaultValues = true;
                if (stateName == "Idle") idle = st;
                if (oneShot) oneShots.Add(st);
            }

            if (idle)
            {
                sm.defaultState = idle;
                foreach (var st in oneShots)
                {
                    var t = st.AddTransition(idle);
                    t.hasExitTime = true;
                    t.exitTime = 0.95f;
                    t.duration = 0.2f;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpaceRat] Built {ControllerPath} with {sm.states.Length} states.");
            return controller;
        }
    }
}
