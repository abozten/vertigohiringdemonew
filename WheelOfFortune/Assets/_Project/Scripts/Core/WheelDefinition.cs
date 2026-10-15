using System;
using System.Collections.Generic;
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

        /// <summary>A slice with no reward is never landed on (see WheelSession.PickSlice) and shows as an empty socket.</summary>
        public bool IsFilled(int index) => _slices[index].Reward != null;

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
            Sanitize();
        }

        /// <summary>Data problems a designer must fix; empty when the wheel is valid.</summary>
        public List<string> GetProblems()
        {
            var problems = new List<string>();
            int filled = 0;
            for (int i = 0; i < _slices.Length; i++)
            {
                if (_slices[i].Reward == null) problems.Add($"Slice {i} has no reward.");
                else filled++;
            }
            if (filled == 0) problems.Add("Wheel has no rewards and cannot be spun.");
            return problems;
        }

        /// <summary>Fixes what can be fixed silently: slice count, and amounts of slices added by a resize (0 by default).</summary>
        private void Sanitize()
        {
            if (_slices == null || _slices.Length != SliceCount)
                Array.Resize(ref _slices, SliceCount);
            for (int i = 0; i < _slices.Length; i++)
                _slices[i].BaseAmount = Math.Max(1, _slices[i].BaseAmount);
        }

        private void OnValidate()
        {
            Sanitize();
            foreach (string problem in GetProblems())
                Debug.LogError($"{name}: {problem}", this);
        }
    }
}
