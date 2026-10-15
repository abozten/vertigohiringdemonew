using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelOfFortune.Core;

namespace WheelOfFortune.UI
{
    /// <summary>
    /// Top zone strip. Uses a fixed set of labels that are re-numbered every zone instead of
    /// creating one object per zone, so it works for unlimited zones with zero allocations.
    /// Early zones keep zone 1 at the left edge and move the highlight instead, so the strip never shows blank slots.
    /// </summary>
    public sealed class ZoneBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;       // slides left when the zone advances
        [SerializeField] private TMP_Text[] _zoneTexts;         // odd count, the middle one sits at the strip center
        [SerializeField] private Image _currentHighlight;
        [SerializeField] private RectTransform _currentFrame;
        [SerializeField] private TMP_Text _nextSafeText;
        [SerializeField] private TMP_Text _nextSuperText;
        [SerializeField] private float _itemWidth = 76f;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _safeColor = new(0.45f, 0.9f, 0.2f);
        [SerializeField] private Color _superColor = new(1f, 0.75f, 0.2f);
        [SerializeField] private Color _currentTextColor = Color.black;

        private int _shownZone;
        private int _shownCenterLabel;

        public void Show(int zone, GameSettings settings)
        {
            int center = _zoneTexts.Length / 2;
            // Number of fully visible items either side of the strip center.
            int visibleHalf = Mathf.FloorToInt((((RectTransform)_content.parent).rect.width / _itemWidth - 1f) * 0.5f);
            int centerLabel = Mathf.Max(zone, visibleHalf + 1);
            for (int i = 0; i < _zoneTexts.Length; i++)
            {
                int labelZone = centerLabel - center + i;
                _zoneTexts[i].text = labelZone > 0 ? labelZone.ToString() : string.Empty;
                _zoneTexts[i].color = labelZone == zone ? _currentTextColor : GetColor(settings.GetZoneType(labelZone));
            }

            _currentHighlight.color = GetColor(settings.GetZoneType(zone));
            // Upcoming milestones; the current zone is already marked in the strip.
            _nextSafeText.text = GameSettings.NextZone(zone + 1, settings.SafeZoneInterval).ToString();
            _nextSuperText.text = GameSettings.NextZone(zone + 1, settings.SuperZoneInterval).ToString();

            float highlightX = (zone - centerLabel) * _itemWidth;
            bool animate = zone > _shownZone && _shownZone > 0;
            MoveCurrent(_currentHighlight.rectTransform, highlightX, animate);
            MoveCurrent(_currentFrame, highlightX, animate);

            // Strip moved forward: start where the old labels were and slide into place.
            int shift = centerLabel - _shownCenterLabel;
            if (animate && shift > 0)
            {
                _content.DOKill();
                _content.anchoredPosition = new Vector2(_itemWidth * shift, 0f);
                _content.DOAnchorPosX(0f, 0.35f).SetEase(Ease.OutCubic).SetLink(gameObject);
            }
            _shownZone = zone;
            _shownCenterLabel = centerLabel;
        }

        private void MoveCurrent(RectTransform target, float x, bool animate)
        {
            target.DOKill();
            if (animate) target.DOAnchorPosX(x, 0.35f).SetEase(Ease.OutCubic).SetLink(gameObject);
            else target.anchoredPosition = new Vector2(x, target.anchoredPosition.y);
        }

        private Color GetColor(ZoneType type) => type switch
        {
            ZoneType.Super => _superColor,
            ZoneType.Safe => _safeColor,
            _ => _normalColor
        };
    }
}
