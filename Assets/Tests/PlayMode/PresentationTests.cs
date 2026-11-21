using System.Collections;
using System.IO;
using NUnit.Framework;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;
using SlotBattle.Presentation;
using SlotBattle.Services;
using UnityEngine;
using UnityEngine.TestTools;

namespace SlotBattle.PlayModeTests
{
    /// <summary>기기 화면 크기(pt 기준 안전 영역 포함)를 흉내 낸다.</summary>
    internal static class Devices
    {
        // iPhone 15: 393x852pt @3x, 노치 59pt, 홈 표시줄 34pt
        public static readonly ScreenInfo IPhone15 = Make(393, 852, 3, 59, 34);
        // iPhone SE(3세대): 375x667pt @2x, 상태 표시줄 20pt
        public static readonly ScreenInfo IPhoneSE = Make(375, 667, 2, 20, 0);
        // iPad Air 11": 820x1180pt @2x, 상단 24pt, 홈 표시줄 20pt
        public static readonly ScreenInfo IPadAir = Make(820, 1180, 2, 24, 20);

        public static ScreenInfo Make(float widthPt, float heightPt, float scale, float topPt, float bottomPt)
        {
            var size = new Vector2(widthPt * scale, heightPt * scale);
            var safe = new Rect(0f, bottomPt * scale, widthPt * scale, (heightPt - topPt - bottomPt) * scale);
            return new ScreenInfo(size, safe, scale);
        }
    }

