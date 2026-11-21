using System;
using System.Collections.Generic;
using SlotBattle.Core.Battle.Keywords;
using SlotBattle.Core.Data;
using SlotBattle.Core.Slot;
using SlotBattle.Core.Util;

namespace SlotBattle.Core.Battle
{
    /// <summary>
    /// 한 판의 전투. 초당 20회 Tick()으로 진행하고, 입력은 Execute(명령)으로만 받는다.
    /// 같은 데이터·시드·명령 순서면 결과가 항상 같다.
    /// </summary>
    public sealed class BattleSimulation
    {
        /// <summary>시뮬레이션 진행 간격(초당 틱 수). 구조 규칙이라 데이터가 아니라 상수로 둔다.</summary>
        public const int TickRate = 20;

        private const int ManaScale = 1000;

        private readonly GameData _data;
        private readonly BattleConfig _cfg;
        private readonly SlotMachine _slot;
        private readonly SeededRandom _slotRng;
        private readonly EnemySchedule _schedule;
        private readonly List<UnitDef> _deck;
        private readonly List<Unit> _units = new List<Unit>();
        private readonly List<QueuedUnit> _queue = new List<QueuedUnit>();
        private readonly List<BattleEvent> _events = new List<BattleEvent>();
        private readonly List<LoggedCommand> _log = new List<LoggedCommand>();
        private readonly List<EnemySchedule.Entry> _dueSpawns = new List<EnemySchedule.Entry>();
        private readonly List<PendingHit> _pendingHits = new List<PendingHit>();
        private readonly List<Unit> _selfDestroyed = new List<Unit>();
        private readonly AttackContext _ctx = new AttackContext();
        private readonly float _enemyStatMultiplier;
        private int _nextUnitId = 1;
        private int _manaMilli;
        private int _maxManaMilli;

        public BattleSimulation(GameData data, StageDef stage, IReadOnlyList<string> deck, ulong seed)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _cfg = data.Battle;
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
            Seed = seed;

            if (deck == null || deck.Count != data.Slot.DeckSize)
            {
                throw new ArgumentException("덱은 " + data.Slot.DeckSize + "종이어야 합니다.", nameof(deck));
            }
            _deck = new List<UnitDef>(deck.Count);
            var weights = new int[deck.Count];
            for (int i = 0; i < deck.Count; i++)
            {
                var def = data.GetUnit(deck[i]);
                if (_deck.Contains(def))
                {
                    throw new ArgumentException("덱에 같은 유닛이 두 번 있습니다: " + def.Id, nameof(deck));
                }
                _deck.Add(def);
                weights[i] = data.RarityWeight(def);
            }

            _slotRng = new SeededRandom(SeededRandom.Derive(seed, 1));
            _slot = new SlotMachine(weights, data.Slot.ReelCount, _slotRng);
            _schedule = new EnemySchedule(stage, data, TickRate);
            _enemyStatMultiplier = stage.EnemyStatMultiplier > 0f ? stage.EnemyStatMultiplier : 1f;

            PlayerCastle = new Castle(Side.Player, _cfg.CastleHp, 0f);
            EnemyCastle = new Castle(Side.Enemy, stage.EnemyCastleHp > 0f ? stage.EnemyCastleHp : _cfg.CastleHp, _cfg.LaneLength);

            _manaMilli = ToMilli(_cfg.StartMana);
            _maxManaMilli = ToMilli(_cfg.MaxMana);
            DurationTicks = (int)Math.Round(_cfg.DurationSeconds * TickRate);
        }

        // ───────────── 상태 읽기 ─────────────

        public GameData Data => _data;
        public BattleConfig Config => _cfg;
        public StageDef Stage { get; }
        public ulong Seed { get; }
        public SlotMachine SlotMachine => _slot;
        public IReadOnlyList<UnitDef> Deck => _deck;
        public IReadOnlyList<Unit> Units => _units;
        public IReadOnlyList<QueuedUnit> Queue => _queue;
        public int QueueCapacity => _cfg.WaitingQueueCapacity;
        public int QueueFreeSlots => QueueCapacity - _queue.Count;
        public SpinResult PendingSpin { get; private set; }
        public Castle PlayerCastle { get; }
        public Castle EnemyCastle { get; }
        public EnemySchedule Schedule => _schedule;
        public IReadOnlyList<LoggedCommand> CommandLog => _log;

