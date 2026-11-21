using System;
using System.Collections.Generic;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;
using SlotBattle.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlotBattle.Presentation
{
    /// <summary>
    /// 한 판의 화면. Core(BattleSimulation)를 초당 20틱으로 돌리고, 사건을 받아 연출을 재생하며,
    /// 터치 입력을 명령 객체로 바꿔 Core에 넘긴다. 규칙 판단은 하지 않는다.
    /// </summary>
    public sealed class BattleController : MonoBehaviour
    {
        private const float TickDt = 1f / BattleSimulation.TickRate;
        private const int MaxTicksPerFrame = 8;

        private GameData _data;
        private FeelConfig _feel;
        private IHapticService _haptics;
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private Camera _camera;
        private RectTransform _root;

        private BattlefieldView _field;
        private HudView _hud;
        private CastleBar _playerBar;
        private ControlPanel _control;
        private FxLayer _overlayFx;
        private ResultPanel _result;
        private SettingsPanel _settings;
        private Image _dragGhost;
        private Image _dragGhostOutline;

        private BattleSimulation _sim;
        private LayoutResult _layout;
        private ScreenInfo _lastScreen;
        private bool _hasLayout;
        private float _acc;
        private bool _paused;
        private float _slowT;
        private float _endDelay;
        private bool _resultShown;
        private int _lockedReel = -1;
        private int _dragIndex = -1;
        private bool _dragValid;
        private int _dragLane;
        private float _dragY;
        private bool _aiming;
        private float _castleShakeCooldown;
        private readonly string[] _queueSignature = new string[8];

        /// <summary>테스트에서 iPhone·iPad 화면을 흉내 낼 때 쓴다. null이면 실제 기기 화면.</summary>
        public Func<ScreenInfo> ScreenOverride;

        public int StageId = 1;

        public BattleSimulation Sim => _sim;
        public BattlefieldView Field => _field;
        public ControlPanel Control => _control;
        public HudView Hud => _hud;
        public ResultPanel Result => _result;
        public SettingsPanel Settings => _settings;
        public IHapticService Haptics => _haptics;
        public LayoutResult CurrentLayout => _layout;
        public bool IsAiming => _aiming;
        public bool IsPaused => _paused;
        public int LockedReel => _lockedReel;
        public Camera Camera => _camera;

        public void Initialize(GameData data, FeelConfig feel, IHapticService haptics, Canvas canvas, CanvasScaler scaler, Camera camera)
        {
            _data = data;
            _feel = feel;
            _haptics = haptics;
            _canvas = canvas;
            _scaler = scaler;
            _camera = camera;

            _root = Ui.CreateRect("Root", canvas.transform);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.pivot = Vector2.zero;
            _root.sizeDelta = Vector2.zero;
            _root.anchoredPosition = Vector2.zero;

            _field = new BattlefieldView(_root, feel);
            _playerBar = new CastleBar(_root, "내 코어 기지", Palette.Player, feel.Numbers);
            _hud = new HudView(_root, feel);
            _control = new ControlPanel(_root, feel, data.Battle.WaitingQueueCapacity, data.Slot.ReelCount, OnReelTapped);
            _overlayFx = new FxLayer("OverlayFx", _root);

            _dragGhostOutline = Ui.CreateImage("DragGhostOutline", _root, Shape.Circle, Palette.Player);
            _dragGhost = Ui.CreateImage("DragGhost", _root, Shape.Circle, Color.white);
            _dragGhostOutline.gameObject.SetActive(false);
            _dragGhost.gameObject.SetActive(false);

            _result = new ResultPanel(_root, feel);
            _settings = new SettingsPanel(_root, feel);

            _control.MainButton.OnClick = PressMain;
            _control.RespinButton.OnClick = PressRespin;
            _control.SkillButton.OnClick = PressSkill;
            _control.Reels.ReelStopped = _ => PlayHaptic(_feel.Haptics.ReelStop);
            _control.Reels.Jackpot = OnJackpotRevealed;
            foreach (var slot in _control.QueueSlots)
            {
                slot.BeginDrag = OnQueueBeginDrag;
                slot.Drag = OnQueueDrag;
                slot.EndDrag = OnQueueEndDrag;
            }
            _field.AimInput.Down = OnAimPointer;
            _field.AimInput.Drag = OnAimPointer;
            _field.AimInput.Up = OnAimRelease;
            _hud.SettingsButton.OnClick = OpenSettings;
            _result.RetryButton.OnClick = Restart;
            _settings.CloseButton.OnClick = CloseSettings;
            _settings.RestartButton.OnClick = Restart;
            _settings.HapticsButton.OnClick = ToggleHaptics;
            _settings.ShakeButton.OnClick = ToggleShake;

            Restart();
        }

        // ───────────── 판 시작 ─────────────

        public void Restart()
        {
            Restart((ulong)DateTime.UtcNow.Ticks ^ (ulong)Environment.TickCount);
        }

        public void Restart(ulong seed)
        {
            var stage = _data.GetStage(StageId);
            _sim = new BattleSimulation(_data, stage, _data.Slot.StartingDeck, seed);
            _acc = 0f;
            _paused = false;
            _slowT = 0f;
            _endDelay = 0f;
            _resultShown = false;
            _lockedReel = -1;
            _aiming = false;
            CancelDrag();
            for (int i = 0; i < _queueSignature.Length; i++)
            {
                _queueSignature[i] = null;
            }
            _field.Clear();
            _field.Configure(_sim);
            _control.Reels.SetDeck(_sim.Deck);
            _control.Reels.ShowIdle();
            _control.Fx.Clear();
            _overlayFx.Clear();
            _result.Hide();
            _settings.Hide();
            _hasLayout = false;
            ApplyLayoutIfNeeded();
            _hud.EnemyBar.Reset(_sim.EnemyCastle.MaxHp);
            _playerBar.Reset(_sim.PlayerCastle.MaxHp);
            _sim.DrainEvents();
        }

        // ───────────── 프레임 ─────────────

        private void Update()
        {
            if (_sim == null)
            {
                return;
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            ApplyLayoutIfNeeded();
            if (!_paused)
            {
                Step(dt, MaxTicksPerFrame);
            }
            Render(dt);
        }

        /// <summary>테스트용: 틱 수 제한 없이 시간을 진행하고 한 번 그린다.</summary>
        public void StepForTest(float seconds)
        {
            ApplyLayoutIfNeeded();
            if (!_paused)
            {
                Step(seconds, int.MaxValue);
            }
            Render(Mathf.Min(seconds, 0.1f));
        }

        private void Step(float dt, int maxTicks)
        {
            if (_sim.IsOver)
            {
                return;
            }
            _acc += dt;
            int ticks = 0;
            while (_acc >= TickDt && ticks < maxTicks)
            {
                _field.BeginTick(_sim);
                _sim.Tick();
                _acc -= TickDt;
                ticks++;
                Dispatch();
                if (_sim.IsOver)
                {
                    _acc = 0f;
                    break;
                }
            }
            if (ticks >= maxTicks)
            {
                // 너무 밀렸으면 따라잡지 않고 버린다(끊김보다 낫다).
                _acc = Mathf.Min(_acc, TickDt);
            }
        }

        private void Render(float dt)
        {
            // 성이 무너질 때의 느린 화면은 연출 시간에만 적용한다.
            float fxDt = dt;
            if (_slowT > 0f)
            {
                _slowT -= dt;
                fxDt = dt * _feel.Death.CastleSlowMotion;
            }
            float alpha = Mathf.Clamp01(_acc / TickDt);
            _field.Render(_sim, alpha, fxDt, OnBigLanding);

            _hud.EnemyBar.SetValue(_sim.EnemyCastle.Hp);
            _hud.EnemyBar.Render(dt);
            _playerBar.SetValue(_sim.PlayerCastle.Hp);
            _playerBar.Render(dt);
            _hud.Render(_sim, dt);

            _control.Reels.Update(dt);
            _control.Mana.Render(_sim.ManaExact, dt);
            RefreshButtons();
            RefreshQueue();
            _control.Fx.Update(dt);
            _overlayFx.Update(dt);
            _castleShakeCooldown = Mathf.Max(0f, _castleShakeCooldown - dt);

            if (_sim.IsOver && !_resultShown)
            {
                _endDelay -= dt;
                if (_endDelay <= 0f)
                {
                    _resultShown = true;
                    _result.Show(_layout.SafeArea, _sim);
                }
            }
            _result.Render(dt);
            _settings.Render(dt);
        }

        // ───────────── 화면 배치 ─────────────

        private ScreenInfo CurrentScreen()
        {
            return ScreenOverride != null ? ScreenOverride() : ScreenInfo.FromDevice();
        }

        public void ApplyLayoutIfNeeded()
        {
            var screen = CurrentScreen();
            if (_hasLayout && screen.Equals(_lastScreen))
            {
                return;
            }
            _hasLayout = true;
            _lastScreen = screen;
            _scaler.scaleFactor = screen.PointScale;
            _layout = LayoutCalculator.Compute(screen, _feel.Layout);
            _field.Layout(_layout.Battlefield);
            Ui.Place(_playerBar.Root, _layout.PlayerCastleBar);
            _hud.Layout(_layout.Hud);
            _control.Layout(_layout.Control, _sim != null ? _sim.MaxMana : 10);
            for (int i = 0; i < _queueSignature.Length; i++)
            {
                _queueSignature[i] = null;
            }
            if (_result.IsVisible)
            {
                _result.Show(_layout.SafeArea, _sim);
            }
            if (_settings.IsVisible)
            {
                _settings.Show(_layout.SafeArea, _haptics.Enabled, _haptics.IsSupported, _field.ShakeEnabled);
            }
        }

        // ───────────── 사건 → 연출 ─────────────

        private void Dispatch()
        {
            var events = _sim.DrainEvents();
            for (int i = 0; i < events.Count; i++)
            {
                switch (events[i])
                {
                    case UnitSummonedEvent summoned:
                        bool bigDrop = summoned.Unit.Side == Side.Player && summoned.Unit.Tier >= 3;
                        _field.AddUnit(summoned.Unit, bigDrop);
                        break;
                    case UnitHitEvent hit:
                        _field.OnHit(hit);
                        break;
                    case UnitDiedEvent died:
                        _field.OnDied(died.Unit);
                        break;
                    case CastleDamagedEvent castle:
                        _field.OnCastleDamaged(castle.Castle.Side);
                        if (_castleShakeCooldown <= 0f)
                        {
                            // 성 타격 흔들림은 너무 잦지 않게 묶는다.
                            _field.Shake(_feel.Shake.CastleHit);
                            _castleShakeCooldown = 0.2f;
                        }
                        break;
                    case ExplosionEvent explosion:
                        _field.Explosion(explosion.Lane, explosion.Y, explosion.Radius);
                        break;
                    case CommanderSkillUsedEvent skill:
                        _field.SkillBlast(skill.Lane, skill.Y, skill.Radius);
                        break;
                    case BattleEndedEvent ended:
                        OnBattleEnded(ended.Outcome);
                        break;
                }
            }
        }

        private void OnBattleEnded(BattleOutcome outcome)
        {
            CancelDrag();
            SetAiming(false);
            _control.Reels.Finish();
            if (_sim.EndedByTimeout)
            {
                _endDelay = 0.6f;
                return;
            }
            if (_sim.EnemyCastle.IsDestroyed)
            {
                _field.CastleDestroyed(Side.Enemy);
            }
            if (_sim.PlayerCastle.IsDestroyed)
            {
                _field.CastleDestroyed(Side.Player);
            }
            _slowT = _feel.Death.CastleSlowTime;
            _endDelay = _feel.Death.CastleSlowTime;
            PlayHaptic(_feel.Haptics.CastleDestroyed);
        }

        private void OnBigLanding(UnitView view)
        {
            _field.LandingImpact(view.DisplayPosition, view.Size);
            PlayHaptic(_feel.Haptics.Jackpot);
        }

        private void OnJackpotRevealed()
        {
            _overlayFx.Flash(Color.white, _feel.Slot.JackpotFlashAlpha, _feel.Slot.JackpotFlashTime);
            var center = _control.Root.anchoredPosition + _control.ReelCenter(1);
            _overlayFx.Text(center + new Vector2(0f, 60f), "잭팟! 합체형 조립", Palette.Energy, 26, 30f, 1.2f);
            PlayHaptic(_feel.Haptics.Jackpot);
        }

        // ───────────── 버튼 ─────────────

        private CommandResult Execute(BattleCommand command)
        {
            var result = _sim.Execute(command);
            Dispatch();
            return result;
        }

        /// <summary>큰 버튼: 결과가 없으면 조립(스핀), 있으면 확정.</summary>
        public void PressMain()
        {
            if (_sim.IsOver || _paused)
            {
                return;
            }
            var pending = _sim.PendingSpin;
            if (pending == null)
            {
                var result = Execute(new SpinCommand());
                if (result == CommandResult.Ok)
                {
                    _lockedReel = -1;
                    _control.Reels.SetLocked(-1);
                    _control.Reels.Start(_sim.PendingSpin.Symbols, null, _sim.PendingSpin.IsJackpot);
                }
                else
                {
                    DenySpin(result);
                }
                return;
            }

            // 연출 중에도 바로 확정할 수 있다(연출이 조작을 막지 않게).
            _control.Reels.Finish();
            var symbols = new List<int>(pending.Symbols);
            var summons = new List<Core.Slot.SummonEntry>(pending.Summons);
            int before = _sim.Queue.Count;
            var confirm = Execute(new ConfirmSpinCommand());
            if (confirm == CommandResult.Ok)
            {
                for (int k = 0; k < summons.Count; k++)
                {
                    int reel = symbols.IndexOf(summons[k].Symbol);
                    int slot = before + k;
                    if (slot >= _control.QueueSlots.Length)
                    {
                        continue;
                    }
                    var style = UnitStyle.Get(_sim.Deck[summons[k].Symbol].Id);
                    _control.QueueSlots[slot].RevealAfter(_feel.Unit.FlyToQueueTime);
                    _control.Fx.Fly(style.Shape, style.Color, _control.ReelCenter(Mathf.Max(0, reel)), _control.QueueCenter(slot),
                        28f, _feel.Unit.FlyToQueueTime, null);
                }
                _lockedReel = -1;
                _control.Reels.ShowIdle();
            }
            else if (confirm == CommandResult.NotEnoughQueueSpace)
            {
                _control.MainButton.Deny();
                DenyQueue();
                Toast("대기 칸이 모자랍니다. 유닛을 먼저 배치하세요");
            }
        }

        private void DenySpin(CommandResult result)
        {
            _control.MainButton.Deny();
            if (result == CommandResult.NotEnoughMana)
            {
                _control.Mana.Deny(_sim.ManaExact, _data.Slot.SpinCost);
            }
            else if (result == CommandResult.QueueFull)
            {
                DenyQueue();
                Toast("대기 칸이 가득 찼습니다");
            }
        }

        private void DenyQueue()
        {
            foreach (var slot in _control.QueueSlots)
            {
                slot.Deny();
            }
        }

        public void PressRespin()
        {
            if (_sim.IsOver || _paused)
            {
                return;
            }
            var pending = _sim.PendingSpin;
            if (pending == null || _control.Reels.IsAnimating)
            {
                _control.RespinButton.Deny();
                return;
            }
            if (pending.RespinsUsed >= _data.Slot.RespinsPerSpin)
            {
                _control.RespinButton.Deny();
                Toast("재조립은 한 번만 할 수 있습니다");
                return;
            }
            if (_lockedReel < 0)
            {
                _control.RespinButton.Deny();
                Toast("고정할 칸을 먼저 누르세요");
                return;
            }
            var result = Execute(new RespinCommand(_lockedReel));
            if (result == CommandResult.Ok)
            {
                var locked = new bool[_control.Reels.Count];
                locked[_lockedReel] = true;
                _control.Reels.Start(_sim.PendingSpin.Symbols, locked, _sim.PendingSpin.IsJackpot);
            }
            else
            {
                _control.RespinButton.Deny();
                if (result == CommandResult.NotEnoughMana)
                {
                    _control.Mana.Deny(_sim.ManaExact, _data.Slot.RespinCost);
                }
            }
        }

        public void TapReel(int index)
        {
            OnReelTapped(index);
        }

        private void OnReelTapped(int index)
        {
            var pending = _sim.PendingSpin;
            if (_sim.IsOver || pending == null || _control.Reels.IsAnimating || pending.RespinsUsed >= _data.Slot.RespinsPerSpin)
            {
                return;
            }
            _lockedReel = _lockedReel == index ? -1 : index;
            _control.Reels.SetLocked(_lockedReel);
            PlayHaptic("light");
        }

        public void PressSkill()
        {
            if (_sim.IsOver || _paused)
            {
                return;
            }
            if (_aiming)
            {
                SetAiming(false);
                return;
            }
            if (!_sim.IsSkillReady)
            {
                _control.SkillButton.Deny();
                if (_sim.SkillCooldownTicks > 0)
                {
                    Toast("EMP 재충전 중: " + Mathf.CeilToInt(_sim.SkillCooldownTicks * TickDt) + "초");
                }
                else
                {
                    _control.Mana.Deny(_sim.ManaExact, _data.Battle.CommanderSkill.ManaCost);
                }
                return;
            }
            SetAiming(true);
        }

        private void SetAiming(bool aiming)
        {
            _aiming = aiming;
            _field.SetAiming(aiming);
        }

        private void OnAimPointer(PointerEventData e)
        {
            if (!_aiming)
            {
                return;
            }
            if (_field.ScreenToLocal(e.position, _camera, out var local) && _field.TryFromLocal(local, out int lane, out float y))
            {
                _field.ShowAim(lane, Mathf.Clamp(y, 0f, _sim.Config.LaneLength), _sim.Config.CommanderSkill.Radius);
            }
        }

        private void OnAimRelease(PointerEventData e)
        {
            if (!_aiming)
            {
                return;
            }
            if (_field.ScreenToLocal(e.position, _camera, out var local) && _field.TryFromLocal(local, out int lane, out float y))
            {
                FireSkillAt(lane, y);
            }
        }

        /// <summary>EMP를 쏜다(조준 해제 포함). 테스트에서도 같은 경로를 쓴다.</summary>
        public CommandResult FireSkillAt(int lane, float laneY)
        {
            var result = Execute(new UseCommanderSkillCommand(lane, Mathf.Clamp(laneY, 0f, _sim.Config.LaneLength)));
            SetAiming(false);
            if (result == CommandResult.Ok)
            {
                PlayHaptic("heavy");
            }
            else
            {
                _control.SkillButton.Deny();
            }
            return result;
        }

        // ───────────── 끌어다 놓기 ─────────────

        private void OnQueueBeginDrag(int index, PointerEventData e)
        {
            if (_sim.IsOver || _paused || index >= _sim.Queue.Count)
            {
                return;
            }
            SetAiming(false);
            _dragIndex = index;
            _control.QueueSlots[index].IsDragging = true;
            var queued = _sim.Queue[index];
            var style = UnitStyle.Get(queued.Def.Id);
            float size = 30f * _feel.Unit.DragScale + queued.Tier * 4f;
            Ui.SetShape(_dragGhost, style.Shape);
            Ui.SetShape(_dragGhostOutline, style.Shape);
            _dragGhost.color = style.Color;
            _dragGhost.rectTransform.sizeDelta = new Vector2(size, size);
            _dragGhostOutline.rectTransform.sizeDelta = new Vector2(size + 6f, size + 6f);
            _dragGhost.gameObject.SetActive(true);
            _dragGhostOutline.gameObject.SetActive(true);
            _dragGhostOutline.transform.SetAsLastSibling();
            _dragGhost.transform.SetAsLastSibling();
            OnQueueDrag(index, e);
        }

        private void OnQueueDrag(int index, PointerEventData e)
        {
            if (_dragIndex != index)
            {
                return;
            }
            UpdateDrag(e.position);
        }

        private void OnQueueEndDrag(int index, PointerEventData e)
        {
            if (_dragIndex != index)
            {
                return;
            }
            UpdateDrag(e.position);
            bool valid = _dragValid;
            int lane = _dragLane;
            float y = _dragY;
            CancelDrag();
            if (valid)
            {
                PlaceFromQueue(index, lane, y);
            }
        }

        private void UpdateDrag(Vector2 screenPoint)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPoint, _camera, out var rootLocal))
            {
                // 손가락에 가리지 않게 조금 위에 띄운다.
                var pos = rootLocal + new Vector2(0f, 36f);
                Ui.PlaceCentered(_dragGhost.rectTransform, pos, _dragGhost.rectTransform.sizeDelta);
                Ui.PlaceCentered(_dragGhostOutline.rectTransform, pos, _dragGhostOutline.rectTransform.sizeDelta);
            }
            _dragValid = false;
            if (_field.ScreenToLocal(screenPoint + new Vector2(0f, 36f * _lastScreen.PointScale), _camera, out var local)
                && _field.TryFromLocal(local, out int lane, out float y))
            {
                float zone = _sim.Config.PlayerZoneLength;
                // 중앙선을 조금 넘겨도 중앙선에 놓는다. 그보다 위는 놓을 수 없다.
                _dragValid = y <= zone + 1.5f;
                _dragLane = lane;
                _dragY = Mathf.Clamp(y, 0f, zone);
                var def = _sim.Queue[_dragIndex].Def;
                float range = def.Melee ? _sim.Config.MeleeRange : def.Range;
                _field.ShowDropPreview(true, lane, _dragY, range, _dragValid);
            }
            else
            {
                _field.ShowDropPreview(true, 0, 0f, 0f, false);
            }
        }

        private void CancelDrag()
        {
            if (_dragIndex >= 0 && _dragIndex < _control.QueueSlots.Length)
            {
                _control.QueueSlots[_dragIndex].IsDragging = false;
            }
            _dragIndex = -1;
            _dragValid = false;
            if (_dragGhost != null)
            {
                _dragGhost.gameObject.SetActive(false);
                _dragGhostOutline.gameObject.SetActive(false);
            }
            _field?.ShowDropPreview(false, 0, 0f, 0f);
        }

        /// <summary>대기 칸의 유닛을 놓는다. 끌어다 놓기와 테스트가 같은 경로를 쓴다.</summary>
        public CommandResult PlaceFromQueue(int index, int lane, float laneY)
        {
            var result = Execute(new PlaceUnitCommand(index, lane, laneY));
            if (result == CommandResult.Ok)
            {
                PlayHaptic(_feel.Haptics.UnitLanding);
            }
            return result;
        }

        // ───────────── 표시 갱신 ─────────────

        private void RefreshButtons()
        {
            var pending = _sim.PendingSpin;
            var main = _control.MainButton;
            var respin = _control.RespinButton;
            var skill = _control.SkillButton;
            bool over = _sim.IsOver;

            if (pending == null)
            {
                main.Label.text = "조립";
                main.SubLabel.text = "에너지 " + _data.Slot.SpinCost;
                main.SetColor(Ui.Hex("#2F7BFF"));
                main.Interactable = _sim.CanSpin;
                main.Pulse = _sim.CanSpin;
            }
            else
            {
                main.Label.text = "확정";
                main.SubLabel.text = pending.IsJackpot ? "합체형!" : pending.Summons.Count + "기 대기 칸으로";
                main.SetColor(Ui.Hex("#1FAA74"));
                main.Interactable = !over;
                main.Pulse = false;
            }

            bool canRespin = pending != null && !over && !_control.Reels.IsAnimating && pending.RespinsUsed < _data.Slot.RespinsPerSpin;
            respin.Interactable = canRespin && _lockedReel >= 0 && _sim.Mana >= _data.Slot.RespinCost;
            respin.SubLabel.text = canRespin ? (_lockedReel >= 0 ? "에너지 " + _data.Slot.RespinCost : "칸을 고정") : "";

            var skillCfg = _sim.Config.CommanderSkill;
            skill.Label.text = _aiming ? "취소" : "EMP";
            skill.SubLabel.text = "에너지 " + skillCfg.ManaCost;
            skill.Interactable = !over && (_aiming || _sim.IsSkillReady);
            float cooldownTotal = Mathf.Max(1f, skillCfg.CooldownSeconds * BattleSimulation.TickRate);
            _control.SkillCooldown.fillAmount = _sim.SkillCooldownTicks / cooldownTotal;
        }

        private void RefreshQueue()
        {
            var queue = _sim.Queue;
            for (int i = 0; i < _control.QueueSlots.Length; i++)
            {
                string signature = i < queue.Count ? queue[i].Def.Id + ":" + queue[i].Tier : "";
                if (_queueSignature[i] == signature)
                {
                    continue;
                }
                _queueSignature[i] = signature;
                if (i < queue.Count)
                {
                    _control.QueueSlots[i].SetContent(queue[i].Def, queue[i].Tier);
                }
                else
                {
                    _control.QueueSlots[i].SetContent(null, 0);
                }
            }
        }

        private void Toast(string message)
        {
            if (_layout == null)
            {
                return;
            }
            var position = new Vector2(_layout.Control.center.x, _layout.Control.yMax + 18f);
            _overlayFx.Text(position, message, Palette.Text, 15, 16f, 1.6f);
        }

        // ───────────── 설정 ─────────────

        private void OpenSettings()
        {
            if (_sim.IsOver)
            {
                return;
            }
            _paused = true;
            CancelDrag();
            SetAiming(false);
            _settings.Show(_layout.SafeArea, _haptics.Enabled, _haptics.IsSupported, _field.ShakeEnabled);
        }

        private void CloseSettings()
        {
            _settings.Hide();
            _paused = false;
        }

        private void ToggleHaptics()
        {
            if (!_haptics.IsSupported)
            {
                return;
            }
            _haptics.Enabled = !_haptics.Enabled;
            _settings.Refresh(_haptics.Enabled, _haptics.IsSupported, _field.ShakeEnabled);
            PlayHaptic("medium");
        }

        private void ToggleShake()
        {
            _field.ShakeEnabled = !_field.ShakeEnabled;
            _settings.Refresh(_haptics.Enabled, _haptics.IsSupported, _field.ShakeEnabled);
        }

        private void PlayHaptic(string strength)
        {
            if (_haptics == null || string.IsNullOrEmpty(strength))
            {
                return;
            }
            switch (strength.ToLowerInvariant())
            {
                case "heavy":
                    _haptics.Play(HapticStrength.Heavy);
                    break;
                case "medium":
                    _haptics.Play(HapticStrength.Medium);
                    break;
                default:
                    _haptics.Play(HapticStrength.Light);
                    break;
            }
        }
    }
}
