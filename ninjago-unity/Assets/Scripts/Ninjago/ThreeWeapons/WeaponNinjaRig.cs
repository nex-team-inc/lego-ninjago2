#nullable enable

using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;

namespace Nex.Ninjago
{
    /// <summary>
    /// The gauntlet ninja seen from behind (facing +Z). Each weapon reads from the model itself: sword = both arms out
    /// with a left-right arrow by the hands, the matching arm sweeping on each slash; nunchucks = the called arm raised
    /// to that side and height with the spin icon on that spot, whirling on each burst; shuriken = an arm cocked back
    /// with a dart icon by the hand, snapping forward on each throw. Arms are posed by direction (they hang along -Y).
    /// </summary>
    public class WeaponNinjaRig : MonoBehaviour
    {
        enum Mode
        {
            Idle,
            Walk,
            Sword,
            Nunchucks,
            Shuriken,
        }

        [Header("Body")]
        [SerializeField] Transform body = null!;
        [Header("Left Leg")]
        [SerializeField] Transform leftLeg = null!;
        [Header("Right Leg")]
        [SerializeField] Transform rightLeg = null!;
        [Header("Left Shoulder")]
        [SerializeField] Transform leftShoulder = null!;
        [Header("Right Shoulder")]
        [SerializeField] Transform rightShoulder = null!;
        [Header("Left Hand")]
        [SerializeField] Transform leftHand = null!;
        [Header("Right Hand")]
        [SerializeField] Transform rightHand = null!;
        [Header("Sword Prop")]
        [SerializeField] GameObject sword = null!;
        [Header("Left Nunchuck Prop")]
        [SerializeField] Transform leftNunchuck = null!;
        [Header("Right Nunchuck Prop")]
        [SerializeField] Transform rightNunchuck = null!;
        [Header("Left Shuriken Prop")]
        [SerializeField] GameObject leftShuriken = null!;
        [Header("Right Shuriken Prop")]
        [SerializeField] GameObject rightShuriken = null!;
        [Header("Sword Arrows Icon")]
        [Tooltip("The left-right arrows by the hands.")]
        [SerializeField] GameObject swordArrows = null!;
        [Header("Spin Icon")]
        [SerializeField] Transform spinIcon = null!;
        [Header("Spin Spots")]
        [Tooltip("Left head, right head, left body, right body (SpinArea order).")]
        [SerializeField] Transform[] spinSpots = null!;
        [Header("Left Dart Icon")]
        [SerializeField] GameObject leftDartIcon = null!;
        [Header("Right Dart Icon")]
        [SerializeField] GameObject rightDartIcon = null!;
        [Header("Prompt Root")]
        [SerializeField] GameObject promptRoot = null!;
        [Header("Prompt Label")]
        [SerializeField] NexLocalizedString promptLabel = null!;
        [Header("Player Color Renderers")]
        [SerializeField] Renderer[] colorRenderers = null!;
        [Header("Player Marker")]
        [SerializeField] PlayerTagLabel marker = null!;
        [Header("Pose Sharpness")]
        [Tooltip("How fast the arms move to their pose, per second.")]
        [SerializeField] float poseSharpness = 14f;
        [Header("Gesture Seconds")]
        [Tooltip("Length of one slash sweep, spin whirl or throw snap.")]
        [SerializeField] float gestureSeconds = 0.22f;

        readonly float[] gestureStart = { float.NegativeInfinity, float.NegativeInfinity };
        readonly bool[] sweptAcross = new bool[2];
        Vector3 leftDirection = Vector3.down;
        Vector3 rightDirection = Vector3.down;
        Mode mode;
        SpinArea spinArea;
        int cockedHand = 1;
        float walkCycle;
        float time;

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            foreach (var colorRenderer in colorRenderers)
            {
                colorRenderer.material.color = color;
            }

            marker.Initialize(playerIndex, color);
            ShowIdle();
        }

        #endregion

        #region Public API

        public Vector3 HandPosition(int hand) => (hand == 0 ? leftHand : rightHand).position;

        public Vector3 SpinSpot(SpinArea area) => spinSpots[(int)area].position;

        public void SetWalking(bool walking)
        {
            mode = walking ? Mode.Walk : Mode.Idle;
        }

        /// <summary>Choosing a weapon: no prop, no hint.</summary>
        public void ShowIdle()
        {
            mode = Mode.Idle;
            ShowProps(null);
            promptRoot.SetActive(false);
        }

        public void ShowWeapon(WeaponType weapon, LocalizedString prompt)
        {
            mode = weapon switch
            {
                WeaponType.Sword => Mode.Sword,
                WeaponType.Nunchucks => Mode.Nunchucks,
                _ => Mode.Shuriken,
            };
            cockedHand = 1;
            ShowProps(weapon);
            promptLabel.StringReference = prompt;
            promptRoot.SetActive(true);
        }

        public void SetSpinArea(SpinArea area)
        {
            spinArea = area;
            spinIcon.position = SpinSpot(area);
            var left = area is SpinArea.LeftHead or SpinArea.LeftBody;
            leftNunchuck.gameObject.SetActive(left);
            rightNunchuck.gameObject.SetActive(!left);
        }

