using UnityEngine;

namespace WheelOfFortune.Core
{
    /// <summary>
    /// One kind of item that can appear on a wheel slice (gold, cash, a chest, a rifle...).
    /// The bomb is also a RewardDefinition with IsBomb = true, so a slice never needs special-case data.
    /// </summary>
    [CreateAssetMenu(menuName = "Wheel Of Fortune/Reward", fileName = "reward_")]
    public sealed class RewardDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private bool _isBomb;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public bool IsBomb => _isBomb;

        /// <summary>Used by the editor setup script and by tests to create rewards from code.</summary>
        public void Init(string displayName, Sprite icon, bool isBomb)
        {
            _displayName = displayName;
            _icon = icon;
            _isBomb = isBomb;
        }
    }
}
