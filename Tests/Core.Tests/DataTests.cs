using System;
using System.Linq;
using NUnit.Framework;
using SlotBattle.Core.Data;

namespace SlotBattle.Core.Tests
{
    /// <summary>JSON 초기 데이터가 설계서 3장 수치와 같은지 확인한다.</summary>
    public class DataTests
    {
        private static GameData Data => TestHelper.Data;

        [Test]
        public void 일차_출시_유닛은_8종이다()
        {
            var ids = Data.Units.Where(u => u.Release == 1).Select(u => u.Id).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { "blade", "shooter", "shield", "swarm", "boom", "plasma", "dash", "drone" }, ids);
        }

        [TestCase("blade", "common", 100, 20, 1.0f, true, 0f, 1.0f)]
        [TestCase("shooter", "common", 60, 15, 1.0f, false, 4.0f, 1.0f)]
        [TestCase("shield", "common", 250, 8, 1.2f, true, 0f, 0.7f)]
        [TestCase("swarm", "common", 35, 8, 0.7f, true, 0f, 1.4f)]
        [TestCase("boom", "common", 40, 60, 1.0f, true, 0f, 1.6f)]
        [TestCase("plasma", "rare", 50, 30, 1.8f, false, 3.5f, 0.8f)]
        [TestCase("dash", "rare", 120, 25, 0.9f, true, 0f, 1.8f)]
        [TestCase("drone", "rare", 70, 14, 0.8f, false, 2.5f, 1.3f)]
        public void 유닛_수치는_설계서와_같다(string id, string rarity, float hp, float attack, float interval, bool melee, float range, float speed)
        {
            var u = Data.GetUnit(id);
            Assert.AreEqual(rarity, u.Rarity);
            Assert.AreEqual(hp, u.Hp);
            Assert.AreEqual(attack, u.Attack);
            Assert.AreEqual(interval, u.AttackInterval, 1e-6);
            Assert.AreEqual(melee, u.Melee);
            if (!melee)
            {
                Assert.AreEqual(range, u.Range, 1e-6);
            }
            Assert.AreEqual(speed, u.MoveSpeed, 1e-6);
        }

        [Test]
        public void 유닛_특성은_키워드로_표현한다()
        {
            Assert.IsTrue(Data.GetUnit("shooter").HasKeyword("antiAir"));
            Assert.IsTrue(Data.GetUnit("drone").HasKeyword("air"));
            Assert.IsTrue(Data.GetUnit("plasma").HasKeyword("splash"));
            Assert.AreEqual(1.0f, Data.GetUnit("plasma").SplashRadius);
            Assert.IsTrue(Data.GetUnit("boom").HasKeyword("selfDestruct"));
            Assert.AreEqual(1.5f, Data.GetUnit("boom").SplashRadius);
            Assert.IsTrue(Data.GetUnit("swarm").HasKeyword("swarm"));
            Assert.AreEqual(3, Data.GetUnit("swarm").SwarmCount);
        }

        [Test]
        public void 슬롯_설정은_설계서와_같다()
        {
            var slot = Data.Slot;
            Assert.AreEqual(3, slot.ReelCount);
            Assert.AreEqual(3, slot.SpinCost);
            Assert.AreEqual(1, slot.RespinCost);
            Assert.AreEqual(1, slot.RespinsPerSpin);
            Assert.AreEqual(1, slot.MaxLockedReels);
            Assert.AreEqual(25, slot.RarityWeights["common"]);
            Assert.AreEqual(18, slot.RarityWeights["rare"]);
            Assert.AreEqual(12, slot.RarityWeights["epic"]);
            CollectionAssert.AreEqual(new[] { 1.0f, 2.2f, 5.0f }, slot.TierMultipliers);
            Assert.AreEqual(5, slot.DeckSize);
            CollectionAssert.AreEqual(new[] { "blade", "shooter", "shield", "swarm", "plasma" }, slot.StartingDeck);
        }

        [Test]
        public void 전투_설정은_설계서와_같다()
        {
            var b = Data.Battle;
            Assert.AreEqual(180f, b.DurationSeconds);
            Assert.AreEqual(2, b.LaneCount);
            Assert.AreEqual(20f, b.LaneLength);
            Assert.AreEqual(10f, b.PlayerZoneLength);
            Assert.AreEqual(20000f, b.CastleHp);
            Assert.AreEqual(100f, b.CastleDefense.Damage);
            Assert.AreEqual(1.0f, b.CastleDefense.AttackInterval);
            Assert.AreEqual(3.4f, b.CastleDefense.Range, 1e-6);
            Assert.AreEqual(5f, b.StartMana);
            Assert.AreEqual(10f, b.MaxMana);
            Assert.AreEqual(1f, b.ManaRegenPerSecond);
            Assert.AreEqual(30f, b.FinalPhaseSeconds);
            Assert.AreEqual(2f, b.FinalPhaseRegenMultiplier);
            Assert.AreEqual(4, b.WaitingQueueCapacity);
            Assert.AreEqual(4, b.CommanderSkill.ManaCost);
            Assert.AreEqual(2.0f, b.CommanderSkill.Radius);
            Assert.AreEqual(40f, b.CommanderSkill.Damage);
            Assert.AreEqual(2f, b.CommanderSkill.StunSeconds);
            Assert.AreEqual(30f, b.CommanderSkill.CooldownSeconds);
        }

        [Test]
        public void 스테이지_1부터_3까지_있다()
        {
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, Data.Stages.Select(s => s.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 30000f, 16000f, 6000f }, Data.Stages.Select(s => s.EnemyCastleHp).ToArray());
            foreach (var stage in Data.Stages)
            {
                Assert.IsNotEmpty(stage.Spawns);
            }
        }

        [Test]
        public void 처리기가_없는_키워드는_불러올_때_거부한다()
        {
            string units = "{\"units\":[{\"id\":\"x\",\"rarity\":\"common\",\"hp\":1,\"keywords\":[\"teleport\"]}]}";
            string slot = "{\"rarityWeights\":{\"common\":25},\"tierMultipliers\":[1,2.2,5],\"startingDeck\":[]}";
            string battle = "{\"laneCount\":2}";
            string stages = "{\"stages\":[]}";
            Assert.Throws<InvalidOperationException>(() => GameData.FromJson(units, slot, battle, stages));
        }

        [Test]
        public void 시간표에_없는_유닛이_있으면_거부한다()
        {
            string units = "{\"units\":[{\"id\":\"x\",\"rarity\":\"common\",\"hp\":1}]}";
            string slot = "{\"rarityWeights\":{\"common\":25},\"tierMultipliers\":[1,2.2,5],\"startingDeck\":[]}";
            string battle = "{\"laneCount\":2}";
            string stages = "{\"stages\":[{\"id\":1,\"spawns\":[{\"time\":1,\"lane\":0,\"unit\":\"nope\"}]}]}";
            Assert.Throws<InvalidOperationException>(() => GameData.FromJson(units, slot, battle, stages));
        }
    }
}