        /// <summary>다음에 처리할 틱 번호. 0부터 시작.</summary>
        public int CurrentTick { get; private set; }
        public int DurationTicks { get; }
        public float ElapsedSeconds => (float)CurrentTick / TickRate;
        public float RemainingSeconds => (float)(DurationTicks - CurrentTick) / TickRate;
        public bool IsOver => Outcome != BattleOutcome.None;
        public BattleOutcome Outcome { get; private set; }
        /// <summary>3분이 다 되어 성 체력 비율로 끝났는가.</summary>
        public bool EndedByTimeout { get; private set; }

        /// <summary>쓸 수 있는 정수 마나.</summary>
        public int Mana => _manaMilli / ManaScale;
        /// <summary>게이지 표시용 소수 마나.</summary>
        public float ManaExact => (float)_manaMilli / ManaScale;
        public int MaxMana => _maxManaMilli / ManaScale;
        public bool IsFinalPhase => CurrentTick >= DurationTicks - (int)Math.Round(_cfg.FinalPhaseSeconds * TickRate);

        public int SkillCooldownTicks { get; private set; }
        public bool IsSkillReady => SkillCooldownTicks <= 0 && Mana >= _cfg.CommanderSkill.ManaCost;

        public bool CanSpin => !IsOver && PendingSpin == null && _queue.Count < QueueCapacity && Mana >= _data.Slot.SpinCost;

        /// <summary>false로 두면 사건을 쌓지 않는다(BalanceSim처럼 연출이 필요 없을 때).</summary>
        public bool EventsEnabled { get; set; } = true;

        /// <summary>쌓인 사건을 꺼내고 비운다.</summary>
        public List<BattleEvent> DrainEvents()
        {
            var copy = new List<BattleEvent>(_events);
            _events.Clear();
            return copy;
        }

        // ───────────── 입력 ─────────────

        public CommandResult Execute(BattleCommand command)
        {
            if (IsOver)
            {
                return CommandResult.BattleOver;
            }
            CommandResult result;
            switch (command)
            {
                case SpinCommand _:
                    result = DoSpin();
                    break;
                case RespinCommand respin:
                    result = DoRespin(respin);
                    break;
                case ConfirmSpinCommand _:
                    result = DoConfirm();
                    break;
                case PlaceUnitCommand place:
                    result = DoPlace(place);
                    break;
                case UseCommanderSkillCommand skill:
                    result = DoSkill(skill);
                    break;
                default:
                    result = CommandResult.UnknownCommand;
                    break;
            }
            if (result == CommandResult.Ok)
            {
                _log.Add(new LoggedCommand(CurrentTick, command));
            }
            return result;
        }

        private CommandResult DoSpin()
        {
            if (PendingSpin != null)
            {
                return CommandResult.SpinPending;
            }
            if (_queue.Count >= QueueCapacity)
            {
                return CommandResult.QueueFull;
            }
            if (!TrySpendMana(_data.Slot.SpinCost))
            {
                return CommandResult.NotEnoughMana;
            }
            var symbols = _slot.Spin();
            PendingSpin = new SpinResult(symbols, _data.MaxTier);
            for (int i = 0; i < symbols.Length; i++)
            {
                Emit(new ReelStoppedEvent(CurrentTick, i, symbols[i], false));
            }
            if (PendingSpin.IsJackpot)
            {
                Emit(new JackpotEvent(CurrentTick, symbols[0]));
            }
            return CommandResult.Ok;
        }

