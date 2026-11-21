using System;
using System.Collections.Generic;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;

namespace SlotBattle.Tools.BalanceSim
{
    /// <summary>
    /// 숙련형: 상성과 대형을 따져 놓는다.
    /// - 수비 후 역습: 적이 내 성 가까이(거리 6) 오면 상성이 맞는 유닛을 바로 앞에 놓아 성 포격과 함께 막고,
    ///   내 유닛이 이기고 있는 라인이 생기면 그 뒤에 붙여 민다. 그 전에는 대기 칸에 모아 둔다.
    /// - 위협이 없을 때는 다시 돌리기 마나(1)까지 모은 뒤 스핀해 2단계를 노린다.
    /// - 세 칸이 모두 다르면 가장 쓸모 있는 칸을 고정하고 다시 돌리고, 그래도 다르면 전장의 적과 상성을 보고 고른다.
    /// - 적이 뭉치면 사령관 기술을 쓴다.
    /// 판단 수치는 봇의 성향이라 Core 데이터가 아니라 여기 둔다.
    /// </summary>
    public sealed class SkilledPlayer : IAutoPlayer
    {
        // 전략 값. 조정 실험용으로 환경 변수(SKILLED_DANGER, SKILLED_PUSH, SKILLED_DEFEND)로 바꿀 수 있다.
        private static readonly float DangerLine = EnvFloat("SKILLED_DANGER", 6f);
        private static readonly int PushSize = (int)EnvFloat("SKILLED_PUSH", 2f);
        private static readonly float DefendRatio = EnvFloat("SKILLED_DEFEND", 1f);
        private static readonly float AnchorBonus = EnvFloat("SKILLED_ANCHOR", 1.6f);
        private static readonly bool UseRespin = EnvFloat("SKILLED_RESPIN", 1f) > 0f;
        /// <summary>다시 돌리기 마나까지 모은 뒤 스핀한다(위협이 없을 때만).</summary>
        private static readonly bool WaitForRespin = EnvFloat("SKILLED_WAITRESPIN", 1f) > 0f;
        /// <summary>0보다 크면 수비 후 역습: 적이 이 거리보다 가까이 오기 전에는 내보내지 않고 모아 둔다.</summary>
        private static readonly float HoldLine = EnvFloat("SKILLED_HOLD", 0f);

        private static float EnvFloat(string name, float fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
        }

        private readonly LaneInfo[] _lanes = new LaneInfo[8];

        public string Name => "숙련형";

        public void Act(BattleSimulation sim)
        {
            if (sim.IsOver)
            {
                return;
            }
            AnalyzeLanes(sim);
            TryCommanderSkill(sim);
            HandleSlot(sim);
            PlaceUnits(sim);
            HandleSlot(sim);
        }

        // ───────────── 판세 읽기 ─────────────

        private struct LaneInfo
        {
            public float EnemyGroundPower;
            public float EnemyAirPower;
            public float MyPower;
            public float EnemyFront;
            public float EnemyGroundFront;
            public float MyFront;
            public int EnemyCount;
            /// <summary>내 방어진(움직이지 않는 도발 유닛) 중 가장 앞의 위치. 없으면 -1.</summary>
            public float MyAnchor;
            public Dictionary<string, int> EnemyKinds;

            public float EnemyPower => EnemyGroundPower + EnemyAirPower;
        }

        private static float Power(Unit unit)
        {
            float dps = unit.Attack * BattleSimulation.TickRate / unit.AttackIntervalTicks;
            return (float)Math.Sqrt(Math.Max(0f, unit.Hp) * dps);
        }

        private static float Power(UnitDef def, float tierMultiplier, int count)
        {
            float dps = def.Attack / Math.Max(0.1f, def.AttackInterval);
            return (float)Math.Sqrt(def.Hp * tierMultiplier * dps * tierMultiplier) * count;
        }

