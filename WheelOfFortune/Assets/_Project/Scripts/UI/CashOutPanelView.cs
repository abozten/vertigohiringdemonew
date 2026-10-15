using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheelOfFortune.UI
{
    /// <summary>Right panel: tells the player whether leaving is possible now and when it unlocks.</summary>
    public sealed class CashOutPanelView : MonoBehaviour
    {
        [SerializeField] private Image _buttonImage;
        [SerializeField] private TMP_Text _buttonText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Image _icon;
        [SerializeField] private Color _lockedButtonColor = new(0.55f, 0.6f, 0.7f, 0.55f);
        [SerializeField] private Color _lockedTextColor = new(0.8f, 0.84f, 0.9f, 0.7f);
        [SerializeField] private float _lockedIconAlpha = 0.35f;

        public void Show(bool unlocked, int unlockZone)
        {
            _buttonImage.color = unlocked ? Color.white : _lockedButtonColor;
            _buttonText.text = unlocked ? "COLLECT" : "LOCKED";
            _buttonText.color = unlocked ? Color.white : _lockedTextColor;
            _statusText.text = unlocked
                ? "<color=#8FE85A>SAFE TO LEAVE</color>\nKeep everything you won"
                : $"Unlocks at <color=#8FE85A>zone {unlockZone}</color>\nThe bomb takes it all";
            Color icon = _icon.color;
            icon.a = unlocked ? 1f : _lockedIconAlpha;
            _icon.color = icon;
        }
    }
}
