using UnityEngine;

namespace SpaceRat.Characters
{
    /// <summary>
    /// Puts a Humanoid character into a relaxed standing pose (arms down) using muscle values,
    /// so un-animated characters don't stand in a T-pose. Remove once real animations exist.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class RelaxedPose : MonoBehaviour
    {
        [Range(-1f, 1f)] [SerializeField] float armsDown = -0.62f;
        [Range(-1f, 1f)] [SerializeField] float armsForward = 0.25f;
        [Range(-1f, 1f)] [SerializeField] float elbowBend = 0.15f;
        [Range(-1f, 1f)] [SerializeField] float fingersCurl = 0.25f;

        void Start() => Apply();

        public void Apply()
        {
            var animator = GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isHuman) return;

            using var handler = new HumanPoseHandler(animator.avatar, transform);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            Vector3 rootPos = transform.position;
            Quaternion rootRot = transform.rotation;

            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string m = HumanTrait.MuscleName[i];
                if (m.EndsWith("Arm Down-Up")) pose.muscles[i] = armsDown;
                else if (m.EndsWith("Arm Front-Back")) pose.muscles[i] = armsForward;
                else if (m.EndsWith("Forearm Stretch")) pose.muscles[i] = 1f - elbowBend;
                else if (m.Contains("Stretched") && (m.Contains("Index") || m.Contains("Middle") || m.Contains("Ring") || m.Contains("Little")))
                    pose.muscles[i] = -fingersCurl;
            }
            handler.SetHumanPose(ref pose);
            transform.SetPositionAndRotation(rootPos, rootRot);
        }
    }
}
