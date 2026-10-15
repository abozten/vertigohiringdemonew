using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelOfFortune.Core;

namespace WheelOfFortune.UI
{
    /// <summary>One line of the cash-out receipt: icon, reward name and the total collected.</summary>
    public sealed class ReceiptEntryView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _amountText;

        public void Show(RewardDefinition reward, int amount)
        {
            _icon.sprite = reward.Icon;
            _nameText.text = reward.DisplayName;
            _amountText.text = "x" + UIUtils.FormatAmount(amount);
        }
    }
}
