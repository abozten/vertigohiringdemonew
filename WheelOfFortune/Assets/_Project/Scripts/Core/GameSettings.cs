using UnityEngine;

namespace WheelOfFortune.Core
{
    /// <summary>Game rules in one asset: which wheel each zone uses and how rewards grow.</summary>
    [CreateAssetMenu(menuName = "Wheel Of Fortune/Game Settings", fileName = "game_settings")]
    public sealed class GameSettings : ScriptableObject, IGameRules
    {
        [SerializeField] private WheelDefinition _normalWheel;
        [SerializeField] private WheelDefinition _safeWheel;
        [SerializeField] private WheelDefinition _superWheel;
        [SerializeField, Min(1)] private int _safeZoneInterval = 5;
        [SerializeField, Min(1)] private int _superZoneInterval = 30;
        [Tooltip("0.1 = every zone adds 10% of the slice's base amount (rounded, minimum +1 per zone).")]
        [SerializeField, Min(0f)] private float _rewardGrowthPerZone = 0.1f;

        public int SafeZoneInterval => _safeZoneInterval;
        public int SuperZoneInterval => _superZoneInterval;

        public ZoneType GetZoneType(int zone)
        {
            if (zone <= 0) return ZoneType.Normal;
            if (zone % _superZoneInterval == 0) return ZoneType.Super;
            if (zone % _safeZoneInterval == 0) return ZoneType.Safe;
            return ZoneType.Normal;
        }

        public WheelDefinition GetWheel(ZoneType type) => type switch
        {
            ZoneType.Super => _superWheel,
            ZoneType.Safe => _safeWheel,
            _ => _normalWheel
        };

        /// <summary>
        /// Linear growth with a whole-number step of at least 1, so every zone is strictly better than the last,
        /// even for base amount 1 (rounding a fractional multiplier would keep it at 1 for several zones).
        /// </summary>
        public int GetAmount(int baseAmount, int zone)
        {
            int stepPerZone = Mathf.Max(1, Mathf.RoundToInt(baseAmount * _rewardGrowthPerZone));
            return baseAmount + stepPerZone * (zone - 1);
        }

        /// <summary>First zone >= <paramref name="zone"/> that is a multiple of <paramref name="interval"/>.</summary>
        public static int NextZone(int zone, int interval) => (zone + interval - 1) / interval * interval;

        public void Init(WheelDefinition normal, WheelDefinition safe, WheelDefinition super)
        {
            _normalWheel = normal;
            _safeWheel = safe;
            _superWheel = super;
        }

        private void OnValidate()
        {
            // Designer safety net: risk-free wheels must never contain the bomb.
            if (_safeWheel != null && _safeWheel.HasBomb()) Debug.LogError("Safe wheel must not contain a bomb.", this);
            if (_superWheel != null && _superWheel.HasBomb()) Debug.LogError("Super wheel must not contain a bomb.", this);
        }
    }
}
