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
        }
    }
}
