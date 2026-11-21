using System;
using System.Collections.Generic;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlotBattle.Presentation
{
    /// <summary>
    /// 전장(설계서 4장 "3D 전장"): 3D 카메라가 그린 그림을 캔버스의 전장 자리에 놓고,
    /// 그 위에 체력 막대, 피해 숫자, 2D 효과, 배치·조준 표시를 UI로 그린다.
    /// Core 상태를 그리기만 하고 규칙 판단은 하지 않는다.
    /// 로컬 좌표: 전장 그림 안의 pt(왼쪽 아래 원점).
    /// </summary>
    public sealed class BattlefieldView
    {
        private const float TickDt = 1f / BattleSimulation.TickRate;

        private readonly FeelConfig _feel;
        private readonly RectTransform _root;
        private readonly RectTransform _content;
        private readonly RawImage _image;
        private readonly World3D _world;
        private readonly RectTransform _unitsLayer;
        private readonly Image _dropMarker;
        private readonly Image _aimRing;
        private readonly Image _aimCatcher;
        private readonly Text _aimHint;
        private readonly Dictionary<Unit, UnitView> _views = new Dictionary<Unit, UnitView>();
        private readonly Stack<UnitView> _pool = new Stack<UnitView>();
        private readonly List<Unit> _removeBuffer = new List<Unit>();
        private readonly HashSet<Unit> _dying = new HashSet<Unit>();
        private readonly Dictionary<int, ProjectileTrack> _tracks = new Dictionary<int, ProjectileTrack>();
        private readonly List<ProjectileView3D> _projectiles = new List<ProjectileView3D>();
        private float _shake;
        private float _enemyCastleFlash;
        private float _playerCastleFlash;
        private bool _enemyDestroyed;
        private bool _playerDestroyed;
        private float _time;

        public BattlefieldView(Transform parent, FeelConfig feel)
        {
            _feel = feel;
            _root = Ui.CreateRect("Battlefield", parent);
            _content = Ui.CreateRect("Content", _root);
            _content.anchorMin = Vector2.zero;
            _content.anchorMax = Vector2.one;
            _content.sizeDelta = Vector2.zero;

            // 3D 전장은 UI와 떨어진 곳에 만들고(호스트 아래), 그 그림만 여기에 놓는다.
            _world = new World3D(parent.root, feel.World);
            var imageRect = Ui.CreateRect("WorldView", _content);
            _image = imageRect.gameObject.AddComponent<RawImage>();
            _image.raycastTarget = false;
            _image.color = Color.white;

            _unitsLayer = Ui.CreateRect("UnitBars", _content);
            _unitsLayer.anchorMin = Vector2.zero;
            _unitsLayer.anchorMax = Vector2.one;
            _unitsLayer.sizeDelta = Vector2.zero;

            _dropMarker = Ui.CreateImage("DropMarker", _content, Shape.Ring, Ui.WithAlpha(Palette.Accent, 0.95f));
            _dropMarker.gameObject.SetActive(false);

            Fx = new FxLayer("FieldFx", _content);

            _aimRing = Ui.CreateImage("AimRing", _content, Shape.Ring, Ui.WithAlpha(Palette.Energy, 0.95f));
            _aimRing.gameObject.SetActive(false);
            _aimHint = Ui.CreateText("AimHint", _content, "EMP: 터뜨릴 곳을 누르세요", 15, Palette.Energy);
            _aimHint.gameObject.SetActive(false);

            // EMP 조준 중에만 켜지는 입력 받개.
            _aimCatcher = Ui.CreateImage("AimInput", _root, Shape.Square, new Color(0, 0, 0, 0), true);
            Ui.Fill(_aimCatcher.rectTransform);
            AimInput = _aimCatcher.gameObject.AddComponent<FieldPointerInput>();
            _aimCatcher.gameObject.SetActive(false);
        }

        public RectTransform Root => _root;
        public World3D World => _world;
        public FxLayer Fx { get; }
        public FieldPointerInput AimInput { get; }
        public Vector2 Size { get; private set; }
        public bool ShakeEnabled { get; set; } = true;
        public int ViewCount => _views.Count;
        /// <summary>Core에서는 죽었지만 준비 동작만큼 늦춰 아직 화면에 남아 있는 유닛 수.</summary>
        public int DyingCount => _dying.Count;
        /// <summary>화면에 날고 있는 탄 수.</summary>
        public int FlyingProjectileCount
        {
            get
            {
                int count = 0;
                foreach (var view in _projectiles)
                {
                    if (view.Active)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public void Configure(BattleSimulation sim)
        {
            _world.Configure(sim.Config.LaneLength, sim.Config.PlayerZoneLength, sim.Config.LaneCount);
        }

        /// <summary>전장 자리(pt)와 화면 배율(@2x, @3x)에 맞춰 3D 그림의 해상도를 정한다.</summary>
        public void Layout(Rect rect, float pointScale)
        {
            Ui.Place(_root, rect);
            Size = rect.size;
            Ui.Place(_image.rectTransform, new Rect(0f, 0f, Size.x, Size.y));
            _world.Resize(rect.size, pointScale);
            _image.texture = _world.Texture;
            Ui.PlaceCentered(_aimHint.rectTransform, new Vector2(Size.x / 2f, Size.y - 30f), new Vector2(Size.x, 24f));
        }

        // ───────────── 좌표 ─────────────

        /// <summary>라인과 거리(Core 좌표)의 바닥 점이 전장 그림에서 보이는 곳(pt).</summary>
        public Vector2 ToLocal(int lane, float laneY)
        {
            return _world.ToLocal(_world.Ground(lane, laneY));
        }

        /// <summary>그 바닥 점에서 전장 거리 1이 몇 pt인가(원근이라 멀수록 작다).</summary>
        public float PointsPerUnit(int lane, float laneY)
        {
            return _world.PointsPerUnit(_world.Ground(lane, laneY));
        }

        /// <summary>전장 그림 안의 점을 라인과 거리로 바꾼다(바닥에 쏜 광선). 전장 밖이면 false.</summary>
        public bool TryFromLocal(Vector2 local, out int lane, out float laneY)
        {
            return _world.TryFromLocal(local, out lane, out laneY);
        }

        public bool ScreenToLocal(Vector2 screenPoint, Camera camera, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPoint, camera, out local);
        }

        // ───────────── 유닛 ─────────────

        public void AddUnit(Unit unit, bool bigDrop)
        {
            if (_views.ContainsKey(unit))
            {
                return;
            }
            var view = _pool.Count > 0 ? _pool.Pop() : new UnitView(_unitsLayer);
            view.Bind(unit, this, _feel, bigDrop);
            _views[unit] = view;
        }

        public bool TryGetView(Unit unit, out UnitView view)
        {
            return _views.TryGetValue(unit, out view);
        }

        /// <summary>틱 직전 위치를 기억해 틱 사이를 부드럽게 잇는다.</summary>
        public void BeginTick(BattleSimulation sim)
        {
            foreach (var unit in sim.Units)
            {
                if (_views.TryGetValue(unit, out var view))
                {
                    view.PrevY = unit.Y;
                }
            }
        }

        /// <summary>
        /// Core에서 피해가 났지만 아직 보여 주지 않은 몫. 준비 동작만큼 늦춰 보여 주는 동안
        /// 체력 막대가 먼저 줄지 않게 한다(설계서 8장 "유닛별 공격 연출").
        /// </summary>
        public void AddPendingDamage(Unit unit, float damage)
        {
            if (_views.TryGetValue(unit, out var view))
            {
                view.PendingDamage += damage;
            }
        }

        public void OnHit(UnitHitEvent hit)
        {
            if (!_views.TryGetValue(hit.Target, out var view))
            {
                return;
            }
            view.PendingDamage = Mathf.Max(0f, view.PendingDamage - hit.Damage);
            view.OnHit(_feel);
            var color = hit.Target.Side == Side.Player ? Ui.Hex("#FFB4B4") : Color.white;
            if (hit.Attacker == null && hit.SourceCastle == null)
            {
                color = Palette.Energy;
            }
            if (hit.Attacker != null && hit.Attacker.Def.Melee && hit.Attacker.Def.AttackType != AttackType.Explode)
            {
                // 근접 타격 불꽃(탄은 도착할 때 따로 불꽃을 낸다).
                var spark = Color.Lerp(UnitStyle.Get(hit.Attacker.Def.Id).Color, Color.white, 0.5f);
                Fx.Debris(view.DisplayPosition, spark, _feel.Attack.SparkCount, _feel.Attack.SparkSpeed, _feel.Attack.SparkTime, 5f);
            }
            float scale = hit.Attacker != null && hit.Attacker.Tier >= 3 ? 1.5f : 1f;
            if (hit.Attacker != null && hit.Attacker.Def.IsStrongAgainst(hit.Target.Def))
            {
                // 상성 피해는 크게, 노랗게 보여 준다.
                color = Palette.Energy;
                scale *= 1.3f;
            }
            Fx.DamageNumber(view.HeadPosition, hit.Damage, color, _feel.Hit.DamageNumberRise, _feel.Hit.DamageNumberTime, scale);
            if (hit.Attacker != null && hit.Attacker.Tier >= 3)
            {
                Shake(_feel.Shake.Tier3Attack);
            }
        }

        /// <summary>Core에서 죽었지만 사망 연출을 늦춰 보여 줄 유닛. 그동안 화면에서 지우지 않는다.</summary>
        public void MarkDying(Unit unit)
        {
            if (_views.ContainsKey(unit))
            {
                _dying.Add(unit);
            }
        }

        public void OnDied(Unit unit)
        {
            _dying.Remove(unit);
            if (!_views.TryGetValue(unit, out var view))
            {
                return;
            }
            var style = UnitStyle.Get(unit.Def.Id);
            Fx.Debris(view.DisplayPosition, style.Color, _feel.Death.DebrisCount + unit.Tier * 2, _feel.Death.DebrisSpeed, _feel.Death.DebrisTime, 7f);
            Fx.Debris(view.DisplayPosition, RobotFactory.Metal, _feel.Death.DebrisCount / 2 + 1, _feel.Death.DebrisSpeed * 0.8f, _feel.Death.DebrisTime, 5f);
            view.Release();
            _views.Remove(unit);
            _pool.Push(view);
        }

        public void OnCastleDamaged(Side side)
        {
            if (side == Side.Enemy)
            {
                _enemyCastleFlash = 0.12f;
            }
            else
            {
                _playerCastleFlash = 0.12f;
            }
        }

        public Vector2 CastleCenter(Side side)
        {
            return _world.ToLocal(_world.CastleCenter(side == Side.Enemy));
        }

        public void CastleDestroyed(Side side)
        {
            bool enemy = side == Side.Enemy;
            var color = enemy ? Palette.Enemy : Palette.Player;
            var centerWorld = _world.CastleCenter(enemy);
            float width = _world.FieldWidth;
            for (int i = 0; i < 5; i++)
            {
                var p = _world.ToLocal(centerWorld + new Vector3((i - 2) * width * 0.2f, 0f, 0f));
                Fx.Debris(p, color, _feel.Death.CastleDebrisCount / 5 + 1, _feel.Death.DebrisSpeed * 2f, _feel.Death.DebrisTime * 2.5f, 10f);
                Fx.Debris(p, Palette.Energy, 3, _feel.Death.DebrisSpeed * 1.5f, _feel.Death.DebrisTime * 2f, 6f);
            }
            Fx.Ring(_world.ToLocal(centerWorld), 20f, Size.x * 1.4f, Ui.WithAlpha(Color.white, 0.9f), 0.8f);
            _world.SetCastleDestroyed(enemy, true);
            if (enemy)
            {
                _enemyDestroyed = true;
            }
            else
            {
                _playerDestroyed = true;
            }
            Shake(_feel.Shake.CastleDestroyed);
        }

        /// <summary>폭발: 고리의 크기는 Core가 알린 실제 피해 반경과 같다(원근으로 본 지름).</summary>
        public void Explosion(ExplosionEvent explosion)
        {
            var a = _feel.Attack;
            var ground = _world.Ground(explosion.Lane, explosion.Y);
            var center = _world.ToLocal(ground);
            float size = _world.ProjectedCircle(ground, explosion.Radius).x;
            var anim = explosion.Source != null ? UnitStyle.AttackOf(explosion.Source.Def) : AttackAnim.Slash;
            if (anim == AttackAnim.Burst)
            {
                var orange = Ui.Hex("#FFB347");
                Fx.Pop(Shape.Glow, Ui.WithAlpha(orange, 0.9f), center, Vector2.one * size * 0.3f, Vector2.one * size * 1.1f, 0f, a.ExplosionTime * 0.8f);
                Fx.Ring(center, size * 0.3f, size, Ui.WithAlpha(orange, 0.95f), a.ExplosionTime);
                Fx.Ring(center, size * 0.1f, size * 0.7f, Ui.WithAlpha(Color.white, 0.7f), a.ExplosionTime * 0.7f);
                Fx.Debris(center, orange, 10, a.SparkSpeed * 1.6f, a.ExplosionTime * 1.3f, 6f);
                Shake(a.ExplosionShake);
            }
            else if (anim == AttackAnim.Lob)
            {
                var color = Color.Lerp(UnitStyle.Get(explosion.Source.Def.Id).Color, Color.white, 0.3f);
                Fx.Pop(Shape.Glow, Ui.WithAlpha(color, 0.85f), center, Vector2.one * size * 0.2f, Vector2.one * size, 0f, a.ExplosionTime * 0.7f);
                Fx.Ring(center, size * 0.25f, size, Ui.WithAlpha(color, 0.95f), a.ExplosionTime);
                Fx.Debris(center, color, 6, a.SparkSpeed, a.ExplosionTime, 5f);
            }
            else
            {
                Fx.Ring(center, size * 0.3f, size, Ui.WithAlpha(Ui.Hex("#FFB347"), 0.9f), a.ExplosionTime);
            }
        }

        // ───────────── 공격 연출 ─────────────

        /// <summary>공격 시작: 준비 동작(칼을 치켜듦, 웅크림, 포구가 빛남, 포탑이 대상 쪽으로 돎)을 시작한다.</summary>
        public void BeginAttack(AttackStartedEvent attack, float windup)
        {
            if (attack.Attacker != null)
            {
                if (_views.TryGetValue(attack.Attacker, out var view))
                {
                    view.BeginAttack(UnitStyle.AttackOf(attack.Attacker.Def), windup);
                }
                return;
            }
            var turret = TurretFor(attack);
            if (turret != null)
            {
                var target = attack.TargetUnit != null && _views.TryGetValue(attack.TargetUnit, out var targetView)
                    ? targetView.WorldPosition
                    : _world.Ground(attack.TargetUnit != null ? attack.TargetUnit.Lane : 0, _world.LaneLength / 2f);
                turret.Aim(target, windup);
            }
        }

        /// <summary>준비 동작이 끝나는 순간: 휘두른 궤적·찌르기·돌진 잔상·포구 섬광. 피해와 탄은 사건으로 따로 보여 준다.</summary>
        public void Strike(AttackStartedEvent attack)
        {
            var a = _feel.Attack;
            if (attack.Attacker == null)
            {
                var turret = TurretFor(attack);
                if (turret != null)
                {
                    turret.Fire(a.TurretRecoil);
                    var color = Color.Lerp(attack.SourceCastle.Side == Side.Enemy ? Palette.Enemy : Palette.Player, Color.white, 0.5f);
                    var muzzle = _world.ToLocal(turret.Muzzle);
                    float s = _world.PointsPerUnit(turret.Muzzle);
                    Fx.Pop(Shape.Glow, Ui.WithAlpha(color, 0.95f), muzzle, Vector2.one * s * 0.3f, Vector2.one * s * a.MuzzleFlashSize * 1.2f, 0f, a.MuzzleFlashTime * 1.5f);
                }
                return;
            }
            if (!_views.TryGetValue(attack.Attacker, out var view))
            {
                return;
            }
            var style = UnitStyle.Get(attack.Attacker.Def.Id);
            float size = view.Size;
            int dir = attack.Attacker.Direction;
            var front = view.FrontPosition;
            float facing = dir < 0 ? 180f : 0f;
            var light = Color.Lerp(style.Color, Color.white, 0.6f);
            switch (UnitStyle.AttackOf(attack.Attacker.Def))
            {
                case AttackAnim.Slash:
                    // 휘두른 자리: 옅은 부채꼴 위에 밝은 호. 호의 가운데가 앞을 향한다.
                    Fx.Pop(Shape.Circle, Ui.WithAlpha(light, 0.35f), front, Vector2.one * size * a.SlashSize * 0.9f, Vector2.one * size * a.SlashSize * 1.1f,
                        a.SlashArc * 180f + facing, a.SlashTime, a.SlashArc);
                    Fx.Pop(Shape.Ring, Ui.WithAlpha(Color.white, 1f), front, Vector2.one * size * a.SlashSize, Vector2.one * size * a.SlashSize * 1.15f,
                        a.SlashArc * 180f + facing, a.SlashTime, a.SlashArc);
                    break;
                case AttackAnim.Bash:
                    Fx.Ring(front, size * 0.5f, size * a.BashRingSize, Ui.WithAlpha(Color.white, 0.8f), a.BashTime);
                    break;
                case AttackAnim.Stab:
                {
                    float length = size * a.StabLength;
                    Fx.Pop(Shape.Square, Ui.WithAlpha(light, 0.95f), front + new Vector2(0f, dir * length * 0.3f),
                        new Vector2(6f, length * 0.3f), new Vector2(3f, length * 0.6f), 0f, a.StabTime);
                    break;
                }
                case AttackAnim.Dash:
                    // 돌진 잔상: 뒤로 흐르는 속도선.
                    for (int i = 0; i < a.GhostCount; i++)
                    {
                        float x = (i - (a.GhostCount - 1) / 2f) * size * 0.35f;
                        var p = view.DisplayPosition + new Vector2(x, -dir * size * (0.6f + i * 0.15f));
                        Fx.Pop(Shape.Square, Ui.WithAlpha(light, 0.7f), p, new Vector2(4f, size * 0.4f), new Vector2(2f, size * 1.2f), 0f, a.GhostTime);
                    }
                    break;
                case AttackAnim.Shoot:
                case AttackAnim.Strafe:
                case AttackAnim.Lob:
                    Fx.Pop(Shape.Glow, Ui.WithAlpha(light, 0.95f), front, Vector2.one * size * 0.2f, Vector2.one * size * a.MuzzleFlashSize * 1.4f, 0f, a.MuzzleFlashTime);
                    Fx.Pop(Shape.Circle, Ui.WithAlpha(Color.white, 0.9f), front, Vector2.one * size * 0.15f, Vector2.one * size * a.MuzzleFlashSize * 0.6f, 0f, a.MuzzleFlashTime);
                    break;
                case AttackAnim.Burst:
                    // 폭발은 ExplosionEvent로 보여 준다.
                    break;
            }
        }

        private Turret3D TurretFor(AttackStartedEvent attack)
        {
            if (attack.SourceCastle == null)
            {
                return null;
            }
            int lane = attack.TargetUnit != null ? Mathf.Clamp(attack.TargetUnit.Lane, 0, 1) : 0;
            return attack.SourceCastle.Side == Side.Enemy ? _world.EnemyTurrets[lane] : _world.PlayerTurrets[lane];
        }

        // ───────────── 탄 ─────────────

        /// <summary>탄이 Core에서 발사됨: 이후 틱마다 Core의 탄 위치를 기록해 둔다.</summary>
        public void TrackLaunch(Projectile projectile, int tick)
        {
            var track = new ProjectileTrack { Projectile = projectile, LastTick = tick };
            track.Ys.Add(projectile.FromY);
            _tracks[projectile.Id] = track;
        }

        /// <summary>틱을 마칠 때마다 날고 있는 탄의 Core 위치를 기록한다.</summary>
        public void RecordProjectiles(BattleSimulation sim)
        {
            int tick = sim.CurrentTick - 1;
            var projectiles = sim.Projectiles;
            for (int i = 0; i < projectiles.Count; i++)
            {
                if (_tracks.TryGetValue(projectiles[i].Id, out var track) && track.LastTick < tick)
                {
                    track.Ys.Add(projectiles[i].Y);
                    track.LastTick = tick;
                }
            }
        }

        public void TrackArrival(Projectile projectile, int tick)
        {
            if (_tracks.TryGetValue(projectile.Id, out var track) && track.LastTick < tick)
            {
                track.Ys.Add(projectile.Y);
                track.LastTick = tick;
            }
        }

        /// <summary>
        /// 탄 발사를 보여 준다(준비 동작만큼 늦게). 화면의 탄은 발사 뒤 지난 시간만큼의 Core 기록 위치를 따라간다.
        /// late: 보여 줄 시각보다 이미 늦은 시간.
        /// </summary>
        public void ShowLaunch(Projectile projectile, float late)
        {
            if (!_tracks.TryGetValue(projectile.Id, out var track))
            {
                return;
            }
            ProjectileView3D view = null;
            foreach (var candidate in _projectiles)
            {
                if (!candidate.Active)
                {
                    view = candidate;
                    break;
                }
            }
            if (view == null)
            {
                view = new ProjectileView3D(_world.Root);
                _projectiles.Add(view);
            }
            var from = _world.Ground(projectile.Lane, projectile.FromY, _feel.World.CastleHeight + 0.5f);
            var kind = ProjectileKind.Shell;
            var color = Color.Lerp(projectile.Side == Side.Enemy ? Palette.Enemy : Palette.Player, Color.white, 0.4f);
            if (projectile.SourceCastle != null)
            {
                var turrets = projectile.SourceCastle.Side == Side.Enemy ? _world.EnemyTurrets : _world.PlayerTurrets;
                from = turrets[Mathf.Clamp(projectile.Lane, 0, 1)].Muzzle;
            }
            if (projectile.Attacker != null)
            {
                var style = UnitStyle.Get(projectile.Attacker.Def.Id);
                color = Color.Lerp(style.Color, Color.white, 0.45f);
                switch (UnitStyle.AttackOf(projectile.Attacker.Def))
                {
                    case AttackAnim.Lob: kind = ProjectileKind.Orb; break;
                    case AttackAnim.Strafe: kind = ProjectileKind.Burst; break;
                    default: kind = ProjectileKind.Laser; break;
                }
                from = _views.TryGetValue(projectile.Attacker, out var shooter)
                    ? shooter.MuzzleWorld
                    : _world.Ground(projectile.Lane, projectile.FromY, 1f);
            }
            track.View = view;
            view.Show(track, kind, color, from, late, _feel.World.ProjectileSize);
            RenderProjectile(view, 0f);
        }

        /// <summary>탄 도착을 보여 준다: 직사탄은 착탄 불꽃, 사라진 탄은 흩어짐, 성 포탄은 착탄 폭발.</summary>
        public void ShowArrival(ProjectileArrivedEvent arrived)
        {
            if (!_tracks.TryGetValue(arrived.Projectile.Id, out var track))
            {
                return;
            }
            _tracks.Remove(arrived.Projectile.Id);
            var view = track.View;
            if (view == null || !view.Active)
            {
                return;
            }
            // 마지막 기록 위치로 맞춘 뒤 터뜨린다.
            view.Elapsed = (track.Ys.Count - 1) * TickDt;
            RenderProjectile(view, 0f);
            var position = _world.ToLocal(view.Position);
            var color = view.Color;
            var kind = view.Kind;
            view.Hide();
            var a = _feel.Attack;
            if (!arrived.Hit)
            {
                Fx.Pop(Shape.Ring, Ui.WithAlpha(color, 0.6f), position, Vector2.one * 6f, Vector2.one * 18f, 0f, a.SparkTime);
                return;
            }
            switch (kind)
            {
                case ProjectileKind.Laser:
                case ProjectileKind.Burst:
                    Fx.Pop(Shape.Glow, Ui.WithAlpha(color, 0.9f), position, Vector2.one * 8f, Vector2.one * 30f, 0f, a.SparkTime);
                    Fx.Debris(position, color, a.SparkCount, a.SparkSpeed, a.SparkTime, 4f);
                    break;
                case ProjectileKind.Shell:
                {
                    float size = a.ShellSize;
                    Fx.Ring(position, size, size * 4f, Ui.WithAlpha(color, 0.9f), 0.25f);
                    Fx.Debris(position, color, a.SparkCount, a.SparkSpeed * 1.2f, a.SparkTime * 1.3f, 5f);
                    break;
                }
                case ProjectileKind.Orb:
                    // 범위 폭발은 ExplosionEvent로 보여 준다.
                    break;
            }
        }

        private void RenderProjectiles(float showDt)
        {
            foreach (var view in _projectiles)
            {
                if (view.Active)
                {
                    RenderProjectile(view, showDt);
                }
            }
        }

        /// <summary>Core가 기록한 탄의 거리(Y)를 따라 그린다. 높이와 좌우는 쏜 곳에서 대상 쪽으로 잇는다.</summary>
        private void RenderProjectile(ProjectileView3D view, float showDt)
        {
            view.Elapsed += showDt;
            var track = view.Track;
            var ys = track.Ys;
            float f = Mathf.Max(0f, view.Elapsed / TickDt);
            int i = (int)f;
            float y = i >= ys.Count - 1 ? ys[ys.Count - 1] : Mathf.Lerp(ys[i], ys[i + 1], f - i);
            var p = track.Projectile;
            float span = Mathf.Abs(p.TargetY - p.FromY);
            float progress = span > 0.01f ? Mathf.Clamp01(Mathf.Abs(y - p.FromY) / span) : 1f;
            if (p.TargetUnit != null && _views.TryGetValue(p.TargetUnit, out var target))
            {
                view.To = target.CenterWorld;
            }
            else if (!view.HasTarget)
            {
                view.To = _world.Ground(p.Lane, p.TargetY, p.TargetCastle != null ? _feel.World.CastleHeight * 0.6f : 0.4f);
            }
            float x = Mathf.Lerp(view.From.x, view.To.x, progress);
            float h = Mathf.Lerp(view.From.y, view.To.y, progress);
            if (view.Kind == ProjectileKind.Orb)
            {
                h += _feel.World.LobArcHeight * 4f * progress * (1f - progress);
            }
            view.Place(new Vector3(x, h, y));
        }

        public void SkillBlast(int lane, float laneY, float radius)
        {
            var ground = _world.Ground(lane, laneY);
            var center = _world.ToLocal(ground);
            float size = _world.ProjectedCircle(ground, radius).x;
            Fx.Ring(center, size * 0.2f, size, Ui.WithAlpha(Palette.Energy, 1f), 0.45f);
            Fx.Ring(center, size * 0.6f, size * 1.3f, Ui.WithAlpha(Color.white, 0.6f), 0.6f);
            Fx.Debris(center, Palette.Energy, 14, 180f, 0.5f, 5f);
            Shake(_feel.Shake.CastleHit * 1.5f);
        }

        /// <summary>3단계 유닛 착지: 충격파와 흔들림.</summary>
        public void LandingImpact(Vector2 position, float size)
        {
            Fx.Ring(position, size, size * 3f, Ui.WithAlpha(Color.white, 0.9f), 0.45f);
            Fx.Debris(position, Ui.Hex("#C8D2F0"), 10, 160f, 0.4f, 5f);
            Shake(_feel.Shake.JackpotLanding);
        }

        public void Shake(float amount)
        {
            if (ShakeEnabled)
            {
                _shake = Mathf.Max(_shake, amount);
            }
        }

        public void ResetCastles()
        {
            _enemyDestroyed = false;
            _playerDestroyed = false;
            _world.SetCastleDestroyed(true, false);
            _world.SetCastleDestroyed(false, false);
        }

        public void Clear()
        {
            foreach (var view in _views.Values)
            {
                view.Release();
                _pool.Push(view);
            }
            _views.Clear();
            _dying.Clear();
            _tracks.Clear();
            foreach (var view in _projectiles)
            {
                view.Hide();
            }
            Fx.Clear();
            _shake = 0f;
            ResetCastles();
            ShowDropPreview(false, 0, 0f, 0f);
            SetAiming(false);
        }

        /// <summary>
        /// 틱 사이 보간 비율(alpha)로 유닛을 그린다. dt는 연출 시간(느린 화면 포함),
        /// showDt는 늦춰 보여 주는 사건과 같은 시계(멈춤 중에는 0)로, 준비 동작과 탄 이동에 쓴다.
        /// </summary>
        public void Render(BattleSimulation sim, float alpha, float dt, float showDt, Action<UnitView> onLanded)
        {
            _time += showDt;
            if (_image.texture != _world.Texture)
            {
                _image.texture = _world.Texture;
            }
            _removeBuffer.Clear();
            foreach (var pair in _views)
            {
                if (!pair.Key.IsAlive && !_dying.Contains(pair.Key) && !ContainsUnit(sim, pair.Key))
                {
                    _removeBuffer.Add(pair.Key);
                    continue;
                }
                if (pair.Value.Render(this, alpha, dt, showDt, _time))
                {
                    onLanded?.Invoke(pair.Value);
                }
            }
            foreach (var unit in _removeBuffer)
            {
                OnDied(unit);
            }

            _enemyCastleFlash = Mathf.Max(0f, _enemyCastleFlash - dt);
            _playerCastleFlash = Mathf.Max(0f, _playerCastleFlash - dt);
            if (!_enemyDestroyed)
            {
                _world.FlashCastle(true, _enemyCastleFlash * 5f);
            }
            if (!_playerDestroyed)
            {
                _world.FlashCastle(false, _playerCastleFlash * 5f);
            }
            for (int i = 0; i < 2; i++)
            {
                _world.EnemyTurrets[i].Render(showDt, _feel.Attack.TurretTurnSpeed);
                _world.PlayerTurrets[i].Render(showDt, _feel.Attack.TurretTurnSpeed);
            }
            RenderProjectiles(showDt);

            if (_shake > 0.05f)
            {
                _content.anchoredPosition = UnityEngine.Random.insideUnitCircle * _shake;
                _shake *= Mathf.Exp(-_feel.Shake.Decay * dt);
            }
            else
            {
                _shake = 0f;
                _content.anchoredPosition = Vector2.zero;
            }
            Fx.Update(dt);
        }

        private static bool ContainsUnit(BattleSimulation sim, Unit unit)
        {
            var units = sim.Units;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] == unit)
                {
                    return true;
                }
            }
            return false;
        }

        // ───────────── 배치 미리보기와 조준 ─────────────

        /// <summary>끌어다 놓는 동안: 놓을 수 있는 영역(내 진영)과 사거리를 바닥에 보여 준다.</summary>
        public void ShowDropPreview(bool visible, int lane, float laneY, float range, bool valid = true)
        {
            _world.ShowDropZones(visible, visible ? lane : -1);
            bool show = visible && valid;
            _world.ShowRangeBand(show, lane, Mathf.Max(0f, laneY - range), Mathf.Min(_world.LaneLength, laneY + range));
            _dropMarker.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }
            var ground = _world.Ground(lane, laneY);
            Ui.PlaceCentered(_dropMarker.rectTransform, _world.ToLocal(ground), _world.ProjectedCircle(ground, 0.9f));
        }

        public void SetAiming(bool aiming)
        {
            _aimCatcher.gameObject.SetActive(aiming);
            _aimHint.gameObject.SetActive(aiming);
            if (!aiming)
            {
                _aimRing.gameObject.SetActive(false);
            }
        }

        public void ShowAim(int lane, float laneY, float radius)
        {
            _aimRing.gameObject.SetActive(true);
            var ground = _world.Ground(lane, laneY);
            Ui.PlaceCentered(_aimRing.rectTransform, _world.ToLocal(ground), _world.ProjectedCircle(ground, radius));
        }
    }

    /// <summary>전장 위 누르기·끌기·떼기를 넘겨준다(EMP 조준용).</summary>
    public sealed class FieldPointerInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<PointerEventData> Down;
        public Action<PointerEventData> Drag;
        public Action<PointerEventData> Up;

        public void OnPointerDown(PointerEventData eventData) => Down?.Invoke(eventData);
        public void OnDrag(PointerEventData eventData) => Drag?.Invoke(eventData);
        public void OnPointerUp(PointerEventData eventData) => Up?.Invoke(eventData);
    }

    /// <summary>
    /// 유닛 하나의 모습: 3D 로봇과 머리 위 체력 막대(UI). 단계가 높을수록 크고,
    /// 맞으면 흰색으로 번쩍이고 뒤로 살짝 밀리며, 공격 때는 준비 동작과 휘두르기를 한다.
    /// </summary>
    public sealed class UnitView
    {
        private readonly RectTransform _bar;
        private readonly Image _hpBack;
        private readonly Image _hpFill;
        private RobotModel _model;
        private BattlefieldView _field;
        private FeelConfig _feel;
        private float _worldSize;
        private float _jitterX;
        private float _flashT;
        private float _knockT;
        private float _hitStopT;
        private float _frozenY;
        private float _landT;
        private bool _bigDrop;
        private bool _landed;
        private AttackAnim _attack;
        private float _attackT = -1f;
        private float _windup;
        private float _lastY;
        private float _walkPhase;
        private float _trailT;

        public UnitView(Transform parent)
        {
            _bar = Ui.CreateRect("UnitBar", parent);
            _hpBack = Ui.CreateImage("HpBack", _bar, Shape.Square, new Color(0, 0, 0, 0.6f));
            _hpFill = Ui.CreateImage("HpFill", _bar, Shape.Square, Palette.Accent);
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
        }

        public Unit Unit { get; private set; }
        public float PrevY { get; set; }
        /// <summary>로봇 몸 가운데가 보이는 곳(전장 그림 pt).</summary>
        public Vector2 DisplayPosition { get; private set; }
        /// <summary>머리 위(피해 숫자가 뜨는 곳).</summary>
        public Vector2 HeadPosition { get; private set; }
        /// <summary>무기 끝·포구가 보이는 곳. 휘두르기와 포구 섬광이 나는 곳.</summary>
        public Vector2 FrontPosition { get; private set; }
        /// <summary>로봇 폭이 보이는 크기(pt).</summary>
        public float Size { get; private set; }
        public bool IsBigDrop => _bigDrop;
        /// <summary>Core에서는 들어갔지만 아직 보여 주지 않은 피해.</summary>
        public float PendingDamage { get; set; }
        public bool IsAttacking => _attackT >= 0f;
        /// <summary>바닥 위 위치(전장 로컬).</summary>
        public Vector3 WorldPosition { get; private set; }
        /// <summary>몸 가운데(전장 로컬). 탄이 날아가 맞는 곳.</summary>
        public Vector3 CenterWorld { get; private set; }
        public Vector3 MuzzleWorld => _model != null ? _model.MuzzleLocal(_field.World.Root) : CenterWorld;

        public void Bind(Unit unit, BattlefieldView field, FeelConfig feel, bool bigDrop)
        {
            Unit = unit;
            _field = field;
            _feel = feel;
            PrevY = unit.Y;
            _frozenY = unit.Y;
            _lastY = unit.Y;
            _walkPhase = 0f;
            _flashT = 0f;
            _knockT = 0f;
            _hitStopT = 0f;
            _landT = 0f;
            _bigDrop = bigDrop;
            _landed = false;
            _attackT = -1f;
            PendingDamage = 0f;
            // 같은 자리에 겹치지 않게 유닛마다 가로로 조금씩 비켜 놓는다.
            _jitterX = ((unit.Id * 37) % 9 - 4) / 4f;
            int tierIndex = Mathf.Clamp(unit.Tier - 1, 0, 2);
            var ratios = feel.Unit.SizeByTierRatio;
            float ratio = ratios != null && ratios.Length > tierIndex ? ratios[tierIndex] : 0.16f;
            _worldSize = Mathf.Min(feel.World.FieldWidth * ratio, field.World.LaneWidth * 0.8f);
            _model = RobotFactory.Create(unit.Def, unit.Side, unit.Tier, field.World.Root, _worldSize, feel.World.RobotFootprint, feel.World.AirHeight);
            _bar.gameObject.SetActive(true);
        }

        public void OnHit(FeelConfig feel)
        {
            _flashT = feel.Hit.FlashTime;
            _knockT = feel.Hit.KnockbackTime;
            if (_hitStopT <= 0f)
            {
                _frozenY = DisplayYFromLast;
            }
            _hitStopT = feel.Hit.HitStop;
        }

        private float DisplayYFromLast { get; set; }

        /// <summary>준비 동작을 시작한다. windup이 지나면 휘두르기(또는 반동)로 이어진다.</summary>
        public void BeginAttack(AttackAnim attack, float windup)
        {
            _attack = attack;
            _windup = Mathf.Max(0.01f, windup);
            _attackT = 0f;
        }

        /// <summary>공격 동작의 단계와 몸의 앞뒤 움직임(유닛 크기 배율), 찌그러짐, 밝기.</summary>
        private void AttackPose(float showDt, out int phase, out float e, out float t, out float forward, out Vector2 squash, out float whiten)
        {
            phase = 0;
            e = 0f;
            t = 0f;
            forward = 0f;
            squash = Vector2.one;
            whiten = 0f;
            if (_attackT < 0f)
            {
                return;
            }
            var a = _feel.Attack;
            _attackT += showDt;
            float strike = Mathf.Max(0.01f, a.StrikeTime);
            if (_attackT < _windup)
            {
                // 준비 동작: 처음엔 느리고 끝에 몰리게(때리기 직전이 보이게).
                e = _attackT / _windup;
                e *= e;
                phase = 1;
                switch (_attack)
                {
                    case AttackAnim.Slash:
                    case AttackAnim.Stab:
                        forward = -a.PullBack * e;
                        break;
                    case AttackAnim.Dash:
                        forward = -a.PullBack * 1.6f * e;
                        squash = new Vector2(Mathf.Lerp(1f, 1f / a.Crouch, e), Mathf.Lerp(1f, a.Crouch, e));
                        break;
                    case AttackAnim.Bash:
                    case AttackAnim.Lob:
                        squash = new Vector2(Mathf.Lerp(1f, 1f / a.Crouch, e), Mathf.Lerp(1f, a.Crouch, e));
                        whiten = _attack == AttackAnim.Lob ? e * 0.4f : 0f;
                        break;
                    case AttackAnim.Burst:
                        squash = Vector2.one * Mathf.Lerp(1f, a.Swell, e);
                        whiten = e * (0.5f + 0.5f * Mathf.Sin(_attackT * 60f));
                        break;
                    default:
                        whiten = e * 0.4f;
                        break;
                }
                return;
            }
            t = (_attackT - _windup) / strike;
            if (t >= 1f)
            {
                _attackT = -1f;
                t = 0f;
                return;
            }
            phase = 2;
            float k = Mathf.Sin(t * Mathf.PI);
            switch (_attack)
            {
                case AttackAnim.Slash:
                    forward = a.Lunge * k;
                    break;
                case AttackAnim.Stab:
                case AttackAnim.Bash:
                    forward = a.Lunge * 0.7f * k;
                    break;
                case AttackAnim.Dash:
                    forward = a.ChargeLunge * k;
                    squash = new Vector2(1f - 0.12f * k, 1f + 0.15f * k);
                    break;
                case AttackAnim.Burst:
                    squash = Vector2.one * a.Swell;
                    whiten = 1f;
                    break;
                default:
                    // 쏘는 유닛은 반동으로 살짝 밀린다.
                    forward = -a.PullBack * 0.5f * k;
                    break;
            }
        }

        /// <summary>그린다. 큰 착지가 이번 프레임에 끝났으면 true.</summary>
        public bool Render(BattlefieldView field, float alpha, float dt, float showDt, float time)
        {
            var world = field.World;
            float y = Mathf.Lerp(PrevY, Unit.Y, alpha);
            if (_hitStopT > 0f)
            {
                // 타격 순간 멈춤: 화면 표시에만 적용한다.
                _hitStopT -= dt;
                y = _frozenY;
            }
            DisplayYFromLast = y;

            // 걷기: 지난 프레임보다 움직였으면 다리를 흔든다(걸은 거리로 위상을 정함).
            float moved = Mathf.Abs(y - _lastY);
            bool moving = moved > 0.0005f;
            _walkPhase += moved / Mathf.Max(0.1f, _feel.World.WalkStride) * Mathf.PI * 2f;
            _lastY = y;

            var position = world.Ground(Unit.Lane, y);
            position.x += _jitterX * world.LaneWidth * 0.18f;

            if (_knockT > 0f)
            {
                _knockT -= dt;
                float kt = Mathf.Clamp01(_knockT / Mathf.Max(0.01f, _feel.Hit.KnockbackTime));
                position.z -= Unit.Direction * _feel.Hit.Knockback * 2f * kt;
            }

            AttackPose(showDt, out int phase, out float e, out float t, out float forward, out var squash, out float whiten);
            position.z += Unit.Direction * forward * _worldSize;

            bool landedNow = false;
            if (!_landed)
            {
                _landT += dt;
                float duration = _bigDrop ? _feel.Unit.LandingTime * 2f : _feel.Unit.LandingTime;
                float lt = Mathf.Clamp01(_landT / Mathf.Max(0.01f, duration));
                if (_bigDrop)
                {
                    // 위에서 크게 떨어진다.
                    position.y += (1f - lt * lt) * _feel.World.DropHeight;
                }
                else
                {
                    squash *= Mathf.Lerp(0.6f, 1f, lt);
                }
                if (lt >= 1f)
                {
                    _landed = true;
                    landedNow = _bigDrop;
                }
            }

            if (Unit.IsCharging)
            {
                // 돌격: 몸을 낮추고 기울여 달려든다(뒤에 속도선).
                squash = new Vector2(squash.x * 1.05f, squash.y * 0.9f);
                _trailT -= dt;
                if (_trailT <= 0f)
                {
                    _trailT = 0.06f;
                    var light = Color.Lerp(UnitStyle.Get(Unit.Def.Id).Color, Color.white, 0.6f);
                    var back = world.ToLocal(position + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), 0.4f, -Unit.Direction * _worldSize * 0.8f));
                    field.Fx.Pop(Shape.Square, Ui.WithAlpha(light, 0.8f), back, new Vector2(4f, Size * 0.5f), new Vector2(2f, Size * 1.1f), 0f, 0.2f);
                }
            }
            _model.Place(position, Unit.Side == Side.Player, squash);
            _model.SetLean(Unit.IsCharging ? 18f : 0f);
            _model.ShowBarrier(!Unit.CanMove && Unit.IsTaunting && _landed, RobotFactory.TeamColor(Unit.Side), time);
            _model.Animate(time, _walkPhase, moving && phase == 0 && !Unit.IsStunned, _feel.World.WalkSwing, phase, e, t);

            _flashT = Mathf.Max(0f, _flashT - dt);
            float flash = _feel.Hit.FlashTime > 0f ? _flashT / _feel.Hit.FlashTime : 0f;
            if (Unit.IsStunned)
            {
                _model.SetTint(0.5f + 0.3f * Mathf.Sin(Time.unscaledTime * 20f), Palette.Energy);
            }
            else
            {
                _model.SetTint(Mathf.Max(flash, whiten), Color.white);
            }

            WorldPosition = position;
            float height = _model.Height;
            CenterWorld = position + new Vector3(0f, Mathf.Max(0.4f, height * 0.5f), 0f);
            DisplayPosition = world.ToLocal(CenterWorld);
            HeadPosition = world.ToLocal(position + new Vector3(0f, height + 0.2f, 0f));
            FrontPosition = world.ToLocal(MuzzleWorld);
            Size = world.PointsPerUnit(position) * _worldSize;

            float ratio = Unit.MaxHp > 0f ? Mathf.Clamp01((Mathf.Max(0f, Unit.Hp) + PendingDamage) / Unit.MaxHp) : 0f;
            bool showHp = ratio < 0.999f;
            _bar.gameObject.SetActive(showHp);
            if (showHp)
            {
                float barW = Mathf.Max(24f, Size * 0.8f);
                Ui.PlaceCentered(_bar, HeadPosition + new Vector2(0f, 6f), new Vector2(barW, 5f));
                Ui.Place(_hpBack.rectTransform, new Rect(0f, 0f, barW, 5f));
                Ui.Place(_hpFill.rectTransform, new Rect(0f, 0f, barW, 5f));
                _hpFill.fillAmount = ratio;
                _hpFill.color = Unit.Side == Side.Player ? Palette.Accent : Palette.Enemy;
            }
            return landedNow;
        }

        public void Release()
        {
            _model?.Destroy();
            _model = null;
            Unit = null;
            _bar.gameObject.SetActive(false);
        }
    }

    public enum ProjectileKind
    {
        /// <summary>슈터봇 레이저 탄.</summary>
        Laser,
        /// <summary>드론봇 연사(두 발이 이어 날아감).</summary>
        Burst,
        /// <summary>플라즈마 포물선 구체.</summary>
        Orb,
        /// <summary>성 포탄.</summary>
        Shell,
    }

    /// <summary>탄 하나의 Core 위치 기록(발사 틱부터 틱마다 한 칸).</summary>
    internal sealed class ProjectileTrack
    {
        public Projectile Projectile;
        public int LastTick;
        public readonly List<float> Ys = new List<float>();
        public ProjectileView3D View;
    }

    /// <summary>날아가는 탄 하나의 3D 모습(빛나는 도형). 돌려 쓴다.</summary>
    internal sealed class ProjectileView3D
    {
        private readonly Transform _head;
        private readonly Transform _tail;
        private readonly Renderer _headRenderer;
        private readonly Renderer _tailRenderer;
        private Vector3 _last;
        private bool _hasLast;
        private float _size;
        private Vector3 _to;

        public ProjectileView3D(Transform parent)
        {
            var head = Mat3D.Sphere(parent, "Projectile", Vector3.zero, Vector3.one, Color.white, true);
            var tail = Mat3D.Sphere(parent, "ProjectileTail", Vector3.zero, Vector3.one, Color.white, true);
            _head = head.transform;
            _tail = tail.transform;
            _headRenderer = head.GetComponent<Renderer>();
            _tailRenderer = tail.GetComponent<Renderer>();
            _headRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Hide();
        }

        public bool Active { get; private set; }
        public ProjectileTrack Track { get; private set; }
        public ProjectileKind Kind { get; private set; }
        public Color Color { get; private set; }
        public Vector3 From { get; private set; }
        public Vector3 To
        {
            get => _to;
            set
            {
                _to = value;
                HasTarget = true;
            }
        }
        public bool HasTarget { get; private set; }
        /// <summary>발사를 보여 준 뒤 지난 시간. 이 시간만큼의 Core 기록 위치에 그린다.</summary>
        public float Elapsed { get; set; }
        public Vector3 Position { get; private set; }

        public void Show(ProjectileTrack track, ProjectileKind kind, Color color, Vector3 from, float late, float size)
        {
            Track = track;
            Kind = kind;
            Color = color;
            From = from;
            _to = from;
            HasTarget = false;
            Elapsed = Mathf.Max(0f, late);
            _hasLast = false;
            _size = size;
            Active = true;
            _head.gameObject.SetActive(true);
            _tail.gameObject.SetActive(kind == ProjectileKind.Burst || kind == ProjectileKind.Laser);
            _headRenderer.sharedMaterial = Mat3D.Get(Color.Lerp(color, Color.white, 0.3f), true);
            _tailRenderer.sharedMaterial = Mat3D.Get(color, true);
        }

        public void Place(Vector3 position)
        {
            var direction = _hasLast && (position - _last).sqrMagnitude > 1e-6f
                ? (position - _last).normalized
                : (Track.Projectile.Side == Side.Player ? Vector3.forward : Vector3.back);
            _last = position;
            _hasLast = true;
            Position = position;
            _head.localPosition = position;
            float s = _size;
            switch (Kind)
            {
                case ProjectileKind.Laser:
                    // 진행 방향으로 길쭉한 레이저.
                    _head.localRotation = Quaternion.LookRotation(direction);
                    _head.localScale = new Vector3(s * 0.45f, s * 0.45f, s * 2.6f);
                    _tail.localPosition = position - direction * s * 1.4f;
                    _tail.localRotation = _head.localRotation;
                    _tail.localScale = new Vector3(s * 0.8f, s * 0.8f, s * 1.6f);
                    break;
                case ProjectileKind.Burst:
                    _head.localScale = Vector3.one * s * 0.7f;
                    _tail.localPosition = position - direction * s * 2.2f;
                    _tail.localScale = Vector3.one * s * 0.6f;
                    break;
                case ProjectileKind.Orb:
                    _head.localScale = Vector3.one * s * 1.5f;
                    break;
                default:
                    _head.localScale = Vector3.one * s * 1.2f;
                    break;
            }
        }

        public void Hide()
        {
            Active = false;
            if (Track != null)
            {
                Track.View = null;
            }
            Track = null;
            _head.gameObject.SetActive(false);
            _tail.gameObject.SetActive(false);
        }
    }
}