        private void AnalyzeLanes(BattleSimulation sim)
        {
            int laneCount = sim.Config.LaneCount;
            for (int i = 0; i < laneCount; i++)
            {
                var kinds = _lanes[i].EnemyKinds ?? new Dictionary<string, int>();
                kinds.Clear();
                _lanes[i] = new LaneInfo
                {
                    EnemyFront = sim.Config.LaneLength,
                    EnemyGroundFront = sim.Config.LaneLength,
                    MyFront = 0f,
                    MyAnchor = -1f,
                    EnemyKinds = kinds,
                };
            }
            foreach (var unit in sim.Units)
            {
                ref var lane = ref _lanes[unit.Lane];
                if (unit.Side == Side.Enemy)
                {
                    float power = Power(unit) * (unit.Y < DangerLine ? 1.5f : 1f);
                    if (unit.IsAir)
                    {
                        lane.EnemyAirPower += power;
                    }
                    else
                    {
                        lane.EnemyGroundPower += power;
                        lane.EnemyGroundFront = Math.Min(lane.EnemyGroundFront, unit.Y);
                    }
                    lane.EnemyFront = Math.Min(lane.EnemyFront, unit.Y);
                    lane.EnemyCount++;
                    lane.EnemyKinds.TryGetValue(unit.Def.Id, out int n);
                    lane.EnemyKinds[unit.Def.Id] = n + 1;
                }
                else
                {
                    lane.MyPower += Power(unit);
                    lane.MyFront = Math.Max(lane.MyFront, unit.Y);
                    if (!unit.CanMove && unit.IsTaunting)
                    {
                        lane.MyAnchor = Math.Max(lane.MyAnchor, unit.Y);
                    }
                }
            }
        }

        /// <summary>이 라인에 이 유닛을 놓았을 때의 가치. 0이면 쓸모 없음.</summary>
        private float Score(BattleSimulation sim, UnitDef def, int tier, in LaneInfo lane)
        {
            int count = def.HasKeyword("swarm") ? Math.Max(1, def.SwarmCount) : 1;
            float score = Power(def, sim.Data.TierMultiplier(tier), count);
            bool antiAir = def.HasKeyword("antiAir");
            if (lane.EnemyGroundPower <= 0f && lane.EnemyAirPower > 0f && !antiAir)
            {
                return 0f;
            }
            if (antiAir && lane.EnemyAirPower > 0f)
            {
                score *= 2f;
            }
            // 상성표(units.json의 strongAgainst): 강한 상대가 많은 라인일수록 가치가 크다.
            if (def.StrongAgainst != null)
            {
                foreach (var target in def.StrongAgainst)
                {
                    if (lane.EnemyKinds.TryGetValue(target, out int n))
                    {
                        score *= 1f + 0.35f * n;
                    }
                }
            }
            // 그 라인에 나를 잡는 적(그 적의 strongAgainst에 내가 있음)이 많으면 피한다.
            foreach (var pair in lane.EnemyKinds)
            {
                if (sim.Data.GetUnit(pair.Key).IsStrongAgainst(def))
                {
                    score /= 1f + 0.35f * pair.Value;
                }
            }
            // 방어진 뒤의 원거리: 실드봇이 맞아 주는 동안 뒤에서 쏜다.
            if (!def.Melee && lane.MyAnchor >= 0f)
            {
                score *= AnchorBonus;
            }
            if ((def.HasKeyword("splash") || def.AttackType == AttackType.Lob || def.AttackType == AttackType.Explode) && lane.EnemyCount >= 3)
            {
                score *= 1.5f;
            }
            return score;
        }

        // ───────────── 사령관 기술 ─────────────

        private void TryCommanderSkill(BattleSimulation sim)
        {
            if (!sim.IsSkillReady)
            {
                return;
            }
            var skill = sim.Config.CommanderSkill;
            int bestLane = -1;
            float bestY = 0f;
            float bestValue = 0f;
            int bestCount = 0;
            foreach (var center in sim.Units)
            {
                if (center.Side != Side.Enemy)
                {
                    continue;
                }
                float value = 0f;
                int count = 0;
                foreach (var other in sim.Units)
                {
                    if (other.Side != Side.Enemy || other.Lane != center.Lane || Math.Abs(other.Y - center.Y) > skill.Radius)
                    {
                        continue;
                    }
                    value += Math.Min(other.Hp, skill.Damage) + Power(other) * 0.5f;
                    count++;
                }
                if (center.Y < DangerLine * 0.5f)
                {
                    value *= 1.5f;
                }
                if (value > bestValue)
                {
                    bestValue = value;
                    bestLane = center.Lane;
                    bestY = center.Y;
                    bestCount = count;
                }
            }
            if (bestLane >= 0 && (bestCount >= 3 || bestValue >= 160f))
            {
                sim.Execute(new UseCommanderSkillCommand(bestLane, bestY));
            }
        }

