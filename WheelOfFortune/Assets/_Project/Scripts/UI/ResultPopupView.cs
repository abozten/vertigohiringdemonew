using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheelOfFortune.UI
{
    [Serializable]
    public struct PopupContent
    {
        public string Title;
        public Sprite Icon;
        public string ButtonLabel;
    }

    /// <summary>Single popup reused for "bomb exploded" and "rewards collected".</summary>
    public sealed class ResultPopupView : MonoBehaviour
    {
        private const string CloseButtonName = "ui_button_popup_close";

        [SerializeField] private RectTransform _panel;   // animated child; the root only toggles on/off
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _buttonText;
        [SerializeField] private Button _closeButton;

        public event Action Closed;

        private void Awake() => _closeButton.onClick.AddListener(Close);
        private void OnDestroy() => _closeButton.onClick.RemoveListener(Close);

        public void Show(PopupContent content)
        {
            _titleText.text = content.Title;
            _icon.sprite = content.Icon;
            _buttonText.text = content.ButtonLabel;
            gameObject.SetActive(true);

            _panel.DOKill();
            _panel.localScale = Vector3.zero;
            _panel.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetLink(gameObject);
            _icon.transform.DOKill(true);
            _icon.transform.DOPunchRotation(new Vector3(0f, 0f, 10f), 0.6f, 10).SetDelay(0.3f).SetLink(gameObject);
        }

        private void Close()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

#if UNITY_EDITOR
        private void OnValidate() => _closeButton = UIUtils.FindButton(this, CloseButtonName);
#endif
    }
}