        public void PlaySlash(int hand)
        {
            sweptAcross[hand] = !sweptAcross[hand];
            gestureStart[hand] = time;
        }

        public void PlaySpin()
        {
            var hand = spinArea is SpinArea.LeftHead or SpinArea.LeftBody ? 0 : 1;
            gestureStart[hand] = time;
        }

        public void PlayThrow(int hand)
        {
            gestureStart[hand] = time;
            // The hint moves to the other arm, so the next throw reads as a fresh wind-up.
            cockedHand = 1 - hand;
            leftDartIcon.SetActive(cockedHand == 0);
            rightDartIcon.SetActive(cockedHand == 1);
        }

        /// <param name="walked">Meters walked this frame.</param>
        public void Tick(float deltaTime, float walked)
        {
            time += deltaTime;
            walkCycle += walked * 3.4f;
            var swing = mode == Mode.Walk ? Mathf.Sin(walkCycle) : 0f;
            leftLeg.localRotation = Quaternion.Euler(swing * 30f, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-swing * 30f, 0f, 0f);
            body.localPosition = new Vector3(0f, mode == Mode.Walk ? Mathf.Abs(Mathf.Cos(walkCycle)) * 0.05f : 0f, 0f);

            var targetLeft = ArmDirection(0, swing);
            var targetRight = ArmDirection(1, swing);
            var blend = 1f - Mathf.Exp(-poseSharpness * deltaTime);
            leftDirection = Vector3.Slerp(leftDirection, targetLeft, Snapping(0) ? 1f : blend);
            rightDirection = Vector3.Slerp(rightDirection, targetRight, Snapping(1) ? 1f : blend);
            leftShoulder.localRotation = Quaternion.FromToRotation(Vector3.down, leftDirection);
            rightShoulder.localRotation = Quaternion.FromToRotation(Vector3.down, rightDirection);

            // The nunchuck whirls slowly as a hint and fast during a burst.
            leftNunchuck.Rotate(0f, 0f, (InGesture(0) ? 1800f : 360f) * deltaTime, Space.Self);
            rightNunchuck.Rotate(0f, 0f, -(InGesture(1) ? 1800f : 360f) * deltaTime, Space.Self);
        }

        #endregion

        #region Poses

        // Directions in the ninja's own space: x = its right (the player's right seen from behind), y up, z forward.
        Vector3 ArmDirection(int hand, float walkSwing)
        {
            var side = hand == 0 ? -1f : 1f;
            var gesturing = InGesture(hand);
            switch (mode)
            {
                case Mode.Sword:
                    // Both arms out in front; a slash sweeps that arm across, the next one back out.
                    var outward = new Vector3(side * 0.85f, -0.05f, 0.55f);
                    var across = new Vector3(-side * 0.35f, -0.05f, 0.95f);
                    return (sweptAcross[hand] ? across : outward).normalized;
                case Mode.Nunchucks:
                    var spinHand = spinArea is SpinArea.LeftHead or SpinArea.LeftBody ? 0 : 1;
                    if (hand != spinHand) return new Vector3(side * 0.2f, -1f, 0.05f).normalized;
                    var head = spinArea is SpinArea.LeftHead or SpinArea.RightHead;
                    var raised = head ? new Vector3(side * 0.55f, 0.85f, 0.1f) : new Vector3(side * 1f, 0.05f, 0.2f);
                    var circle = (gesturing ? 0.45f : 0.15f) * new Vector3(Mathf.Cos(time * 22f), Mathf.Sin(time * 22f), 0f);
                    return (raised + circle).normalized;
                case Mode.Shuriken:
                    if (gesturing) return new Vector3(-side * 0.25f, -0.15f, 1f).normalized;
                    return hand == cockedHand ? new Vector3(side * 0.6f, 0.75f, -0.35f).normalized : new Vector3(side * 0.2f, -1f, 0.1f).normalized;
                default:
                    return new Vector3(side * 0.15f, -1f, walkSwing * side * 0.45f).normalized;
            }
        }

        bool InGesture(int hand) => time - gestureStart[hand] < gestureSeconds;

        // A throw snaps the arm forward at once instead of easing there.
        bool Snapping(int hand) => mode == Mode.Shuriken && time - gestureStart[hand] < gestureSeconds * 0.4f;

        void ShowProps(WeaponType? weapon)
        {
            sword.SetActive(weapon == WeaponType.Sword);
            swordArrows.SetActive(weapon == WeaponType.Sword);
            spinIcon.gameObject.SetActive(weapon == WeaponType.Nunchucks);
            leftNunchuck.gameObject.SetActive(false);
            rightNunchuck.gameObject.SetActive(false);
            leftShuriken.SetActive(weapon == WeaponType.Shuriken);
            rightShuriken.SetActive(weapon == WeaponType.Shuriken);
            leftDartIcon.SetActive(false);
            rightDartIcon.SetActive(weapon == WeaponType.Shuriken);
        }

        #endregion
    }
}
