#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// One player's ninja for the whole chapter. The same block body carries the fight rig (slip, spin, hit, swirl) and
    /// the gait rig (walk, run, crouch) on separate transforms, so every block animates the one ninja the player keeps.
    /// A frame no block ticks it, it idles.
    /// </summary>
    public class ChapterNinja : MonoBehaviour
    {
        [Header("Fight Rig")]
        [SerializeField] NinjaRig fightRig = null!;
        [Header("Gait Rig")]
        [SerializeField] CrouchNinjaRig gaitRig = null!;
        [Header("Status Prompt")]
        [Tooltip("Results and states over the head: KICK x3, HEARD, WIGGLE.")]
        [SerializeField] WorldPrompt status = null!;
        [Header("Hint Prompt")]
        [Tooltip("The one-word hint the first time a gesture appears.")]
        [SerializeField] WorldPrompt hint = null!;
        [Header("Body Center")]
        [SerializeField] Transform center = null!;
        [Header("Player Marker")]
        [Tooltip("The P1 / P2 tag over the head; turned to face the camera from every view.")]
        [SerializeField] Transform marker = null!;
        [Header("Leap Turns")]
        [SerializeField] float leapTurns = 1f;

        Camera facing = null!;
        int tickedFrame = -1;

        public int PlayerIndex { get; private set; }
        public Color Color { get; private set; }
        public NinjaRig Fight => fightRig;
        public WorldPrompt Status => status;
        public WorldPrompt Hint => hint;
        public Vector3 Center => center.position;
        public Vector3 FeetPosition => fightRig.FeetPosition;

        #region Initialization

        public void Initialize(int playerIndex, Color color, Camera camera)
        {
            PlayerIndex = playerIndex;
            Color = color;
            facing = camera;
            fightRig.Initialize(playerIndex, color);
            gaitRig.Initialize(playerIndex, color);
            status.Initialize(camera);
            hint.Initialize(camera);
        }

        #endregion

        #region Public API

        public void Place(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void TickFight(float deltaTime)
        {
            tickedFrame = Time.frameCount;
            fightRig.Tick(deltaTime);
            gaitRig.Tick(0f, false, deltaTime);
        }

        public void TickGait(float distance, bool crouching, float deltaTime)
        {
            tickedFrame = Time.frameCount;
            gaitRig.Tick(distance, crouching, deltaTime);
            fightRig.Tick(deltaTime);
        }

        public void PlayHeard() => gaitRig.PlayHeard();

        /// <summary>A ninja leap along an arc with a flip, landing at the pose.</summary>
        public async UniTask LeapToAsync(Vector3 position, Quaternion rotation, float seconds, float height, CancellationToken cancellationToken)
        {
            var from = transform.position;
            var fromRotation = transform.rotation;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                var eased = Mathf.SmoothStep(0f, 1f, t);
                var arc = 4f * height * t * (1f - t);
                transform.position = Vector3.Lerp(from, position, eased) + Vector3.up * arc;
                var flip = Quaternion.AngleAxis(-360f * leapTurns * eased, Vector3.right);
                transform.rotation = Quaternion.Slerp(fromRotation, rotation, eased) * flip;
                TickFight(Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            transform.SetPositionAndRotation(position, rotation);
            VfxManager.Instance.PlayVisualEffect(VfxManager.VisualEffect.NinjaDustKick, position);
        }

        #endregion

        #region Life Cycle

        void LateUpdate()
        {
            var toCamera = marker.position - facing.transform.position;
            if (toCamera.sqrMagnitude > 1e-4f) marker.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            if (tickedFrame == Time.frameCount) return;
            fightRig.Tick(Time.deltaTime);
            gaitRig.Tick(0f, false, Time.deltaTime);
        }

        #endregion
    }
}
