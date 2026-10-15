using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelOfFortune.Core;

namespace WheelOfFortune.UI
{
    [Serializable]
    public struct PopupContent
    {
        public string Title;
        public Sprite Icon;
        public string ButtonLabel;
    }

    /// <summary>
    /// Result popup. Used for "bomb exploded" and, with a receipt list wired up, for "rewards collected".
    /// </summary>
    public sealed class ResultPopupView : MonoBehaviour
    {
        private const string CloseButtonName = "ui_button_popup_close";

        [SerializeField] private RectTransform _panel;   // animated child; the root only toggles on/off
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _buttonText;
        [SerializeField] private Button _closeButton;
        [Header("Receipt (optional)")]
        [SerializeField] private ReceiptEntryView _receiptEntryPrefab;
        [SerializeField] private RectTransform _receiptContent;
        [SerializeField] private float _receiptEntryDelay = 0.05f;

        private readonly List<ReceiptEntryView> _receiptEntries = new();
        private ScrollRect _receiptScroll;

        public event Action Closed;

        private void Awake()
        {
            _closeButton.onClick.AddListener(Close);
            if (_receiptContent != null) _receiptScroll = _receiptContent.GetComponentInParent<ScrollRect>(true);
        }

        private void OnDestroy() => _closeButton.onClick.RemoveListener(Close);

        public void Show(PopupContent content) => Show(content, Array.Empty<KeyValuePair<RewardDefinition, int>>());

        /// <summary>Shows the popup with one receipt line per reward. Pass a snapshot: the list is read once here.</summary>
        public void Show(PopupContent content, IReadOnlyList<KeyValuePair<RewardDefinition, int>> receipt)
        {
            _titleText.text = content.Title;
            _icon.sprite = content.Icon;
            _buttonText.text = content.ButtonLabel;
            gameObject.SetActive(true);
            FillReceipt(receipt);

            _panel.DOKill();
            _panel.localScale = Vector3.zero;
            _panel.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetLink(gameObject);
            _icon.transform.DOKill(true);
            _icon.transform.DOPunchRotation(new Vector3(0f, 0f, 10f), 0.6f, 10).SetDelay(0.3f).SetLink(gameObject);
        }

        private void FillReceipt(IReadOnlyList<KeyValuePair<RewardDefinition, int>> receipt)
        {
            if (_receiptContent == null) return;

            foreach (ReceiptEntryView entry in _receiptEntries)
                Destroy(entry.gameObject);
            _receiptEntries.Clear();

            for (int i = 0; i < receipt.Count; i++)
            {
                ReceiptEntryView entry = Instantiate(_receiptEntryPrefab, _receiptContent);
                entry.Show(receipt[i].Key, receipt[i].Value);
                _receiptEntries.Add(entry);

                // Lines pop in one after another once the panel has scaled up.
                Transform t = entry.transform;
                t.localScale = Vector3.zero;
                t.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetDelay(0.3f + i * _receiptEntryDelay).SetLink(entry.gameObject);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_receiptContent);
            if (_receiptScroll != null) _receiptScroll.verticalNormalizedPosition = 1f;
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
