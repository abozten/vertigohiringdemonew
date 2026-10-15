using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelOfFortune.Core;

namespace WheelOfFortune.UI
{
    public sealed class WheelSliceView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _amountText;

        public Vector3 IconPosition => _icon.transform.position;

        public void Show(RewardDefinition reward, int amount)
        {
            // An unassigned slice (wheel still being edited) shows as an empty socket.
            _icon.enabled = reward != null;
            if (reward == null)
            {
                _amountText.text = string.Empty;
                return;
            }

            _icon.sprite = reward.Icon;
            // Cropped sprites use a frame suited to their silhouette. Keep the icon and
            // quantity in separate bands inside the circular socket.
            float aspect = reward.Icon != null ? reward.Icon.rect.width / reward.Icon.rect.height : 1f;
            _icon.rectTransform.sizeDelta = reward.IsBomb ? new Vector2(62f, 66f)
                : aspect > 1.6f ? new Vector2(78f, 42f)
                : aspect < 0.65f ? new Vector2(38f, 58f)
                : new Vector2(54f, 54f);
            _icon.rectTransform.anchoredPosition = new Vector2(0f, reward.IsBomb ? 0f : 12f);
            _amountText.text = reward.IsBomb ? string.Empty : "x" + UIUtils.FormatAmount(amount);
        }
    }
}
