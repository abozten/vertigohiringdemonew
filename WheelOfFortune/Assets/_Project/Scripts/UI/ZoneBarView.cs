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
    /// </summary>
    public sealed class ZoneBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;       // slides left when the zone advances
        [SerializeField] private TMP_Text[] _zoneTexts;         // odd count, the middle one is the current zone
        [SerializeField] private Image _currentHighlight;
        [SerializeField] private TMP_Text _nextSafeText;
        [SerializeField] private TMP_Text _nextSuperText;
        [SerializeField] private float _itemWidth = 76f;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _safeColor = new(0.45f, 0.9f, 0.2f);
        [SerializeField] private Color _superColor = new(1f, 0.75f, 0.2f);
        [SerializeField] private Color _currentTextColor = Color.black;

        private int _shownZone;

        public void Show(int zone, GameSettings settings)
        {
            int center = _zoneTexts.Length / 2;
            for (int i = 0; i < _zoneTexts.Length; i++)
            {
                int labelZone = zone - center + i;
                _zoneTexts[i].text = labelZone > 0 ? labelZone.ToString() : string.Empty;
                _zoneTexts[i].color = i == center ? _currentTextColor : GetColor(settings.GetZoneType(labelZone));
            }

            _currentHighlight.color = GetColor(settings.GetZoneType(zone));
            _nextSafeText.text = GameSettings.NextZone(zone, settings.SafeZoneInterval).ToString();
            _nextSuperText.text = GameSettings.NextZone(zone, settings.SuperZoneInterval).ToString();

            // Moving forward: start one item to the right (looks like the old state) and slide into place.
            if (zone > _shownZone && _shownZone > 0)
            {
                _content.DOKill();
                _content.anchoredPosition = new Vector2(_itemWidth, 0f);
                _content.DOAnchorPosX(0f, 0.35f).SetEase(Ease.OutCubic).SetLink(gameObject);
            }
            _shownZone = zone;
        }

        private Color GetColor(ZoneType type) => type switch
        {
            ZoneType.Super => _superColor,
            ZoneType.Safe => _safeColor,
            _ => _normalColor
        };
    }
}
