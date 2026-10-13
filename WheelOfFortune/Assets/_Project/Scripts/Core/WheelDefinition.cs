using System;
using UnityEngine;

namespace WheelOfFortune.Core
{
    [Serializable]
    public struct WheelSlice
    {
        public RewardDefinition Reward;
        [Min(1)] public int BaseAmount;
    }

    /// <summary>
    /// Look and contents of one wheel type (bronze / silver / golden). Edit the slices in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Wheel Of Fortune/Wheel", fileName = "wheel_")]
    public sealed class WheelDefinition : ScriptableObject
    {
        // The wheel art has 8 holes, so the slice count is fixed to match it.
        public const int SliceCount = 8;

        [SerializeField] private string _title = "BRONZE SPIN";
        [SerializeField] private Color _titleColor = Color.white;
        [SerializeField] private Sprite _baseSprite;
        [SerializeField] private Sprite _indicatorSprite;
        [SerializeField] private WheelSlice[] _slices = new WheelSlice[SliceCount];

        public string Title => _title;
        public Color TitleColor => _titleColor;
        public Sprite BaseSprite => _baseSprite;
        public Sprite IndicatorSprite => _indicatorSprite;
        public WheelSlice GetSlice(int index) => _slices[index];

        public bool HasBomb()
        {
            foreach (WheelSlice slice in _slices)
                if (slice.Reward != null && slice.Reward.IsBomb) return true;
            return false;
        }

        public void Init(string title, Color titleColor, Sprite baseSprite, Sprite indicatorSprite, WheelSlice[] slices)
        {
            _title = title;
            _titleColor = titleColor;
            _baseSprite = baseSprite;
            _indicatorSprite = indicatorSprite;
            _slices = slices;
            OnValidate();
        }

        private void OnValidate()
        {
            if (_slices == null || _slices.Length != SliceCount)
                Array.Resize(ref _slices, SliceCount);
        }
    }
}