        private CommandResult DoRespin(RespinCommand command)
        {
            if (PendingSpin == null)
            {
                return CommandResult.NoPendingSpin;
            }
            if (PendingSpin.RespinsUsed >= _data.Slot.RespinsPerSpin)
            {
                return CommandResult.RespinLimitReached;
            }
            var locks = command.LockedReels;
            int reelCount = _slot.ReelCount;
            if (locks == null || locks.Length < 1 || locks.Length > _data.Slot.MaxLockedReels || locks.Length >= reelCount)
            {
                return CommandResult.InvalidLock;
            }
            var locked = new bool[reelCount];
            foreach (int reel in locks)
            {
                if (reel < 0 || reel >= reelCount || locked[reel])
                {
                    return CommandResult.InvalidLock;
                }
                locked[reel] = true;
            }
            if (!TrySpendMana(_data.Slot.RespinCost))
            {
                return CommandResult.NotEnoughMana;
            }
            var symbols = _slot.Respin(PendingSpin.SymbolArray, locked);
            PendingSpin.Set(symbols, _data.MaxTier);
            PendingSpin.RespinsUsed++;
            for (int i = 0; i < symbols.Length; i++)
            {
                if (!locked[i])
                {
                    Emit(new ReelStoppedEvent(CurrentTick, i, symbols[i], true));
                }
            }
            if (PendingSpin.IsJackpot)
            {
                Emit(new JackpotEvent(CurrentTick, symbols[0]));
            }
            return CommandResult.Ok;
        }

        private CommandResult DoConfirm()
        {
            if (PendingSpin == null)
            {
                return CommandResult.NoPendingSpin;
            }
            var summons = PendingSpin.Summons;
            if (summons.Count > QueueFreeSlots)
            {
                return CommandResult.NotEnoughQueueSpace;
            }
            foreach (var entry in summons)
            {
                _queue.Add(new QueuedUnit(_deck[entry.Symbol], entry.Tier));
            }
            PendingSpin = null;
            Emit(new UnitsQueuedEvent(CurrentTick, summons.Count));
            return CommandResult.Ok;
        }

        private CommandResult DoPlace(PlaceUnitCommand command)
        {
            if (command.QueueIndex < 0 || command.QueueIndex >= _queue.Count)
            {
                return CommandResult.InvalidQueueIndex;
            }
            if (command.Lane < 0 || command.Lane >= _cfg.LaneCount)
            {
                return CommandResult.InvalidLane;
            }
            if (float.IsNaN(command.Position) || command.Position < 0f || command.Position > _cfg.PlayerZoneLength)
            {
                return CommandResult.InvalidPosition;
            }
            var queued = _queue[command.QueueIndex];
            _queue.RemoveAt(command.QueueIndex);
            SpawnGroup(queued.Def, queued.Tier, Side.Player, command.Lane, command.Position, 1f);
            return CommandResult.Ok;
        }

