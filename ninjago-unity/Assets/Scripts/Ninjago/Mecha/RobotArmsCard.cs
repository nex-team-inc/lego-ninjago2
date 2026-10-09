#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>One player's card in the robot arms setup step: which arm holds the L and how long the hold has lasted.</summary>
    public class RobotArmsCard : MonoBehaviour
    {
        [Header("Player Tag")]
        [SerializeField] PlayerTagLabel playerTag = null!;
        [Header("Left Arm Icon")]
        [SerializeField] Image leftArmIcon = null!;
        [Header("Right Arm Icon")]
        [SerializeField] Image rightArmIcon = null!;
        [Header("Hold Fill")]
        [SerializeField] Image holdFill = null!;
        [Header("Done Mark")]
        [SerializeField] GameObject doneMark = null!;
        [Header("Arm Held Color")]
        [SerializeField] Color armHeldColor = new(0.3f, 1f, 0.35f);
        [Header("Arm Broken Color")]
        [SerializeField] Color armBrokenColor = new(0.55f, 0.55f, 0.6f, 0.9f);

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            playerTag.Initialize(playerIndex, color);
            holdFill.color = color;
            doneMark.SetActive(false);
            Show(false, false, 0f);
        }

        #endregion

        #region Public API

        public void Show(bool leftArmL, bool rightArmL, float hold01)
        {
            leftArmIcon.color = leftArmL ? armHeldColor : armBrokenColor;
            rightArmIcon.color = rightArmL ? armHeldColor : armBrokenColor;
            holdFill.fillAmount = hold01;
        }

        public void ShowDone()
        {
            Show(true, true, 1f);
            doneMark.SetActive(true);
        }

        #endregion
    }
}