    internal static class TestData
    {
        public static string Read(string file)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "Data", file));
        }

        public static GameData Game()
        {
            return GameData.FromJson(Read("units.json"), Read("slot.json"), Read("battle.json"), Read("stages.json"));
        }

        public static FeelConfig Feel()
        {
            return FeelConfig.FromJson(Read("feel.json"));
        }
    }

    /// <summary>설계서 4장 "대상 기기와 화면" 배치 규칙.</summary>
    public class LayoutTests
    {
        private static readonly object[] AllDevices = { new object[] { "iPhone15" }, new object[] { "iPhoneSE" }, new object[] { "iPadAir" } };

        private static ScreenInfo Device(string name)
        {
            switch (name)
            {
                case "iPhone15": return Devices.IPhone15;
                case "iPhoneSE": return Devices.IPhoneSE;
                default: return Devices.IPadAir;
            }
        }

        private static bool Inside(Rect inner, Rect outer)
        {
            const float e = 0.01f;
            return inner.xMin >= outer.xMin - e && inner.xMax <= outer.xMax + e && inner.yMin >= outer.yMin - e && inner.yMax <= outer.yMax + e;
        }

        [TestCaseSource(nameof(AllDevices))]
        public void 모든_UI는_안전_영역_안에_있다(string device)
        {
            var layout = LayoutCalculator.Compute(Device(device), TestData.Feel().Layout);
            Assert.IsTrue(Inside(layout.Hud, layout.SafeArea), "HUD");
            Assert.IsTrue(Inside(layout.Battlefield, layout.SafeArea), "전장");
            Assert.IsTrue(Inside(layout.PlayerCastleBar, layout.SafeArea), "내 성 막대");
            Assert.IsTrue(Inside(layout.Control, layout.SafeArea), "조작 영역");
        }

        [TestCaseSource(nameof(AllDevices))]
        public void 영역끼리_겹치지_않는다(string device)
        {
            var l = LayoutCalculator.Compute(Device(device), TestData.Feel().Layout);
            Assert.LessOrEqual(l.Control.yMax, l.PlayerCastleBar.yMin + 0.01f);
            Assert.LessOrEqual(l.PlayerCastleBar.yMax, l.Battlefield.yMin + 0.01f);
            Assert.LessOrEqual(l.Battlefield.yMax, l.Hud.yMin + 0.01f);
        }

        [TestCaseSource(nameof(AllDevices))]
        public void 전장은_고정_비율이다(string device)
        {
            var feel = TestData.Feel().Layout;
            var l = LayoutCalculator.Compute(Device(device), feel);
            Assert.AreEqual(feel.BattlefieldAspect, l.Battlefield.width / l.Battlefield.height, 0.001f);
            Assert.Greater(l.Battlefield.height, 300f, "전장이 너무 작지 않다");
        }

        [Test]
        public void iPad에서는_조작_영역이_최대_폭을_넘지_않고_가운데에_있다()
        {
            var feel = TestData.Feel().Layout;
            var l = LayoutCalculator.Compute(Devices.IPadAir, feel);
            Assert.AreEqual(feel.ControlMaxWidth, l.Control.width, 0.01f);
            Assert.Less(l.Control.width, l.SafeArea.width);
            Assert.AreEqual(l.SafeArea.center.x, l.Control.center.x, 0.01f);
        }

        [Test]
        public void iPhone에서는_조작_영역이_화면_폭을_쓴다()
        {
            var l = LayoutCalculator.Compute(Devices.IPhone15, TestData.Feel().Layout);
            Assert.AreEqual(l.SafeArea.width, l.Control.width, 0.01f);
        }

        [Test]
        public void 터치_대상은_44pt_이상이다()
        {
            var feel = TestData.Feel().Layout;
            Assert.GreaterOrEqual(feel.MinTouchSize, 44f);
            Assert.GreaterOrEqual(ControlPanel.QueueSlotSize, 44f);
            Assert.GreaterOrEqual(ControlPanel.ReelSize, 44f);
            Assert.GreaterOrEqual(ControlPanel.ButtonHeight, 44f);
            Assert.GreaterOrEqual(ControlPanel.SideButtonWidth, 44f);
            Assert.GreaterOrEqual(feel.HudHeight, 44f);
        }
    }

    /// <summary>화면을 실제로 띄워 스테이지 1을 끝까지 자동으로 진행하고, iPhone·iPad 화면을 캡처한다.</summary>
    public class BattleScreenTests
    {
        private GameObject _host;
        private RenderTexture _texture;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.Destroy(_host);
            }
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
            }
        }

        private BattleController Create(ScreenInfo screen, NullHapticService haptics)
        {
            _host = new GameObject("TestBootstrap");
            var controller = GameBootstrap.Build(_host, TestData.Game(), TestData.Feel(), haptics);
            controller.ScreenOverride = () => screen;
            _texture = new RenderTexture((int)screen.SizePx.x, (int)screen.SizePx.y, 24);
            controller.Camera.targetTexture = _texture;
            controller.Restart(7);
            return controller;
        }

        /// <summary>사람처럼 버튼과 같은 경로로 조작하는 간단한 자동 플레이.</summary>
        private static void Play(BattleController c, ref int lane)
        {
            var sim = c.Sim;
            if (sim.IsOver)
            {
                return;
            }
            if (sim.PendingSpin == null && sim.CanSpin)
            {
                c.PressMain();
            }
            var pending = sim.PendingSpin;
            if (pending != null)
            {
                if (pending.RequiresChoice && pending.RespinsUsed == 0 && sim.Mana >= 1)
                {
                    c.Control.Reels.Finish();
                    c.TapReel(0);
                    c.PressRespin();
                }
                c.Control.Reels.Finish();
                if (sim.PendingSpin != null && sim.PendingSpin.RequiresChoice && c.LockedReel < 0)
                {
                    c.TapReel(0);
                }
                c.PressMain();
            }
            while (sim.Queue.Count > 0 && !sim.IsOver)
            {
                if (c.PlaceFromQueue(0, lane, 8f) != CommandResult.Ok)
                {
                    break;
                }
                lane = 1 - lane;
            }
            if (sim.IsSkillReady)
            {
                foreach (var unit in sim.Units)
                {
                    if (unit.Side == Side.Enemy && unit.Y < 12f)
                    {
                        c.PressSkill();
                        Assert.IsTrue(c.IsAiming);
                        c.FireSkillAt(unit.Lane, unit.Y);
                        break;
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator 스테이지1을_처음부터_끝까지_화면과_함께_진행한다()
        {
            var haptics = new NullHapticService();
            var c = Create(Devices.IPhone15, haptics);
            int lane = 0;
            int frames = 0;
            while (!c.Sim.IsOver && frames < 3000)
            {
                Play(c, ref lane);
                c.StepForTest(0.25f);
                frames++;
                yield return null;
            }
            Assert.IsTrue(c.Sim.IsOver, "3분 안에 끝나야 한다");
            Assert.LessOrEqual(c.Sim.ElapsedSeconds, 180f);

            // 결과 화면이 뜨고, 한 번 눌러 바로 다시 시작한다.
            for (int i = 0; i < 40 && !c.Result.IsVisible; i++)
            {
                c.StepForTest(0.1f);
                yield return null;
            }
            Assert.IsTrue(c.Result.IsVisible, "결과 화면");
            c.Result.RetryButton.SimulateClick();
            Assert.IsFalse(c.Sim.IsOver);
            Assert.IsFalse(c.Result.IsVisible);
            Assert.Greater(haptics.PlayCount, 0, "진동 요청이 있었다");
        }

        [UnityTest]
        public IEnumerator 사건마다_유닛_모습이_생기고_사라진다()
        {
            var c = Create(Devices.IPhone15, new NullHapticService());
            int lane = 0;
            for (int i = 0; i < 120; i++)
            {
                Play(c, ref lane);
                c.StepForTest(0.25f);
                Assert.AreEqual(c.Sim.Units.Count, c.Field.ViewCount, "유닛 수와 화면의 유닛 모습 수가 같다");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator 원거리_공격은_날아가는_탄으로_보이고_늦춘_사건은_모두_보여준다()
        {
            var c = Create(Devices.IPhone15, new NullHapticService());
            int lane = 0;
            bool sawProjectile = false;
            for (int i = 0; i < 3600 && !c.Sim.IsOver; i++)
            {
                Play(c, ref lane);
                c.StepForTest(1f / 30f);
                if (c.Field.FlyingProjectileCount > 0 && !sawProjectile)
                {
                    sawProjectile = true;
                    // 탄이 날아가는 중의 화면을 남긴다.
                    for (int k = 0; k < 3; k++)
                    {
                        c.StepForTest(1f / 30f);
                    }
                    string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Screenshots"));
                    Directory.CreateDirectory(dir);
                    Capture(c, Path.Combine(dir, "Attack.png"));
                }
                if (i % 15 == 0)
                {
                    yield return null;
                }
                if (sawProjectile && i > 1800)
                {
                    break;
                }
            }
            Assert.IsTrue(sawProjectile, "탄이 화면에 날아간다");

            // 준비 동작(0.2초 이하)보다 길게 진행하면 늦춘 사망은 모두 보여 준 상태다.
            c.StepForTest(0.25f);
            Assert.AreEqual(0, c.Field.DyingCount);
            Assert.AreEqual(c.Sim.Units.Count, c.Field.ViewCount);
        }

        [UnityTest]
        public IEnumerator 조립을_누르면_릴이_돌고_멈춘_뒤_Core가_정한_결과를_보여준다()
        {
            var c = Create(Devices.IPhone15, new NullHapticService());
            yield return null;
            c.StepForTest(4f); // 스핀 비용만큼 에너지를 모은다
            Assert.IsTrue(c.Sim.CanSpin);
            c.PressMain();
            var pending = c.Sim.PendingSpin;
            Assert.IsNotNull(pending, "스핀 결과는 Core가 먼저 정한다");
            Assert.IsTrue(c.Control.Reels.IsAnimating, "릴이 돈다");
            c.StepForTest(0.01f);
            Assert.AreEqual("조립 중...", c.Control.MainButton.SubLabel.text, "도는 동안 결과를 미리 보여 주지 않는다");
            for (int i = 0; i < 90; i++)
            {
                c.StepForTest(1f / 30f);
                yield return null;
            }
            Assert.IsFalse(c.Control.Reels.IsAnimating, "3초 안에 멈춘다");
            for (int r = 0; r < c.Control.Reels.Count; r++)
            {
                Assert.AreSame(c.Sim.Deck[pending.Symbols[r]], c.Control.Reels[r].ShownDef, "릴 " + r + "은 Core 결과와 같은 그림");
            }
        }

        [UnityTest]
        public IEnumerator 세_칸이_모두_다르면_칸을_눌러_고른_유닛_1기만_받는다()
        {
            BattleController c = null;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                if (c == null)
                {
                    c = Create(Devices.IPhone15, new NullHapticService());
                }
                c.Restart(seed);
                c.StepForTest(4f);
                c.PressMain();
                var pending = c.Sim.PendingSpin;
                if (pending == null || !pending.RequiresChoice)
                {
                    continue;
                }
                c.Control.Reels.Finish();
                c.PressMain();
                Assert.AreSame(pending, c.Sim.PendingSpin, "고르기 전에는 확정되지 않는다");
                c.TapReel(1);
                var expected = c.Sim.Deck[pending.Symbols[1]];
                c.PressMain();
                Assert.IsNull(c.Sim.PendingSpin);
                Assert.AreEqual(1, c.Sim.Queue.Count, "스핀 한 번에 1기");
                Assert.AreSame(expected, c.Sim.Queue[0].Def, "누른 칸의 유닛");
                yield break;
            }
            Assert.Fail("세 칸이 모두 다른 시드를 찾지 못했습니다.");
        }

        [UnityTest]
        public IEnumerator 설정을_열면_멈추고_진동과_흔들림을_끌_수_있다()
        {
            var c = Create(Devices.IPhone15, new NullHapticService());
            c.Hud.SettingsButton.SimulateClick();
            Assert.IsTrue(c.IsPaused);
            Assert.IsTrue(c.Settings.IsVisible);
            int tick = c.Sim.CurrentTick;
            yield return null;
            c.StepForTest(1f);
            Assert.AreEqual(tick, c.Sim.CurrentTick, "설정 창이 열려 있는 동안 전투가 멈춘다");
            c.Settings.ShakeButton.SimulateClick();
            Assert.IsFalse(c.Field.ShakeEnabled);
            c.Settings.CloseButton.SimulateClick();
            Assert.IsFalse(c.IsPaused);
        }

        [UnityTest]
        public IEnumerator 설정에서_스테이지를_고르면_그_스테이지로_새_판을_시작한다()
        {
            var c = Create(Devices.IPhone15, new NullHapticService());
            Assert.AreEqual(1, c.Sim.Stage.Id);
            c.Hud.SettingsButton.SimulateClick();
            Assert.AreEqual(3, c.Settings.StageButtons.Count);
            c.Settings.StageButtons[1].SimulateClick();
            yield return null;
            Assert.AreEqual(2, c.Sim.Stage.Id);
            Assert.AreEqual(0, c.Sim.CurrentTick, "새 판");
            Assert.IsFalse(c.Settings.IsVisible);
            Assert.IsFalse(c.IsPaused);
        }

        [UnityTest]
        public IEnumerator 설정에서_덱을_바꾸면_그_덱으로_새_판을_시작한다()
        {
            var c = Create(Devices.IPhone15, new NullHapticService());
            c.Hud.SettingsButton.SimulateClick();
            Assert.AreEqual(8, c.Settings.DeckButtons.Count, "1차 출시 8종");
            Assert.IsFalse(System.Linq.Enumerable.Contains(c.DeckIds, "drone"));
            PressableButton Button(string label)
            {
                foreach (var b in c.Settings.DeckButtons)
                {
                    if (b.Label.text == label)
                    {
                        return b;
                    }
                }
                return null;
            }
            Button("트윈").SimulateClick();
            Assert.AreEqual(0, c.Sim.CurrentTick, "4종일 때는 그대로");
            Button("드론").SimulateClick();
            yield return null;
            Assert.IsTrue(System.Linq.Enumerable.Contains(c.DeckIds, "drone"));
            Assert.IsFalse(System.Linq.Enumerable.Contains(c.DeckIds, "twin"));
            Assert.IsTrue(System.Linq.Enumerable.Any(c.Sim.Deck, d => d.Id == "drone"), "새 판의 슬롯에 드론봇이 나온다");
            Assert.AreEqual(0, c.Sim.CurrentTick, "새 판");
        }

        [UnityTest]
        public IEnumerator 아이폰과_아이패드_화면을_캡처한다()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Screenshots"));
            Directory.CreateDirectory(dir);
            foreach (var pair in new[] { ("iPhone15", Devices.IPhone15), ("iPhoneSE", Devices.IPhoneSE), ("iPadAir", Devices.IPadAir) })
            {
                var c = Create(pair.Item2, new NullHapticService());
                int lane = 0;
                for (int i = 0; i < 150 && !c.Sim.IsOver; i++)
                {
                    Play(c, ref lane);
                    c.StepForTest(0.25f);
                    yield return null;
                }
                // 결과가 있는 조립 라인을 보여 주기 위해 한 번 더 돌린다.
                if (c.Sim.PendingSpin == null)
                {
                    c.StepForTest(9f);
                    c.PressMain();
                }
                Assert.IsNotNull(c.Sim.PendingSpin, "캡처에 조립 결과가 보이도록 스핀");
                for (int i = 0; i < 30; i++)
                {
                    c.StepForTest(1f / 30f);
                    yield return null;
                }
                Capture(c, Path.Combine(dir, pair.Item1 + ".png"));
                Object.Destroy(_host);
                _host = null;
                _texture.Release();
                Object.Destroy(_texture);
                _texture = null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator 로봇_9종과_단계를_3D로_캡처한다()
        {
            var c = Create(Devices.IPhone15, new NullHapticService());
            c.StepForTest(0.05f);
            var world = c.Field.World;
            var feel = TestData.Feel();
            var data = TestData.Game();
            string[] ids = { "blade", "shooter", "shield", "twin", "boom", "plasma", "dash", "drone" };
            var models = new System.Collections.Generic.List<RobotModel>();
            for (int i = 0; i < ids.Length; i++)
            {
                var side = i % 2 == 0 ? Side.Player : Side.Enemy;
                var model = RobotFactory.Create(data.GetUnit(ids[i]), side, 1, world.Root, feel.World.FieldWidth * 0.16f, feel.World.RobotFootprint, feel.World.AirHeight);
                model.Place(world.Ground(i % 2, 3f + (i / 2) * 4f), side == Side.Player, Vector2.one);
                models.Add(model);
            }
            for (int t = 1; t <= 3; t++)
            {
                var model = RobotFactory.Create(data.GetUnit("blade"), Side.Player, t, world.Root, feel.World.FieldWidth * feel.Unit.SizeByTierRatio[t - 1], feel.World.RobotFootprint, feel.World.AirHeight);
                model.Place(new Vector3(1.5f + (t - 1) * 3.5f, 0f, 18.5f), true, Vector2.one);
                models.Add(model);
            }
            yield return null;
            c.StepForTest(0.05f);
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Screenshots"));
            Directory.CreateDirectory(dir);
            Capture(c, Path.Combine(dir, "Robots.png"));
            foreach (var model in models)
            {
                model.Destroy();
            }
            Assert.AreEqual(11, models.Count);
        }

        private void Capture(BattleController c, string path)
        {
            Canvas.ForceUpdateCanvases();
            c.Field.World.Camera.Render();
            c.Camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _texture;
            var image = new Texture2D(_texture.width, _texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, _texture.width, _texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.Destroy(image);
            Debug.Log("[SlotBattle] 화면 캡처: " + path);
        }
    }
}