        // ───────────── 슬롯 ─────────────

        private void HandleSlot(BattleSimulation sim)
        {
            if (sim.PendingSpin == null && sim.CanSpin)
            {
                bool threatened = false;
                for (int i = 0; i < sim.Config.LaneCount; i++)
                {
                    threatened |= _lanes[i].EnemyFront < DangerLine;
                }
                int need = sim.Data.Slot.SpinCost + (WaitForRespin && !threatened ? sim.Data.Slot.RespinCost : 0);
                if (sim.Mana >= need || sim.Mana >= sim.MaxMana)
                {
                    sim.Execute(new SpinCommand());
                }
            }
            var pending = sim.PendingSpin;
            if (pending == null)
            {
                return;
            }
            if (UseRespin && pending.RequiresChoice && pending.RespinsUsed < sim.Data.Slot.RespinsPerSpin && sim.Mana >= sim.Data.Slot.RespinCost)
            {
                sim.Execute(new RespinCommand(BestReel(sim, pending.Symbols, 2)));
            }
            int choice = pending.RequiresChoice ? BestReel(sim, pending.Symbols, 1) : -1;
            sim.Execute(new ConfirmSpinCommand(choice));
        }

        /// <summary>그 칸의 유닛을 tier 단계로 받았을 때 어느 라인에서든 가장 쓸모 있는 칸.</summary>
        private int BestReel(BattleSimulation sim, IReadOnlyList<int> symbols, int tier)
        {
            int bestReel = 0;
            float bestScore = float.MinValue;
            for (int reel = 0; reel < symbols.Count; reel++)
            {
                var def = sim.Deck[symbols[reel]];
                float score = 0f;
                for (int lane = 0; lane < sim.Config.LaneCount; lane++)
                {
                    score = Math.Max(score, Score(sim, def, tier, _lanes[lane]));
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestReel = reel;
                }
            }
            return bestReel;
        }

        // ───────────── 배치 ─────────────

        private void PlaceUnits(BattleSimulation sim)
        {
            if (sim.Queue.Count == 0)
            {
                return;
            }
            var cfg = sim.Config;

            // 1) 막아야 할 라인: 적이 내 진영 가까이 왔고 내 전력이 모자란 곳.
            int threatLane = -1;
            float worst = 0f;
            for (int i = 0; i < cfg.LaneCount; i++)
            {
                var lane = _lanes[i];
                if (lane.EnemyFront >= DangerLine)
                {
                    continue;
                }
                float deficit = lane.EnemyPower - lane.MyPower / DefendRatio;
                float urgency = deficit * (1f + (DangerLine - lane.EnemyFront) / DangerLine);
                if (deficit > 0f && urgency > worst)
                {
                    worst = urgency;
                    threatLane = i;
                }
            }
            if (threatLane >= 0)
            {
                int pick = BestQueueIndexFor(sim, _lanes[threatLane]);
                if (pick >= 0)
                {
                    var unit = sim.Queue[pick];
                    float position = DefensivePosition(sim, unit.Def, _lanes[threatLane]);
                    sim.Execute(new PlaceUnitCommand(pick, threatLane, position));
                    AnalyzeLanes(sim);
                    return;
                }
            }

            // 2) 위협이 없으면 모았다가 민다.
            bool queueFull = sim.Queue.Count >= sim.QueueCapacity;
            if (HoldLine > 0f && !queueFull && sim.RemainingSeconds >= 20f)
            {
                // 수비 후 역습: 내 유닛이 이기고 있는 라인이 있으면 그 뒤에 붙여 밀고, 아니면 적이 올 때까지 모은다.
                int best = -1;
                float margin = 0f;
                for (int i = 0; i < cfg.LaneCount; i++)
                {
                    float m = _lanes[i].MyPower - _lanes[i].EnemyPower * 1.3f;
                    if (_lanes[i].MyPower > 0f && m > margin)
                    {
                        margin = m;
                        best = i;
                    }
                }
                if (best < 0)
                {
                    return;
                }
                int pickPush = BestQueueIndexFor(sim, _lanes[best]);
                if (pickPush >= 0)
                {
                    float y = Clamp(_lanes[best].MyFront - 1f, 0f, cfg.PlayerZoneLength);
                    sim.Execute(new PlaceUnitCommand(pickPush, best, y));
                    AnalyzeLanes(sim);
                }
                return;
            }
            bool blocked = sim.PendingSpin != null && sim.QueueFreeSlots < 1;
            bool lateGame = sim.RemainingSeconds < 20f;
            if (sim.Queue.Count < PushSize && !queueFull && !blocked && !lateGame)
            {
                return;
            }
            int pushLane = ChoosePushLane(sim);
            PushFormation(sim, pushLane);
            AnalyzeLanes(sim);
        }

