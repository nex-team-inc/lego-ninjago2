#nullable enable

using Nex.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>A blocky background monster standing behind the path in a talking zone; its bubble repeats the banner.</summary>
    public class CrouchMonster : MonoBehaviour
    {
        [Header("Body")]
        [Tooltip("Bobs while it talks.")]
        [SerializeField] Transform body = null!;
        [Header("Head")]
        [SerializeField] Transform head = null!;
        [Header("Tint Renderers")]
        [SerializeField] Renderer[] tintRenderers = null!;
        [Header("Bubble Background")]
        [SerializeField] Image bubble = null!;
        [Header("Bubble Title")]
        [SerializeField] NexLocalizedString bubbleTitle = null!;
        [Header("Bubble Instruction")]
        [SerializeField] NexLocalizedString bubbleInstruction = null!;
        [Header("Talk Hz")]
        [SerializeField] float talkHz = 2.2f;

        float phase;

        #region Initialization

        public void Initialize(LocalizedString title, LocalizedString instruction, Color bubbleColor, Color bodyColor, float talkPhase, bool facingRight)
        {
            bubbleTitle.StringReference = title;
            bubbleInstruction.StringReference = instruction;
            bubble.color = bubbleColor;
            foreach (var tintRenderer in tintRenderers) tintRenderer.material.color = bodyColor;
            phase = talkPhase;
            body.localRotation = Quaternion.Euler(0f, facingRight ? 90f : -90f, 0f);
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            var t = Time.time * talkHz * Mathf.PI * 2f + phase;
            body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t)) * 0.08f, 0f);
            head.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 12f, 0f, 0f);
        }

        #endregion
    }
}
