#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The brick mecha: yaws on the center mark, tilts its upper body to pitch, fires the beam out of its chest and copies
    /// the robot arms (arms up in an L and lights on while the pose holds, arms hanging and lights dim when it breaks).
    /// </summary>
    public class MechaRig : MonoBehaviour
    {
        [Header("Yaw Root")]
        [SerializeField] Transform yawRoot = null!;
        [Header("Body")]
        [Tooltip("Everything that shakes on a hit and topples when the mecha goes down.")]
        [SerializeField] Transform body = null!;
        [Header("Pitch Pivot")]
        [SerializeField] Transform pitchPivot = null!;
        [Header("Muzzle")]
        [Tooltip("Beam origin on the chest; its forward is the beam direction.")]
        [SerializeField] Transform muzzle = null!;
        [Header("Beam")]
        [Tooltip("Unit beam along local +Z for a hit width of 1; scaled to the beam width and length.")]
        [SerializeField] Transform beam = null!;
        [Header("Left Shoulder")]
        [SerializeField] Transform leftShoulder = null!;
        [Header("Left Elbow")]
        [SerializeField] Transform leftElbow = null!;
        [Header("Right Shoulder")]
        [SerializeField] Transform rightShoulder = null!;
        [Header("Right Elbow")]
        [SerializeField] Transform rightElbow = null!;
        [Header("Light Renderers")]
        [SerializeField] Renderer[] lights = null!;
        [Header("Lights On Material")]
        [SerializeField] Material lightsOnMaterial = null!;
        [Header("Lights Dim Material")]
        [SerializeField] Material lightsDimMaterial = null!;
        [Header("Player Color Renderers")]
        [SerializeField] Renderer[] colorRenderers = null!;
        [Header("Player Marker")]
        [SerializeField] PlayerTagLabel marker = null!;
        [Header("Arm Swing Seconds")]
        [Tooltip("Time the arms take to swing up into the L or drop.")]
        [SerializeField] float armSwingSeconds = 0.18f;
        [Header("Hit Shake Seconds")]
        [SerializeField] float hitShakeSeconds = 0.35f;
        [Header("Down Topple Seconds")]
        [SerializeField] float toppleSeconds = 0.8f;

        float armBlend;
        bool powered;
        bool down;
        float hitElapsed = float.PositiveInfinity;
        float downElapsed;

        public Vector3 MuzzlePosition => muzzle.position;
        public Vector3 MuzzleForward => muzzle.forward;

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            foreach (var colorRenderer in colorRenderers)
            {
                colorRenderer.material.color = color;
            }

            marker.Initialize(playerIndex, color);
            SetPowered(false);
            SetBeam(false, 0f, 0f);
        }

        #endregion

        #region Public API

        /// <summary>Yaw in degrees (+ turns right), pitch in degrees (+ aims up).</summary>
        public void SetAim(float yaw, float pitch)
        {
            yawRoot.localRotation = Quaternion.Euler(0f, yaw, 0f);
            pitchPivot.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
        }

        public void SetPowered(bool on)
        {
            powered = on;
            foreach (var lightRenderer in lights)
            {
                lightRenderer.sharedMaterial = on ? lightsOnMaterial : lightsDimMaterial;
            }
        }

        public void SetBeam(bool on, float length, float width)
        {
            beam.gameObject.SetActive(on);
            if (on) beam.localScale = new Vector3(width, width, length);
        }

        public void PlayHit()
        {
            hitElapsed = 0f;
        }

        public void SetDown()
        {
            down = true;
            downElapsed = 0f;
            SetPowered(false);
            SetBeam(false, 0f, 0f);
        }

        public void Tick(float deltaTime)
        {
            armBlend = Mathf.MoveTowards(armBlend, powered ? 1f : 0f, deltaTime / armSwingSeconds);
            var eased = armBlend * armBlend * (3f - 2f * armBlend);
            // L: upper arm out to the side, forearm straight up. Rest: arms hanging a little open.
            leftShoulder.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-12f, -90f, eased));
            leftElbow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -90f, eased));
            rightShoulder.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(12f, 90f, eased));
            rightElbow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 90f, eased));

            hitElapsed += deltaTime;
            var shake = 1f - Mathf.Clamp01(hitElapsed / hitShakeSeconds);
            body.localPosition = new Vector3(Mathf.Sin(hitElapsed * 70f) * 0.12f * shake, 0f, 0f);
            if (!down) return;
            downElapsed += deltaTime;
            var fall = Mathf.Clamp01(downElapsed / toppleSeconds);
            body.localRotation = Quaternion.Euler(-75f * fall * fall, 0f, 0f);
        }

        #endregion
    }
}
