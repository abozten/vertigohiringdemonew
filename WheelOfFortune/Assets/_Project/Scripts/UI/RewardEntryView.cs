using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheelOfFortune.UI
{
    public sealed class RewardEntryView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _amountText;

        public Vector3 IconPosition => _icon.transform.position;

        public void SetIcon(Sprite icon) => _icon.sprite = icon;

        public void SetAmount(int amount)
        {
            _amountText.text = UIUtils.FormatAmount(amount);
            _icon.transform.DOKill(true);
            _icon.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 6).SetLink(gameObject);
        }
    }
}
