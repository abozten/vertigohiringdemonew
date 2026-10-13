using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using WheelOfFortune.Core;

namespace WheelOfFortune.UI
{
    /// <summary>Left panel listing everything collected in this run. One entry per reward type.</summary>
    public sealed class RewardPanelView : MonoBehaviour
    {
        [SerializeField] private RewardEntryView _entryPrefab;
        [SerializeField] private RectTransform _content;
        [SerializeField] private Image _flyingIcon;   // one reusable icon that flies from the wheel to the panel
        [SerializeField] private float _flyDuration = 0.6f;
        [SerializeField] private GameObject _emptyState;

        private readonly Dictionary<RewardDefinition, RewardEntryView> _entries = new();
        private ScrollRect _scroll;

        private void Awake() => _scroll = _content.GetComponentInParent<ScrollRect>();

        public void Add(RewardDefinition reward, int newTotal, Vector3 fromPosition, Action onComplete)
        {
            if (!_entries.TryGetValue(reward, out RewardEntryView entry))
            {
                entry = Instantiate(_entryPrefab, _content);
                entry.SetIcon(reward.Icon);
                _entries.Add(reward, entry);
                if (_emptyState != null) _emptyState.SetActive(false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content); // so the new entry has its final position
            }
            ScrollIntoView((RectTransform)entry.transform); // the icon must not fly to an entry hidden by the mask

            Transform flying = _flyingIcon.transform;
            _flyingIcon.sprite = reward.Icon;
            flying.position = fromPosition;
            flying.localScale = Vector3.one;
            _flyingIcon.gameObject.SetActive(true);

            DOTween.Sequence()
                .Append(flying.DOScale(1.4f, 0.2f).SetEase(Ease.OutBack))
                .Append(flying.DOMove(entry.IconPosition, _flyDuration).SetEase(Ease.InCubic))
                .Join(flying.DOScale(0.6f, _flyDuration))
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    _flyingIcon.gameObject.SetActive(false);
                    entry.SetAmount(newTotal);
                    onComplete();
                });
        }

        public void Clear()
        {
            foreach (RewardEntryView entry in _entries.Values)
                Destroy(entry.gameObject);
            _entries.Clear();
            if (_emptyState != null) _emptyState.SetActive(true);
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>Scrolls the minimum distance needed so the target entry is fully visible.</summary>
        private void ScrollIntoView(RectTransform target)
        {
            if (_scroll == null) return;
            float viewHeight = _scroll.viewport.rect.height;
            float overflow = _content.rect.height - viewHeight;
            if (overflow <= 0f) return;

            // Distances measured downwards from the top of the content.
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_content, target);
            float top = _content.rect.yMax - bounds.max.y;
            float bottom = _content.rect.yMax - bounds.min.y;
            float offset = (1f - _scroll.verticalNormalizedPosition) * overflow;

            if (top < offset) offset = top;
            else if (bottom > offset + viewHeight) offset = bottom - viewHeight;

            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / overflow);
        }
    }
}
