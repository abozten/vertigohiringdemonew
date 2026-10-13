using System;
using System.Collections.Generic;

namespace WheelOfFortune.Core
{
    /// <summary>
    /// Pure game state for one run (zone 1 until bomb or leave). No Unity UI code here,
    /// so it can be unit tested without a scene.
    /// </summary>
    public sealed class WheelSession
    {
        private readonly IGameRules _rules;
        private readonly Func<int, int> _randomIndex; // returns a number in [0, max)
        private readonly Dictionary<RewardDefinition, int> _rewards = new();

        public WheelSession(IGameRules rules, Func<int, int> randomIndex)
        {
            _rules = rules;
            _randomIndex = randomIndex;
            Restart();
        }

        public int Zone { get; private set; }
        public bool IsGameOver { get; private set; }
        public ZoneType ZoneType => _rules.GetZoneType(Zone);
        public WheelDefinition CurrentWheel => _rules.GetWheel(ZoneType);
        public IReadOnlyDictionary<RewardDefinition, int> Rewards => _rewards;

        /// <summary>The player may walk away only in safe and super zones.</summary>
        public bool CanLeave => !IsGameOver && ZoneType != ZoneType.Normal;

        public int GetAmount(int sliceIndex) =>
            _rules.GetAmount(CurrentWheel.GetSlice(sliceIndex).BaseAmount, Zone);

        public int PickSlice() => _randomIndex(WheelDefinition.SliceCount);

        /// <summary>Applies the slice result: bomb wipes everything, otherwise collect and go to the next zone.</summary>
        public RewardDefinition Resolve(int sliceIndex)
        {
            if (IsGameOver) throw new InvalidOperationException("Game is over. Call Restart first.");

            RewardDefinition reward = CurrentWheel.GetSlice(sliceIndex).Reward;
            if (reward.IsBomb)
            {
                _rewards.Clear();
                IsGameOver = true;
                return reward;
            }

            _rewards.TryGetValue(reward, out int total);
            _rewards[reward] = total + GetAmount(sliceIndex);
            Zone++;
            return reward;
        }

        public void Restart()
        {
            _rewards.Clear();
            Zone = 1;
            IsGameOver = false;
        }
    }
}