        private int BestQueueIndexFor(BattleSimulation sim, in LaneInfo lane)
        {
            int best = -1;
            float bestScore = 0f;
            for (int i = 0; i < sim.Queue.Count; i++)
            {
                float score = Score(sim, sim.Queue[i].Def, sim.Queue[i].Tier, lane);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        private static float DefensivePosition(BattleSimulation sim, UnitDef def, in LaneInfo lane)
        {
            var cfg = sim.Config;
            float front = def.HasKeyword("antiAir") ? lane.EnemyFront : lane.EnemyGroundFront;
            float y;
            if (def.AttackType == AttackType.Explode)
            {
                y = front - 0.3f;
            }
            else if (def.Melee)
            {
                y = front - cfg.MeleeRange * 0.5f;
            }
            else
            {
                // 원거리는 사거리 끝에 두어 접근하는 동안 맞힌다. 내 방어진이 있으면 그 바로 뒤.
                y = front - def.Range + 0.3f;
                if (lane.MyAnchor >= 0f)
                {
                    y = Math.Max(y, lane.MyAnchor - 1.2f);
                }
                if (lane.MyFront > 0f)
                {
                    y = Math.Min(y, lane.MyFront - 0.5f);
                }
            }
            return Clamp(y, 0f, cfg.PlayerZoneLength);
        }

        private int ChoosePushLane(BattleSimulation sim)
        {
            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < sim.Config.LaneCount; i++)
            {
                var lane = _lanes[i];
                float queueValue = 0f;
                foreach (var queued in sim.Queue)
                {
                    queueValue += Score(sim, queued.Def, queued.Tier, lane);
                }
                float score = queueValue + lane.MyPower - lane.EnemyPower * 1.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        private void PushFormation(BattleSimulation sim, int laneIndex)
        {
            var cfg = sim.Config;
            var lane = _lanes[laneIndex];
            float front = Math.Min(cfg.PlayerZoneLength, lane.EnemyGroundFront - cfg.MeleeRange);
            // 버티는 유닛(체력 높고 느린) → 근접 → 원거리 순으로 앞에서부터 세운다.
            var order = new List<int>();
            for (int i = 0; i < sim.Queue.Count; i++)
            {
                order.Add(i);
            }
            order.Sort((a, b) => FrontRank(sim.Queue[a].Def).CompareTo(FrontRank(sim.Queue[b].Def)));
            var placements = new List<KeyValuePair<QueuedUnit, float>>();
            foreach (int index in order)
            {
                var def = sim.Queue[index].Def;
                float y;
                if (def.Melee)
                {
                    // 빠른 유닛일수록 뒤에 두어 느린 앞줄과 함께 도착하게 한다.
                    float speedGap = Math.Max(0f, def.MoveSpeed - 0.7f) * 2f;
                    y = front - speedGap;
                }
                else
                {
                    y = front - Math.Min(def.Range, 3f);
                }
                placements.Add(new KeyValuePair<QueuedUnit, float>(sim.Queue[index], Clamp(y, 0f, cfg.PlayerZoneLength)));
            }
            foreach (var placement in placements)
            {
                int index = IndexOf(sim.Queue, placement.Key);
                sim.Execute(new PlaceUnitCommand(index, laneIndex, placement.Value));
            }
        }

        private static int FrontRank(UnitDef def)
        {
            if (def.Melee && def.Hp >= 200f)
            {
                return 0;
            }
            return def.Melee ? 1 : 2;
        }

        private static int IndexOf(IReadOnlyList<QueuedUnit> queue, QueuedUnit unit)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i] == unit)
                {
                    return i;
                }
            }
            return -1;
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
