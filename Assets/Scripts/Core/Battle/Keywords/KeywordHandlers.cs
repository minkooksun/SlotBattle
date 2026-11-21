using System;
using System.Collections.Generic;
using SlotBattle.Core.Data;

namespace SlotBattle.Core.Battle.Keywords
{
    /// <summary>units.json의 keywords에 쓰는 이름.</summary>
    public static class KeywordNames
    {
        /// <summary>공중: antiAir 키워드가 있는 유닛에게만 공격받는다.</summary>
        public const string Air = "air";
        /// <summary>대공: 공중 유닛을 공격할 수 있다.</summary>
        public const string AntiAir = "antiAir";
        /// <summary>
        /// 범위: 근접 계열 공격이 맞은 지점 반경 splashRadius 안의 적에게도 같은 피해를 준다.
        /// 곡사탄과 자폭은 공격 방식(attackType) 자체가 범위 피해라 이 키워드를 쓰지 않는다.
        /// </summary>
        public const string Splash = "splash";
        /// <summary>물량: 한 묶음이 swarmCount기로 나온다.</summary>
        public const string Swarm = "swarm";
        /// <summary>방어진: 놓은 자리에서 움직이지 않는다.</summary>
        public const string Hold = "hold";
        /// <summary>도발: 사거리 안의 적이 이 유닛을 먼저 노린다.</summary>
        public const string Taunt = "taunt";
        /// <summary>돌격: 앞에 적이 보이면 빨라지고, 달려든 뒤 첫 타격이 세다(battle.json의 charge).</summary>
        public const string Charge = "charge";
    }

    /// <summary>한 번의 공격이 누구에게 들어가는지 모으는 작업 공간.</summary>
    internal sealed class AttackContext
    {
        public readonly List<Unit> UnitHits = new List<Unit>(8);
        public Unit Attacker;
        public Castle EnemyCastle;
        public bool HitsCastle;
        public float ImpactY;
        public bool IsExplosion;
        public float ExplosionRadius;

        public void Reset(Unit attacker, Castle enemyCastle)
        {
            UnitHits.Clear();
            Attacker = attacker;
            EnemyCastle = enemyCastle;
            HitsCastle = false;
            ImpactY = attacker.Y;
            IsExplosion = false;
            ExplosionRadius = 0f;
        }

        public bool Contains(Unit unit)
        {
            for (int i = 0; i < UnitHits.Count; i++)
            {
                if (UnitHits[i] == unit)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>키워드 하나의 규칙. 새 특성은 처리기를 하나 추가하고 레지스트리에 등록한다.</summary>
    internal abstract class KeywordHandler
    {
        public abstract string Keyword { get; }

        /// <summary>배치 한 번에 전장에 나오는 기체 수.</summary>
        public virtual int SpawnCount(UnitDef def)
        {
            return 1;
        }

        /// <summary>이 키워드를 가진 유닛(self)을 attacker가 공격할 수 있는가.</summary>
        public virtual bool CanBeTargetedBy(Unit self, Unit attacker)
        {
            return true;
        }

        /// <summary>스스로 움직일 수 있는가(방어진은 false).</summary>
        public virtual bool CanMove => true;

        /// <summary>적이 이 유닛을 먼저 노리는가(도발).</summary>
        public virtual bool Taunts => false;

        /// <summary>돌격하는가.</summary>
        public virtual bool Charges => false;

        /// <summary>근접 계열 공격이 정해진 뒤 맞는 대상을 바꾸거나 더한다(탄 공격에는 쓰지 않는다).</summary>
        public virtual void ModifyAttack(BattleSimulation sim, AttackContext ctx)
        {
        }
    }

    internal sealed class AirHandler : KeywordHandler
    {
        public override string Keyword => KeywordNames.Air;

        public override bool CanBeTargetedBy(Unit self, Unit attacker)
        {
            return attacker.IsAntiAir;
        }
    }

    /// <summary>대공은 AirHandler가 읽는 표식이라 자체 동작이 없다.</summary>
    internal sealed class AntiAirHandler : KeywordHandler
    {
        public override string Keyword => KeywordNames.AntiAir;
    }

    internal sealed class SplashHandler : KeywordHandler
    {
        public override string Keyword => KeywordNames.Splash;

        public override void ModifyAttack(BattleSimulation sim, AttackContext ctx)
        {
            float radius = ctx.Attacker.Def.SplashRadius;
            sim.CollectEnemiesInRadius(ctx.Attacker, ctx.ImpactY, radius, ctx);
            ctx.IsExplosion = true;
            ctx.ExplosionRadius = radius;
        }
    }

    internal sealed class SwarmHandler : KeywordHandler
    {
        public override string Keyword => KeywordNames.Swarm;

        public override int SpawnCount(UnitDef def)
        {
            return Math.Max(1, def.SwarmCount);
        }
    }

    internal sealed class HoldHandler : KeywordHandler
    {
        public override string Keyword => KeywordNames.Hold;
        public override bool CanMove => false;
    }

    internal sealed class TauntHandler : KeywordHandler
    {
        public override string Keyword => KeywordNames.Taunt;
        public override bool Taunts => true;
    }

    internal sealed class ChargeHandler : KeywordHandler
    {
        public override string Keyword => KeywordNames.Charge;
        public override bool Charges => true;
    }

    public static class KeywordRegistry
    {
        private static readonly Dictionary<string, KeywordHandler> Handlers = Build();

        public static bool IsKnown(string keyword)
        {
            return Handlers.ContainsKey(keyword);
        }

        public static IEnumerable<string> All => Handlers.Keys;

        internal static KeywordHandler[] Resolve(UnitDef def)
        {
            var result = new KeywordHandler[def.Keywords.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Handlers[def.Keywords[i]];
            }
            return result;
        }

        private static Dictionary<string, KeywordHandler> Build()
        {
            var list = new KeywordHandler[]
            {
                new AirHandler(),
                new AntiAirHandler(),
                new SplashHandler(),
                new SwarmHandler(),
                new HoldHandler(),
                new TauntHandler(),
                new ChargeHandler(),
            };
            var map = new Dictionary<string, KeywordHandler>();
            foreach (var handler in list)
            {
                map.Add(handler.Keyword, handler);
            }
            return map;
        }
    }
}
