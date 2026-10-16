#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>The little friend the ninjas protect: shakes while a brick flies, and jumps with both arms up when it is blocked.</summary>
    public class GuardFriend : MonoBehaviour
    {
        [Header("Body")]
        [SerializeField] Transform body = null!;
        [Header("Left Arm")]
        [Tooltip("Shoulder pivot with the arm hanging along -Y.")]
        [SerializeField] Transform leftArm = null!;
        [Header("Right Arm")]
        [SerializeField] Transform rightArm = null!;
        [Header("Cheer Point")]
        [Tooltip("Over the head, where hearts pop.")]
        [SerializeField] Transform cheerPoint = null!;
        [Header("Cheer Seconds")]
        [SerializeField] float cheerSeconds = 0.9f;

        bool worried;
        float cheerElapsed = float.PositiveInfinity;

        public Vector3 CheerPosition => cheerPoint.position;

        #region Public API

        public void SetWorried(bool isWorried)
        {
            worried = isWorried;
        }

        public void Cheer()
        {
            worried = false;
            cheerElapsed = 0f;
        }

        #endregion

        #region Animation

        void Update()
        {
            cheerElapsed += Time.deltaTime;
            var cheering = cheerElapsed < cheerSeconds;
            var hop = cheering ? 0.35f * Mathf.Abs(Mathf.Sin(cheerElapsed / cheerSeconds * Mathf.PI * 2f)) : 0f;
            var shake = worried ? Mathf.Sin(Time.time * 40f) * 0.03f : 0f;
            body.localPosition = new Vector3(shake, hop, 0f);
            // Arms up in a V to cheer, hands over the eyes while worried, down otherwise.
            var armX = cheering ? -170f : worried ? -130f : -8f;
            var armZ = cheering ? 25f : worried ? -18f : 6f;
            leftArm.localRotation = Quaternion.Euler(armX, 0f, -armZ);
            rightArm.localRotation = Quaternion.Euler(armX, 0f, armZ);
        }

        #endregion
    }
}
