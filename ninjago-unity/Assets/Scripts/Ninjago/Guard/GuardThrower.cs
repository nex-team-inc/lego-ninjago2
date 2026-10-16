#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// A goblin on the wall, facing the ninjas (-Z). Winding up, it lifts a big brick over its head (the warning); the
    /// throw swings its arms forward and the held brick disappears into the flying one.
    /// </summary>
    public class GuardThrower : MonoBehaviour
    {
        [Header("Body")]
        [SerializeField] Transform body = null!;
        [Header("Left Arm")]
        [Tooltip("Shoulder pivot with the arm hanging along -Y.")]
        [SerializeField] Transform leftArm = null!;
        [Header("Right Arm")]
        [SerializeField] Transform rightArm = null!;
        [Header("Held Brick")]
        [Tooltip("The brick shown over the head while winding up.")]
        [SerializeField] GameObject heldBrick = null!;
        [Header("Throw Point")]
        [Tooltip("Where the flying brick starts.")]
        [SerializeField] Transform throwPoint = null!;

        float windUpElapsed = float.PositiveInfinity;
        float windUpSeconds = 1f;
        float throwElapsed = float.PositiveInfinity;

        public Vector3 ThrowPosition => throwPoint.position;

        #region Public API

        public void WindUp(float seconds)
        {
            windUpSeconds = seconds;
            windUpElapsed = 0f;
            throwElapsed = float.PositiveInfinity;
            heldBrick.SetActive(true);
        }

        public void Throw()
        {
            heldBrick.SetActive(false);
            windUpElapsed = float.PositiveInfinity;
            throwElapsed = 0f;
        }

        #endregion

        #region Animation

        void Update()
        {
            windUpElapsed += Time.deltaTime;
            throwElapsed += Time.deltaTime;
            // Arms up over the head while winding up, a fast swing forward on the throw, then back to idle.
            float armX;
            if (windUpElapsed < windUpSeconds) armX = -Mathf.Lerp(20f, 170f, Mathf.Clamp01(windUpElapsed / 0.3f));
            else if (throwElapsed < 0.5f) armX = -Mathf.Lerp(170f, 70f, Mathf.Clamp01(throwElapsed / 0.15f));
            else armX = -20f;
            leftArm.localRotation = rightArm.localRotation = Quaternion.Euler(armX, 0f, 0f);
            var shake = windUpElapsed < windUpSeconds ? Mathf.Sin(Time.time * 30f) * 3f : 0f;
            body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 0.06f, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, shake);
        }

        #endregion
    }
}
