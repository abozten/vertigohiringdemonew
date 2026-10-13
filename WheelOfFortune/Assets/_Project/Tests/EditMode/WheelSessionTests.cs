using NUnit.Framework;
using UnityEngine;
using WheelOfFortune.Core;

namespace WheelOfFortune.Tests
{
    public sealed class WheelSessionTests
    {
        private const int BombSlice = 0;
        private const int GoldSlice = 1;

        private RewardDefinition _bomb;
        private RewardDefinition _gold;
        private GameSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _bomb = ScriptableObject.CreateInstance<RewardDefinition>();
            _bomb.Init("Bomb", null, isBomb: true);
            _gold = ScriptableObject.CreateInstance<RewardDefinition>();
            _gold.Init("Gold", null, isBomb: false);

            _settings = ScriptableObject.CreateInstance<GameSettings>(); // defaults: safe every 5, super every 30, +10% per zone
            _settings.Init(CreateWheel(withBomb: true), CreateWheel(withBomb: false), CreateWheel(withBomb: false));
        }

        private WheelDefinition CreateWheel(bool withBomb)
        {
            var slices = new WheelSlice[WheelDefinition.SliceCount];
            for (int i = 0; i < slices.Length; i++)
                slices[i] = new WheelSlice { Reward = _gold, BaseAmount = 100 };
            if (withBomb) slices[BombSlice].Reward = _bomb;

            var wheel = ScriptableObject.CreateInstance<WheelDefinition>();
            wheel.Init("Test", Color.white, null, null, slices);
            return wheel;
        }

        private WheelSession CreateSession() => new(_settings, _ => GoldSlice);

        [TestCase(1, ZoneType.Normal)]
        [TestCase(5, ZoneType.Safe)]
        [TestCase(29, ZoneType.Normal)]
        [TestCase(30, ZoneType.Super)]
        [TestCase(60, ZoneType.Super)]
        public void ZoneType_FollowsIntervals(int zone, ZoneType expected) =>
            Assert.AreEqual(expected, _settings.GetZoneType(zone));

        [TestCase(1, 5, 5)]
        [TestCase(5, 5, 5)]
        [TestCase(6, 5, 10)]
        public void NextZone_RoundsUpToInterval(int zone, int interval, int expected) =>
            Assert.AreEqual(expected, GameSettings.NextZone(zone, interval));

        [Test]
        public void Reward_IsCollected_AndZoneAdvances()
        {
            WheelSession session = CreateSession();

            session.Resolve(GoldSlice);

            Assert.AreEqual(2, session.Zone);
            Assert.AreEqual(100, session.Rewards[_gold]);
        }

        [Test]
        public void Rewards_GrowEveryZone()
        {
            WheelSession session = CreateSession();
            int first = session.GetAmount(GoldSlice);
            session.Resolve(GoldSlice);

            Assert.Greater(session.GetAmount(GoldSlice), first);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(100)]
        public void Amount_StrictlyIncreases_EveryZone(int baseAmount)
        {
            Assert.AreEqual(baseAmount, _settings.GetAmount(baseAmount, 1));
            for (int zone = 2; zone <= 60; zone++)
                Assert.Greater(_settings.GetAmount(baseAmount, zone), _settings.GetAmount(baseAmount, zone - 1),
                    $"base {baseAmount}: zone {zone} must beat zone {zone - 1}");
        }

        [Test]
        public void Bomb_WipesRewards_AndEndsGame()
        {
            WheelSession session = CreateSession();
            session.Resolve(GoldSlice);

            session.Resolve(BombSlice);

            Assert.IsTrue(session.IsGameOver);
            Assert.IsEmpty(session.Rewards);
            Assert.IsFalse(session.CanLeave);
        }

        [Test]
        public void CanLeave_OnlyInSafeOrSuperZone()
        {
            WheelSession session = CreateSession();
            Assert.IsFalse(session.CanLeave);

            for (int i = 0; i < 4; i++) session.Resolve(GoldSlice); // reach zone 5

            Assert.AreEqual(ZoneType.Safe, session.ZoneType);
            Assert.IsTrue(session.CanLeave);
        }

        [Test]
        public void SafeAndSuperWheels_HaveNoBomb()
        {
            Assert.IsFalse(_settings.GetWheel(ZoneType.Safe).HasBomb());
            Assert.IsFalse(_settings.GetWheel(ZoneType.Super).HasBomb());
        }

        [Test]
        public void Restart_ResetsEverything()
        {
            WheelSession session = CreateSession();
            session.Resolve(GoldSlice);
            session.Resolve(BombSlice);

            session.Restart();

            Assert.AreEqual(1, session.Zone);
            Assert.IsFalse(session.IsGameOver);
            Assert.IsEmpty(session.Rewards);
        }
    }
}
