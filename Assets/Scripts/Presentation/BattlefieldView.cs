using System;
using System.Collections.Generic;
using SlotBattle.Core.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlotBattle.Presentation
{
    /// <summary>
    /// 전장: 위쪽 적 성, 아래쪽 내 성, 좌우 2개 라인. Core 상태를 그리기만 하고 규칙 판단은 하지 않는다.
    /// 좌표: 라인 위의 거리 0(내 성) ~ laneLength(적 성)를 전장 사각형 안의 pt로 바꾼다.
    /// </summary>
    public sealed class BattlefieldView
    {
        private const float CastleBandRatio = 0.07f;

        private readonly FeelConfig _feel;
        private readonly RectTransform _root;
        private readonly RectTransform _content;
        private readonly RectTransform _unitsLayer;
        private readonly Image _background;
        private readonly Image[] _lanes = new Image[2];
        private readonly Image _midline;
        private readonly Image _enemyCastle;
        private readonly Image _playerCastle;
        private readonly Image[] _dropZones = new Image[2];
        private readonly Image _rangeBand;
        private readonly Image _dropMarker;
        private readonly Image _aimRing;
        private readonly Image _aimCatcher;
        private readonly Text _aimHint;
        private readonly Dictionary<Unit, UnitView> _views = new Dictionary<Unit, UnitView>();
        private readonly Stack<UnitView> _pool = new Stack<UnitView>();
        private readonly List<Unit> _removeBuffer = new List<Unit>();
        private float _laneLength = 20f;
        private float _playerZone = 10f;
        private int _laneCount = 2;
        private float _shake;
        private float _enemyCastleFlash;
        private float _playerCastleFlash;

        public BattlefieldView(Transform parent, FeelConfig feel)
        {
            _feel = feel;
            _root = Ui.CreateRect("Battlefield", parent);
            _content = Ui.CreateRect("Content", _root);
            _content.anchorMin = Vector2.zero;
            _content.anchorMax = Vector2.one;
            _content.sizeDelta = Vector2.zero;

            _background = Ui.CreateImage("Background", _content, Shape.Panel, Palette.Background);
            for (int i = 0; i < 2; i++)
            {
                _lanes[i] = Ui.CreateImage("Lane" + i, _content, Shape.Panel, Palette.Lane);
                _dropZones[i] = Ui.CreateImage("DropZone" + i, _content, Shape.Panel, Ui.WithAlpha(Palette.Player, 0.12f));
                _dropZones[i].gameObject.SetActive(false);
            }
            _midline = Ui.CreateImage("Midline", _content, Shape.Square, Ui.WithAlpha(Palette.LaneLine, 0.9f));
            _enemyCastle = Ui.CreateImage("EnemyCore", _content, Shape.Panel, Palette.Enemy);
            _playerCastle = Ui.CreateImage("PlayerCore", _content, Shape.Panel, Palette.Player);
            _rangeBand = Ui.CreateImage("RangeBand", _content, Shape.Panel, Ui.WithAlpha(Palette.Accent, 0.18f));
            _rangeBand.gameObject.SetActive(false);
            _dropMarker = Ui.CreateImage("DropMarker", _content, Shape.Ring, Ui.WithAlpha(Palette.Accent, 0.9f));
            _dropMarker.gameObject.SetActive(false);

            _unitsLayer = Ui.CreateRect("Units", _content);
            _unitsLayer.anchorMin = Vector2.zero;
            _unitsLayer.anchorMax = Vector2.one;
            _unitsLayer.sizeDelta = Vector2.zero;

            Fx = new FxLayer("FieldFx", _content);

            _aimRing = Ui.CreateImage("AimRing", _content, Shape.Ring, Ui.WithAlpha(Palette.Energy, 0.9f));
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
        public FxLayer Fx { get; }
        public FieldPointerInput AimInput { get; }
        public Vector2 Size { get; private set; }
        public bool ShakeEnabled { get; set; } = true;
        public int ViewCount => _views.Count;
        public float CastleBand => Size.y * CastleBandRatio;
        public float PlayHeight => Size.y - CastleBand * 2f;
        public float LaneWidth => Size.x / _laneCount;
        /// <summary>전장 거리 1이 몇 pt인가.</summary>
        public float UnitLength => PlayHeight / _laneLength;

        public void Configure(BattleSimulation sim)
        {
            _laneLength = sim.Config.LaneLength;
            _playerZone = sim.Config.PlayerZoneLength;
            _laneCount = Mathf.Clamp(sim.Config.LaneCount, 1, 2);
        }

        public void Layout(Rect rect)
        {
            Ui.Place(_root, rect);
            Size = rect.size;
            Ui.Place(_background.rectTransform, new Rect(0, 0, Size.x, Size.y));
            float laneW = LaneWidth;
            for (int i = 0; i < 2; i++)
            {
                var laneRect = new Rect(i * laneW + 4f, CastleBand, laneW - 8f, PlayHeight);
                Ui.Place(_lanes[i].rectTransform, laneRect);
                _lanes[i].gameObject.SetActive(i < _laneCount);
                float zoneTop = ToLocalY(_playerZone);
                Ui.Place(_dropZones[i].rectTransform, new Rect(i * laneW + 4f, CastleBand, laneW - 8f, zoneTop - CastleBand));
            }
            Ui.Place(_midline.rectTransform, new Rect(4f, ToLocalY(_playerZone) - 1f, Size.x - 8f, 2f));
            Ui.Place(_enemyCastle.rectTransform, new Rect(6f, Size.y - CastleBand + 2f, Size.x - 12f, CastleBand - 6f));
            Ui.Place(_playerCastle.rectTransform, new Rect(6f, 4f, Size.x - 12f, CastleBand - 6f));
            Ui.PlaceCentered(_aimHint.rectTransform, new Vector2(Size.x / 2f, Size.y - CastleBand - 18f), new Vector2(Size.x, 24f));
            foreach (var view in _views.Values)
            {
                view.Relayout(this);
            }
        }

        // ───────────── 좌표 ─────────────

        public float ToLocalY(float laneY)
        {
            return CastleBand + laneY / _laneLength * PlayHeight;
        }

        public float LaneCenterX(int lane)
        {
            return LaneWidth * (lane + 0.5f);
        }

        public Vector2 ToLocal(int lane, float laneY)
        {
            return new Vector2(LaneCenterX(lane), ToLocalY(laneY));
        }

        /// <summary>전장 안의 점을 라인과 거리로 바꾼다. 전장 밖이면 false.</summary>
        public bool TryFromLocal(Vector2 local, out int lane, out float laneY)
        {
            lane = Mathf.Clamp(Mathf.FloorToInt(local.x / Mathf.Max(1f, LaneWidth)), 0, _laneCount - 1);
            laneY = (local.y - CastleBand) / Mathf.Max(1f, PlayHeight) * _laneLength;
            return local.x >= 0f && local.x <= Size.x && local.y >= 0f && local.y <= Size.y;
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

        public void OnHit(UnitHitEvent hit)
        {
            if (!_views.TryGetValue(hit.Target, out var view))
            {
                return;
            }
            view.OnHit(_feel);
            var color = hit.Target.Side == Side.Player ? Ui.Hex("#FFB4B4") : Color.white;
            if (hit.Attacker == null && hit.SourceCastle == null)
            {
                color = Palette.Energy;
            }
            float scale = hit.Attacker != null && hit.Attacker.Tier >= 3 ? 1.5f : 1f;
            Fx.DamageNumber(view.DisplayPosition, hit.Damage, color, _feel.Hit.DamageNumberRise, _feel.Hit.DamageNumberTime, scale);
            if (hit.Attacker != null && hit.Attacker.Tier >= 3)
            {
                Shake(_feel.Shake.Tier3Attack);
            }
        }

        public void OnDied(Unit unit)
        {
            if (!_views.TryGetValue(unit, out var view))
            {
                return;
            }
            var style = UnitStyle.Get(unit.Def.Id);
            Fx.Debris(view.DisplayPosition, style.Color, _feel.Death.DebrisCount + unit.Tier * 2, _feel.Death.DebrisSpeed, _feel.Death.DebrisTime);
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
            return new Vector2(Size.x / 2f, side == Side.Enemy ? Size.y - CastleBand / 2f : CastleBand / 2f);
        }

        public void CastleDestroyed(Side side)
        {
            var center = CastleCenter(side);
            var color = side == Side.Enemy ? Palette.Enemy : Palette.Player;
            for (int i = 0; i < 5; i++)
            {
                var offset = new Vector2((i - 2) * Size.x * 0.18f, 0f);
                Fx.Debris(center + offset, color, _feel.Death.CastleDebrisCount / 5 + 1, _feel.Death.DebrisSpeed * 2f, _feel.Death.DebrisTime * 2.5f, 10f);
                Fx.Debris(center + offset, Palette.Energy, 3, _feel.Death.DebrisSpeed * 1.5f, _feel.Death.DebrisTime * 2f, 6f);
            }
            Fx.Ring(center, 20f, Size.x * 1.4f, Ui.WithAlpha(Color.white, 0.9f), 0.8f);
            (side == Side.Enemy ? _enemyCastle : _playerCastle).color = Ui.WithAlpha(color, 0.25f);
            Shake(_feel.Shake.CastleDestroyed);
        }

        public void Explosion(int lane, float laneY, float radius)
        {
            float size = radius * 2f * UnitLength;
            Fx.Ring(ToLocal(lane, laneY), size * 0.3f, size, Ui.WithAlpha(Ui.Hex("#FFB347"), 0.9f), 0.35f);
        }

        public void SkillBlast(int lane, float laneY, float radius)
        {
            var center = ToLocal(lane, laneY);
            float size = radius * 2f * UnitLength;
            Fx.Ring(center, size * 0.2f, size, Ui.WithAlpha(Palette.Energy, 1f), 0.45f);
            Fx.Ring(center, size * 0.6f, size * 1.3f, Ui.WithAlpha(Color.white, 0.6f), 0.6f);
            Fx.Debris(center, Palette.Energy, 14, 180f, 0.5f, 5f);
            Shake(_feel.Shake.CastleHit * 1.5f);
        }

        /// <summary>3단계 유닛 착지: 충격파와 흔들림.</summary>
        public void LandingImpact(Vector2 position, float size)
        {
            Fx.Ring(position, size, size * 4f, Ui.WithAlpha(Color.white, 0.9f), 0.45f);
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
            _enemyCastle.color = Palette.Enemy;
            _playerCastle.color = Palette.Player;
        }

        public void Clear()
        {
            foreach (var view in _views.Values)
            {
                view.Release();
                _pool.Push(view);
            }
            _views.Clear();
            Fx.Clear();
            _shake = 0f;
            ResetCastles();
            ShowDropPreview(false, 0, 0f, 0f);
            SetAiming(false);
        }

        /// <summary>틱 사이 보간 비율(alpha)로 유닛을 그린다.</summary>
        public void Render(BattleSimulation sim, float alpha, float dt, Action<UnitView> onLanded)
        {
            _removeBuffer.Clear();
            foreach (var pair in _views)
            {
                if (!pair.Key.IsAlive && !ContainsUnit(sim, pair.Key))
                {
                    _removeBuffer.Add(pair.Key);
                    continue;
                }
                if (pair.Value.Render(this, alpha, dt))
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
            if (!sim.EnemyCastle.IsDestroyed)
            {
                _enemyCastle.color = Color.Lerp(Palette.Enemy, Color.white, _enemyCastleFlash * 5f);
            }
            if (!sim.PlayerCastle.IsDestroyed)
            {
                _playerCastle.color = Color.Lerp(Palette.Player, Color.white, _playerCastleFlash * 5f);
            }

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

        /// <summary>끌어다 놓는 동안: 놓을 수 있는 영역(내 진영)과 사거리를 보여 준다.</summary>
        public void ShowDropPreview(bool visible, int lane, float laneY, float range, bool valid = true)
        {
            for (int i = 0; i < 2; i++)
            {
                _dropZones[i].gameObject.SetActive(visible && i < _laneCount);
                _dropZones[i].color = Ui.WithAlpha(Palette.Player, visible && i == lane ? 0.28f : 0.1f);
            }
            _rangeBand.gameObject.SetActive(visible && valid);
            _dropMarker.gameObject.SetActive(visible && valid);
            if (!visible || !valid)
            {
                return;
            }
            float y = ToLocalY(laneY);
            float top = ToLocalY(Mathf.Min(_laneLength, laneY + range));
            float bottom = ToLocalY(Mathf.Max(0f, laneY - range));
            Ui.Place(_rangeBand.rectTransform, new Rect(lane * LaneWidth + 10f, bottom, LaneWidth - 20f, top - bottom));
            Ui.PlaceCentered(_dropMarker.rectTransform, new Vector2(LaneCenterX(lane), y), new Vector2(36f, 36f));
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
            float size = radius * 2f * UnitLength;
            Ui.PlaceCentered(_aimRing.rectTransform, ToLocal(lane, laneY), new Vector2(size, size));
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
    /// 유닛 하나의 모습: 단계가 높을수록 크고 빛나며, 맞으면 흰색으로 번쩍이고 뒤로 살짝 밀린다.
    /// </summary>
    public sealed class UnitView
    {
        private readonly RectTransform _root;
        private readonly Image _shadow;
        private readonly Image _glow;
        private readonly Image _outline;
        private readonly Image _body;
        private readonly Image _hpBack;
        private readonly Image _hpFill;
        private FeelConfig _feel;
        private UnitStyle.Style _style;
        private float _size;
        private float _jitterX;
        private float _flashT;
        private float _knockT;
        private float _hitStopT;
        private float _frozenY;
        private float _landT;
        private bool _bigDrop;
        private bool _landed;

        public UnitView(Transform parent)
        {
            _root = Ui.CreateRect("Unit", parent);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _shadow = Ui.CreateImage("Shadow", _root, Shape.Circle, new Color(0, 0, 0, 0.35f));
            _glow = Ui.CreateImage("Glow", _root, Shape.Glow, Color.white);
            _outline = Ui.CreateImage("Outline", _root, Shape.Circle, Color.white);
            _body = Ui.CreateImage("Body", _root, Shape.Circle, Color.white);
            _hpBack = Ui.CreateImage("HpBack", _root, Shape.Square, new Color(0, 0, 0, 0.6f));
            _hpFill = Ui.CreateImage("HpFill", _root, Shape.Square, Palette.Accent);
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
        }

        public Unit Unit { get; private set; }
        public float PrevY { get; set; }
        public Vector2 DisplayPosition { get; private set; }
        public float Size => _size;
        public bool IsBigDrop => _bigDrop;

        public void Bind(Unit unit, BattlefieldView field, FeelConfig feel, bool bigDrop)
        {
            Unit = unit;
            _feel = feel;
            _style = UnitStyle.Get(unit.Def.Id);
            PrevY = unit.Y;
            _frozenY = unit.Y;
            _flashT = 0f;
            _knockT = 0f;
            _hitStopT = 0f;
            _landT = 0f;
            _bigDrop = bigDrop;
            _landed = false;
            // 같은 자리에 겹치지 않게 유닛마다 가로로 조금씩 비켜 그린다.
            _jitterX = ((unit.Id * 37) % 9 - 4) / 4f;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();

            Ui.SetShape(_body, _style.Shape);
            Ui.SetShape(_outline, _style.Shape);
            _outline.color = unit.Side == Side.Player ? Palette.Player : Palette.Enemy;
            _body.color = _style.Color;
            int tierIndex = Mathf.Clamp(unit.Tier - 1, 0, 2);
            float glow = feel.Unit.GlowByTier.Length > tierIndex ? feel.Unit.GlowByTier[tierIndex] : 0f;
            _glow.color = Ui.WithAlpha(_style.Color, glow);
            _glow.gameObject.SetActive(glow > 0f);
            _shadow.gameObject.SetActive(unit.IsAir);
            // 진행 방향을 향한다(적은 아래로).
            _root.localRotation = Quaternion.identity;
            _body.rectTransform.localRotation = unit.Side == Side.Enemy ? Quaternion.Euler(0, 0, 180f) : Quaternion.identity;
            _outline.rectTransform.localRotation = _body.rectTransform.localRotation;
            Relayout(field);
        }

        public void Relayout(BattlefieldView field)
        {
            int tierIndex = Mathf.Clamp(Unit.Tier - 1, 0, 2);
            float baseSize = _feel.Unit.SizeByTier.Length > tierIndex ? _feel.Unit.SizeByTier[tierIndex] : 24f;
            // 작은 화면에서는 라인 폭에 맞춰 줄인다.
            float laneLimit = field.LaneWidth * 0.42f;
            _size = Mathf.Min(baseSize * _style.Scale, laneLimit);
            float outline = Mathf.Max(2f, _size * 0.12f);
            _root.sizeDelta = new Vector2(_size, _size);
            Ui.PlaceCentered(_glow.rectTransform, new Vector2(_size / 2f, _size / 2f), Vector2.one * _size * 2.2f);
            Ui.PlaceCentered(_outline.rectTransform, new Vector2(_size / 2f, _size / 2f), Vector2.one * (_size + outline * 2f));
            Ui.PlaceCentered(_body.rectTransform, new Vector2(_size / 2f, _size / 2f), Vector2.one * _size);
            Ui.PlaceCentered(_shadow.rectTransform, new Vector2(_size / 2f + 4f, _size / 2f - 10f), new Vector2(_size * 0.8f, _size * 0.4f));
            Ui.Place(_hpBack.rectTransform, new Rect(0f, _size + 3f, _size, 4f));
            Ui.Place(_hpFill.rectTransform, new Rect(0f, _size + 3f, _size, 4f));
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

        /// <summary>그린다. 큰 착지가 이번 프레임에 끝났으면 true.</summary>
        public bool Render(BattlefieldView field, float alpha, float dt)
        {
            float y = Mathf.Lerp(PrevY, Unit.Y, alpha);
            if (_hitStopT > 0f)
            {
                // 타격 순간 멈춤: 화면 표시에만 적용한다.
                _hitStopT -= dt;
                y = _frozenY;
            }
            DisplayYFromLast = y;

            var position = field.ToLocal(Unit.Lane, y);
            position.x += _jitterX * field.LaneWidth * 0.22f;

            if (_knockT > 0f)
            {
                _knockT -= dt;
                float t = Mathf.Clamp01(_knockT / Mathf.Max(0.01f, _feel.Hit.KnockbackTime));
                position.y -= Unit.Direction * _feel.Hit.Knockback * field.UnitLength * t;
            }

            bool landedNow = false;
            float scale = 1f;
            float alphaBody = 1f;
            if (!_landed)
            {
                _landT += dt;
                float duration = _bigDrop ? _feel.Unit.LandingTime * 2f : _feel.Unit.LandingTime;
                float t = Mathf.Clamp01(_landT / Mathf.Max(0.01f, duration));
                if (_bigDrop)
                {
                    // 위에서 크게 떨어진다.
                    position.y += (1f - t * t) * _feel.Unit.JackpotDropHeight;
                    scale = Mathf.Lerp(1.8f, 1f, t * t);
                }
                else
                {
                    scale = Mathf.Lerp(1.4f, 1f, t);
                    alphaBody = t;
                }
                if (t >= 1f)
                {
                    _landed = true;
                    landedNow = _bigDrop;
                }
            }

            DisplayPosition = position;
            _root.anchoredPosition = position;
            _root.localScale = new Vector3(scale, scale, 1f);

            _flashT = Mathf.Max(0f, _flashT - dt);
            float flash = _feel.Hit.FlashTime > 0f ? _flashT / _feel.Hit.FlashTime : 0f;
            var body = Color.Lerp(_style.Color, Color.white, flash);
            if (Unit.IsStunned)
            {
                body = Color.Lerp(body, Palette.Energy, 0.5f + 0.3f * Mathf.Sin(Time.unscaledTime * 20f));
            }
            body.a = alphaBody;
            _body.color = body;

            float ratio = Unit.MaxHp > 0f ? Mathf.Clamp01(Unit.Hp / Unit.MaxHp) : 0f;
            bool showHp = ratio < 0.999f;
            _hpBack.gameObject.SetActive(showHp);
            _hpFill.gameObject.SetActive(showHp);
            _hpFill.fillAmount = ratio;
            _hpFill.color = Unit.Side == Side.Player ? Palette.Accent : Palette.Enemy;
            return landedNow;
        }

        public void Release()
        {
            Unit = null;
            _root.gameObject.SetActive(false);
        }
    }
}
