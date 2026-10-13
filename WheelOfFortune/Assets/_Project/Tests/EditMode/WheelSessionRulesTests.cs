using NUnit.Framework;
using UnityEngine;
using WheelOfFortune.Core;

namespace WheelOfFortune.Tests
{
    public sealed class WheelSessionRulesTests
    {
        private RewardDefinition _reward;
        private WheelDefinition _normal;
        private WheelDefinition _safe;
        private WheelDefinition _super;
        private IGameRules _rules;

        [SetUp]
        public void SetUp()
        {
            _reward = ScriptableObject.CreateInstance<RewardDefinition>();
            _reward.Init("Test reward", null, isBomb: false);
            _normal = CreateWheel(10);
            _safe = CreateWheel(30);
            _super = CreateWheel(60);
            _rules = new AlternateRules(_normal, _safe, _super);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_normal);
            Object.DestroyImmediate(_safe);
            Object.DestroyImmediate(_super);
            Object.DestroyImmediate(_reward);
        }

        [Test]
        public void Session_UsesAlternateZoneAndWheelPolicies()
        {
            var session = new WheelSession(_rules, _ => 0);
            Assert.AreEqual(ZoneType.Normal, session.ZoneType);
            Assert.AreSame(_normal, session.CurrentWheel);
            Assert.IsFalse(session.CanLeave);

            session.Resolve(0);
            Assert.AreEqual(ZoneType.Safe, session.ZoneType);
            Assert.AreSame(_safe, session.CurrentWheel);
            Assert.IsTrue(session.CanLeave);

            session.Resolve(0);
            Assert.AreEqual(ZoneType.Super, session.ZoneType);
            Assert.AreSame(_super, session.CurrentWheel);
            Assert.IsTrue(session.CanLeave);

            session.Resolve(0);
            Assert.AreEqual(ZoneType.Normal, session.ZoneType);
            Assert.AreSame(_normal, session.CurrentWheel);
            Assert.IsFalse(session.CanLeave);
        }

        [Test]
        public void Session_DisplaysAndCollectsAmountsFromAlternateGrowthPolicy()
        {
            var session = new WheelSession(_rules, _ => 0);
            Assert.AreEqual(17, session.GetAmount(0)); // base 10 + zone 1 * 7
            session.Resolve(0);
            Assert.AreEqual(17, session.Rewards[_reward]);

            Assert.AreEqual(44, session.GetAmount(0)); // base 30 + zone 2 * 7
            session.Resolve(0);
            Assert.AreEqual(61, session.Rewards[_reward]);

            Assert.AreEqual(81, session.GetAmount(0)); // base 60 + zone 3 * 7
            session.Resolve(0);
            Assert.AreEqual(142, session.Rewards[_reward]);
        }

        private WheelDefinition CreateWheel(int baseAmount)
        {
            var slices = new WheelSlice[WheelDefinition.SliceCount];
            for (int i = 0; i < slices.Length; i++)
                slices[i] = new WheelSlice { Reward = _reward, BaseAmount = baseAmount };
            var wheel = ScriptableObject.CreateInstance<WheelDefinition>();
            wheel.Init("Test wheel", Color.white, null, null, slices);
            return wheel;
        }

        // An ordinary C# rules provider, independent of GameSettings. Shared wheel/reward
        // data remains in ScriptableObjects, just as it does for the production session.
        private sealed class AlternateRules : IGameRules
        {
            private readonly WheelDefinition _normal;
            private readonly WheelDefinition _safe;
            private readonly WheelDefinition _super;

            public AlternateRules(WheelDefinition normal, WheelDefinition safe, WheelDefinition super)
            {
                _normal = normal;
                _safe = safe;
                _super = super;
            }

            public ZoneType GetZoneType(int zone) => zone switch
            {
                2 => ZoneType.Safe,
                3 => ZoneType.Super,
                _ => ZoneType.Normal
            };

            public WheelDefinition GetWheel(ZoneType type) => type switch
            {
                ZoneType.Safe => _safe,
                ZoneType.Super => _super,
                _ => _normal
            };

            public int GetAmount(int baseAmount, int zone) => baseAmount + zone * 7;
        }
    }
}