        private CommandResult DoSkill(UseCommanderSkillCommand command)
        {
            var skill = _cfg.CommanderSkill;
            if (command.Lane < 0 || command.Lane >= _cfg.LaneCount)
            {
                return CommandResult.InvalidLane;
            }
            if (float.IsNaN(command.Position) || command.Position < 0f || command.Position > _cfg.LaneLength)
            {
                return CommandResult.InvalidPosition;
            }
            if (SkillCooldownTicks > 0)
            {
                return CommandResult.SkillOnCooldown;
            }
            if (!TrySpendMana(skill.ManaCost))
            {
                return CommandResult.NotEnoughMana;
            }
            SkillCooldownTicks = SecondsToTicks(skill.CooldownSeconds);
            int stunTicks = SecondsToTicks(skill.StunSeconds);
            Emit(new CommanderSkillUsedEvent(CurrentTick, command.Lane, command.Position, skill.Radius));
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit.Side != Side.Enemy || unit.Lane != command.Lane || !unit.IsAlive)
                {
                    continue;
                }
                if (Math.Abs(unit.Y - command.Position) > skill.Radius)
                {
                    continue;
                }
                unit.Hp -= skill.Damage;
                unit.StunTicks = Math.Max(unit.StunTicks, stunTicks);
                Emit(new UnitHitEvent(CurrentTick, null, unit, skill.Damage));
            }
            RemoveDead();
            return CommandResult.Ok;
        }

        // ───────────── 진행 ─────────────

        public void Tick()
        {
            if (IsOver)
            {
                return;
            }

            RegenerateMana();
            if (SkillCooldownTicks > 0)
            {
                SkillCooldownTicks--;
            }
            SpawnScheduledEnemies();
            UpdateUnits();
            ApplyHits();
            RemoveDead();

            CurrentTick++;
            CheckOutcome();
        }

        private void RegenerateMana()
        {
            float multiplier = IsFinalPhase ? _cfg.FinalPhaseRegenMultiplier : 1f;
            int gain = (int)Math.Round(_cfg.ManaRegenPerSecond * multiplier * ManaScale / TickRate);
            _manaMilli = Math.Min(_maxManaMilli, _manaMilli + gain);
        }

        private void SpawnScheduledEnemies()
        {
            _dueSpawns.Clear();
            _schedule.TakeDue(CurrentTick, _dueSpawns);
            foreach (var entry in _dueSpawns)
            {
                float y = _cfg.LaneLength - entry.Distance;
                SpawnGroup(entry.Unit, entry.Tier, Side.Enemy, entry.Lane, y, _enemyStatMultiplier);
            }
        }

        private void UpdateUnits()
        {
            _pendingHits.Clear();
            _selfDestroyed.Clear();
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (!unit.IsAlive)
                {
                    continue;
                }
                if (unit.StunTicks > 0)
                {
                    unit.StunTicks--;
                    continue;
                }
                if (unit.CooldownTicks > 0)
                {
                    unit.CooldownTicks--;
                }

                Castle enemyCastle = unit.Side == Side.Player ? EnemyCastle : PlayerCastle;
                Unit target = FindTarget(unit);
                bool castleInRange = target == null && Math.Abs(enemyCastle.Y - unit.Y) <= unit.Range;

                if (target == null && !castleInRange)
                {
                    Move(unit, enemyCastle);
                    continue;
                }
                if (unit.CooldownTicks > 0 || unit.Attack <= 0f)
                {
                    continue;
                }

                _ctx.Reset(unit, enemyCastle);
                if (target != null)
                {
                    _ctx.UnitHits.Add(target);
                    _ctx.ImpactY = target.Y;
                }
                else
                {
                    _ctx.HitsCastle = true;
                    _ctx.ImpactY = enemyCastle.Y;
                }
                for (int h = 0; h < unit.Handlers.Length; h++)
                {
                    unit.Handlers[h].ModifyAttack(this, _ctx);
                }
                if (_ctx.IsExplosion)
                {
                    Emit(new ExplosionEvent(CurrentTick, unit, unit.Lane, _ctx.ImpactY, _ctx.ExplosionRadius));
                }
                for (int h = 0; h < _ctx.UnitHits.Count; h++)
                {
                    _pendingHits.Add(new PendingHit(unit, _ctx.UnitHits[h], null, unit.Attack));
                }
                if (_ctx.HitsCastle)
                {
                    _pendingHits.Add(new PendingHit(unit, null, enemyCastle, unit.Attack));
                }
                if (_ctx.SelfDestroy)
                {
                    _selfDestroyed.Add(unit);
                }
                unit.CooldownTicks = unit.AttackIntervalTicks;
            }

            CastleFire(PlayerCastle);
            CastleFire(EnemyCastle);
        }

        /// <summary>성 포격: 사거리 안의 가장 가까운 적 유닛 하나(두 라인, 공중 포함). 거리가 같으면 먼저 나온 유닛.</summary>
        private void CastleFire(Castle castle)
        {
            var defense = _cfg.CastleDefense;
            if (defense == null || defense.Damage <= 0f)
            {
                return;
            }
            if (castle.CooldownTicks > 0)
            {
                castle.CooldownTicks--;
            }
            if (castle.CooldownTicks > 0)
            {
                return;
            }
            Unit target = FindCastleTarget(castle);
            if (target == null)
            {
                return;
            }
            _pendingHits.Add(new PendingHit(null, target, null, defense.Damage, castle));
            castle.CooldownTicks = Math.Max(1, SecondsToTicks(defense.AttackInterval));
        }

        public Unit FindCastleTarget(Castle castle)
        {
            float range = _cfg.CastleDefense != null ? _cfg.CastleDefense.Range : 0f;
            Unit best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit.Side == castle.Side || !unit.IsAlive)
                {
                    continue;
                }
                float distance = Math.Abs(unit.Y - castle.Y);
                if (distance <= range && distance < bestDistance)
                {
                    best = unit;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void Move(Unit unit, Castle enemyCastle)
        {
            float step = unit.MoveSpeed / TickRate * unit.Direction;
            float y = unit.Y + step;
            // 성을 지나치지 않는다.
            if (unit.Direction > 0)
            {
                y = Math.Min(y, enemyCastle.Y);
            }
            else
            {
                y = Math.Max(y, enemyCastle.Y);
            }
            unit.Y = y;
        }

        /// <summary>같은 라인에서 사거리 안에 있고 공격할 수 있는 가장 가까운 적. 거리가 같으면 먼저 나온 유닛.</summary>
        public Unit FindTarget(Unit unit)
        {
            Unit best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _units.Count; i++)
            {
                var other = _units[i];
                if (other.Side == unit.Side || other.Lane != unit.Lane || !other.IsAlive)
                {
                    continue;
                }
                float distance = Math.Abs(other.Y - unit.Y);
                if (distance > unit.Range || distance >= bestDistance)
                {
                    continue;
                }
                if (!CanAttack(unit, other))
                {
                    continue;
                }
                best = other;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>attacker가 target을 공격할 수 있는가(키워드 규칙).</summary>
        public static bool CanAttack(Unit attacker, Unit target)
        {
            var handlers = target.Handlers;
            for (int i = 0; i < handlers.Length; i++)
            {
                if (!handlers[i].CanBeTargetedBy(target, attacker))
                {
                    return false;
                }
            }
            return true;
        }

        internal void CollectEnemiesInRadius(Unit attacker, float centerY, float radius, AttackContext ctx)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var other = _units[i];
                if (other.Side == attacker.Side || other.Lane != attacker.Lane || !other.IsAlive)
                {
                    continue;
                }
                if (Math.Abs(other.Y - centerY) > radius || ctx.Contains(other) || !CanAttack(attacker, other))
                {
                    continue;
                }
                ctx.UnitHits.Add(other);
            }
        }

        private void ApplyHits()
        {
            for (int i = 0; i < _pendingHits.Count; i++)
            {
                var hit = _pendingHits[i];
                if (hit.Target != null)
                {
                    hit.Target.Hp -= hit.Damage;
                    Emit(new UnitHitEvent(CurrentTick, hit.Attacker, hit.Target, hit.Damage, hit.SourceCastle));
                }
                else
                {
                    hit.Castle.Hp -= hit.Damage;
                    Emit(new CastleDamagedEvent(CurrentTick, hit.Castle, hit.Attacker, hit.Damage));
                }
            }
            for (int i = 0; i < _selfDestroyed.Count; i++)
            {
                _selfDestroyed[i].Hp = 0f;
            }
        }

        private void RemoveDead()
        {
            bool any = false;
            for (int i = 0; i < _units.Count; i++)
            {
                if (!_units[i].IsAlive)
                {
                    any = true;
                    Emit(new UnitDiedEvent(CurrentTick, _units[i]));
                }
            }
            if (any)
            {
                _units.RemoveAll(u => !u.IsAlive);
            }
        }

        private void CheckOutcome()
        {
            bool playerDown = PlayerCastle.IsDestroyed;
            bool enemyDown = EnemyCastle.IsDestroyed;
            if (playerDown || enemyDown)
            {
                if (playerDown && enemyDown)
                {
                    End(BattleOutcome.Draw);
                }
                else
                {
                    End(enemyDown ? BattleOutcome.Win : BattleOutcome.Lose);
                }
                return;
            }
            if (CurrentTick >= DurationTicks)
            {
                EndedByTimeout = true;
                float mine = PlayerCastle.HpRatio;
                float theirs = EnemyCastle.HpRatio;
                if (mine > theirs)
                {
                    End(BattleOutcome.Win);
                }
                else if (mine < theirs)
                {
                    End(BattleOutcome.Lose);
                }
                else
                {
                    End(BattleOutcome.Draw);
                }
            }
        }

        private void End(BattleOutcome outcome)
        {
            Outcome = outcome;
            PendingSpin = null;
            Emit(new BattleEndedEvent(CurrentTick, outcome));
        }

        // ───────────── 소환 ─────────────

        private List<Unit> SpawnGroup(UnitDef def, int tier, Side side, int lane, float y, float statMultiplier)
        {
            var spawned = new List<Unit>();
            var handlers = KeywordRegistry.Resolve(def);
            int count = 1;
            for (int i = 0; i < handlers.Length; i++)
            {
                count = Math.Max(count, handlers[i].SpawnCount(def));
            }
            float tierMultiplier = _data.TierMultiplier(tier);
            float hp = def.Hp * tierMultiplier * statMultiplier;
            float attack = def.Attack * tierMultiplier * statMultiplier;
            float range = def.Melee ? _cfg.MeleeRange : def.Range;
            int interval = Math.Max(1, SecondsToTicks(def.AttackInterval));
            int direction = side == Side.Player ? 1 : -1;
            for (int i = 0; i < count; i++)
            {
                // 묶음은 뒤쪽(내 성 쪽)으로 간격을 두고 늘어선다.
                float unitY = Clamp(y - direction * i * _cfg.SwarmSpacing, 0f, _cfg.LaneLength);
                var unit = new Unit(_nextUnitId++, def, side, tier, lane, unitY, hp, attack, range, interval, handlers);
                _units.Add(unit);
                spawned.Add(unit);
                Emit(new UnitSummonedEvent(CurrentTick, unit));
            }
            return spawned;
        }

        // ───────────── 테스트 전용 ─────────────
        // 테스트 프로젝트는 Core 소스를 직접 포함하므로 internal에 접근할 수 있다.

        internal List<Unit> SpawnForTest(string unitId, int tier, Side side, int lane, float y)
        {
            return SpawnGroup(_data.GetUnit(unitId), tier, side, lane, y, side == Side.Enemy ? _enemyStatMultiplier : 1f);
        }

        internal void SetManaForTest(float mana)
        {
            _manaMilli = ToMilli(mana);
        }

        internal void AddToQueueForTest(string unitId, int tier)
        {
            _queue.Add(new QueuedUnit(_data.GetUnit(unitId), tier));
        }

        // ───────────── 도우미 ─────────────

        private bool TrySpendMana(int amount)
        {
            int cost = amount * ManaScale;
            if (_manaMilli < cost)
            {
                return false;
            }
            _manaMilli -= cost;
            return true;
        }

        private void Emit(BattleEvent e)
        {
            if (EventsEnabled)
            {
                _events.Add(e);
            }
        }

        private static int ToMilli(float value)
        {
            return (int)Math.Round(value * ManaScale);
        }

        private static int SecondsToTicks(float seconds)
        {
            return (int)Math.Round(seconds * TickRate);
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        /// <summary>
        /// 전체 상태를 요약한 값. 같은 시드와 같은 입력이면 같은 값이 나와야 한다(결정성 검사용).
        /// </summary>
        public ulong ComputeStateHash()
        {
            ulong hash = 14695981039346656037UL;
            hash = Fnv(hash, (ulong)CurrentTick);
            hash = Fnv(hash, (ulong)_manaMilli);
            hash = Fnv(hash, (ulong)SkillCooldownTicks);
            hash = Fnv(hash, (ulong)Outcome);
            hash = Fnv(hash, FloatBits(PlayerCastle.Hp));
            hash = Fnv(hash, FloatBits(EnemyCastle.Hp));
            hash = Fnv(hash, (ulong)PlayerCastle.CooldownTicks);
            hash = Fnv(hash, (ulong)EnemyCastle.CooldownTicks);
            hash = Fnv(hash, _slotRng.State);
            hash = Fnv(hash, (ulong)_queue.Count);
            foreach (var q in _queue)
            {
                hash = Fnv(hash, (ulong)_deck.IndexOf(q.Def));
                hash = Fnv(hash, (ulong)q.Tier);
            }
            foreach (var unit in _units)
            {
                hash = Fnv(hash, (ulong)unit.Id);
                hash = Fnv(hash, FloatBits(unit.Y));
                hash = Fnv(hash, FloatBits(unit.Hp));
                hash = Fnv(hash, (ulong)unit.CooldownTicks);
                hash = Fnv(hash, (ulong)unit.StunTicks);
            }
            return hash;
        }

        private static ulong FloatBits(float value)
        {
            return (ulong)BitConverter.SingleToInt32Bits(value);
        }

        private static ulong Fnv(ulong hash, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                hash ^= (value >> (i * 8)) & 0xFF;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        private struct PendingHit
        {
            public PendingHit(Unit attacker, Unit target, Castle castle, float damage, Castle sourceCastle = null)
            {
                Attacker = attacker;
                Target = target;
                Castle = castle;
                Damage = damage;
                SourceCastle = sourceCastle;
            }

            public readonly Unit Attacker;
            public readonly Unit Target;
            public readonly Castle Castle;
            public readonly float Damage;
            public readonly Castle SourceCastle;
        }
    }
}
