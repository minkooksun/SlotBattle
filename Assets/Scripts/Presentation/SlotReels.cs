using System;
using System.Collections.Generic;
using SlotBattle.Core.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlotBattle.Presentation
{
    /// <summary>
    /// 조립 라인(슬롯) 3칸의 연출. 결과는 Core가 먼저 정하고, 여기서는 보여 주기만 한다.
    /// - 왼쪽부터 차례로 멈춘다(stopInterval).
    /// - 앞서 멈춘 칸들이 실제로 같을 때만 마지막 칸을 더 오래 돌린다(결과를 속이지 않는다).
    /// - 멈출 때 살짝 지나쳤다 되돌아오고, 도는 동안은 흐리게 보인다.
    /// </summary>
    public sealed class SlotReels
    {
        private readonly SlotFeel _feel;
        private readonly ReelView[] _reels;
        private IReadOnlyList<UnitDef> _deck = new UnitDef[0];
        private float _time;
        private float _finishTime;
        private bool _jackpot;
        private bool _jackpotFired;
        private float _jackpotTime;

        public SlotReels(Transform parent, SlotFeel feel, int reelCount, Action<int> onReelTapped)
        {
            _feel = feel;
            _reels = new ReelView[reelCount];
            for (int i = 0; i < reelCount; i++)
            {
                _reels[i] = ReelView.Create(parent, i, onReelTapped);
            }
        }

        /// <summary>릴 하나가 멈출 때(진동, 소리용).</summary>
        public Action<int> ReelStopped;
        /// <summary>세 칸이 같아 잠깐 멈춘 뒤(번쩍임, 강한 진동용).</summary>
        public Action Jackpot;

        public bool IsAnimating { get; private set; }
        public int Count => _reels.Length;
        public ReelView this[int index] => _reels[index];

        public void SetDeck(IReadOnlyList<UnitDef> deck)
        {
            _deck = deck;
        }

        public void Layout(Rect[] rects)
        {
            for (int i = 0; i < _reels.Length; i++)
            {
                _reels[i].Layout(rects[i]);
            }
        }

        public void ShowIdle()
        {
            IsAnimating = false;
            foreach (var reel in _reels)
            {
                reel.ShowEmpty();
                reel.SetLocked(false);
            }
        }

        /// <summary>
        /// 연출을 시작한다. symbols는 Core가 정한 최종 결과, locked는 고정되어 돌지 않는 칸.
        /// 반환값: 마지막 칸이 멈추는 시각(연출 시작 기준 초).
        /// </summary>
        public float Start(IReadOnlyList<int> symbols, bool[] locked, bool jackpot)
        {
            _time = 0f;
            _jackpot = jackpot;
            _jackpotFired = false;
            IsAnimating = true;
            float stopAt = _feel.FirstStop;
            var shown = new List<int>();
            for (int i = 0; i < _reels.Length; i++)
            {
                if (locked != null && locked[i])
                {
                    _reels[i].ShowSymbol(Def(symbols[i]));
                    shown.Add(symbols[i]);
                }
            }
            var spinning = new List<int>();
            for (int i = 0; i < _reels.Length; i++)
            {
                if (locked == null || !locked[i])
                {
                    spinning.Add(i);
                }
            }
            for (int k = 0; k < spinning.Count; k++)
            {
                int reel = spinning[k];
                bool last = k == spinning.Count - 1;
                if (last && shown.Count >= 2 && AllSame(shown))
                {
                    // 앞의 칸들이 실제로 같을 때만 마지막 칸을 더 오래 돌린다.
                    stopAt += _feel.PairExtraDelay;
                }
                _reels[reel].Spin(Def(symbols[reel]), _feel.SpinStartDelay, stopAt, _feel, _deck);
                shown.Add(symbols[reel]);
                if (!last)
                {
                    stopAt += _feel.StopInterval;
                }
            }
            _jackpotTime = stopAt + _feel.SettleTime + _feel.JackpotPause;
            _finishTime = stopAt + _feel.SettleTime + (jackpot ? _feel.JackpotPause : 0f);
            return stopAt;
        }

        /// <summary>연출을 바로 끝낸다(연출이 조작을 막지 않게).</summary>
        public void Finish()
        {
            if (!IsAnimating)
            {
                return;
            }
            foreach (var reel in _reels)
            {
                if (reel.Finish())
                {
                    ReelStopped?.Invoke(reel.Index);
                }
            }
            if (_jackpot && !_jackpotFired)
            {
                _jackpotFired = true;
                Jackpot?.Invoke();
            }
            IsAnimating = false;
        }

        public void Update(float dt)
        {
            if (IsAnimating)
            {
                _time += dt;
            }
            foreach (var reel in _reels)
            {
                if (reel.Advance(dt))
                {
                    ReelStopped?.Invoke(reel.Index);
                }
            }
            if (!IsAnimating)
            {
                return;
            }
            if (_jackpot && !_jackpotFired && _time >= _jackpotTime)
            {
                _jackpotFired = true;
                Jackpot?.Invoke();
            }
            if (_time >= _finishTime && (!_jackpot || _jackpotFired))
            {
                IsAnimating = false;
            }
        }

        public void SetLocked(int index)
        {
            for (int i = 0; i < _reels.Length; i++)
            {
                _reels[i].SetLocked(i == index);
            }
        }

        private UnitDef Def(int symbol)
        {
            return symbol >= 0 && symbol < _deck.Count ? _deck[symbol] : null;
        }

        private static bool AllSame(List<int> values)
        {
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] != values[0])
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>릴 한 칸. 누르면 고정을 바꾼다.</summary>
    public sealed class ReelView : MonoBehaviour, IPointerClickHandler
    {
        private RectTransform _root;
        private RectTransform _window;
        private Image _frame;
        private Image _lockGlow;
        private Image _symbol;
        private Image _symbolOutline;
        private Image _ghostAbove;
        private Image _ghostBelow;
        private RectTransform _lockIcon;
        private Action<int> _onTapped;
        private SlotFeel _feel;
        private IReadOnlyList<UnitDef> _deck;
        private UnitDef _final;
        private float _t;
        private float _startDelay;
        private float _stopAt;
        private bool _spinning;
        private float _settleT = -1f;
        private float _scroll;
        private int _cycleIndex;
        private float _lockT;
        private bool _locked;

        public int Index { get; private set; }
        public RectTransform Rect => _root;
        public bool IsSpinning => _spinning;

        public static ReelView Create(Transform parent, int index, Action<int> onTapped)
        {
            var root = Ui.CreateRect("Reel" + index, parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;
            var view = root.gameObject.AddComponent<ReelView>();
            view._root = root;
            view.Index = index;
            view._onTapped = onTapped;

            view._lockGlow = Ui.CreateImage("LockGlow", root, Shape.PanelOutline, Palette.Lock);
            view._frame = Ui.CreateImage("Frame", root, Shape.Panel, Palette.PanelLight);
            view._window = Ui.CreateRect("Window", root);
            view._window.gameObject.AddComponent<RectMask2D>();
            view._ghostAbove = Ui.CreateImage("GhostAbove", view._window, Shape.Circle, Color.white);
            view._ghostBelow = Ui.CreateImage("GhostBelow", view._window, Shape.Circle, Color.white);
            view._symbolOutline = Ui.CreateImage("SymbolOutline", view._window, Shape.Circle, Palette.Player);
            view._symbol = Ui.CreateImage("Symbol", view._window, Shape.Circle, Color.white);

            // 자물쇠: 몸통 + 고리.
            view._lockIcon = Ui.CreateRect("Lock", root);
            var shackle = Ui.CreateImage("Shackle", view._lockIcon, Shape.Ring, Palette.Lock);
            Ui.PlaceCentered(shackle.rectTransform, new Vector2(11f, 15f), new Vector2(14f, 14f));
            var body = Ui.CreateImage("Body", view._lockIcon, Shape.Panel, Palette.Lock);
            body.pixelsPerUnitMultiplier = 10f;
            Ui.Place(body.rectTransform, new Rect(3f, 0f, 16f, 12f));
            view._lockIcon.gameObject.SetActive(false);
            view._lockGlow.gameObject.SetActive(false);
            view.ShowEmpty();
            return view;
        }

        public void Layout(Rect rect)
        {
            Ui.Place(_root, rect);
            var size = rect.size;
            Ui.Place(_frame.rectTransform, new Rect(0, 0, size.x, size.y));
            Ui.Place(_lockGlow.rectTransform, new Rect(-4f, -4f, size.x + 8f, size.y + 8f));
            Ui.Place(_window, new Rect(4f, 4f, size.x - 8f, size.y - 8f));
            Ui.Place(_lockIcon, new Rect(size.x - 26f, size.y - 26f, 22f, 22f));
            LayoutSymbols(0f, 1f);
        }

        private void LayoutSymbols(float offset, float stretch)
        {
            var w = _window.sizeDelta;
            float s = Mathf.Min(w.x, w.y) * 0.62f;
            var center = new Vector2(w.x / 2f, w.y / 2f + offset);
            var size = new Vector2(s, s * stretch);
            Ui.PlaceCentered(_symbol.rectTransform, center, size);
            Ui.PlaceCentered(_symbolOutline.rectTransform, center, size + new Vector2(6f, 6f * stretch));
            Ui.PlaceCentered(_ghostAbove.rectTransform, center + new Vector2(0f, w.y), size);
            Ui.PlaceCentered(_ghostBelow.rectTransform, center - new Vector2(0f, w.y), size);
        }

        public void ShowEmpty()
        {
            _spinning = false;
            _settleT = -1f;
            _final = null;
            _symbol.gameObject.SetActive(false);
            _symbolOutline.gameObject.SetActive(false);
            _ghostAbove.gameObject.SetActive(false);
            _ghostBelow.gameObject.SetActive(false);
        }

        public void ShowSymbol(UnitDef def)
        {
            _spinning = false;
            _settleT = -1f;
            _final = def;
            SetSymbol(_symbol, def, 1f);
            _symbolOutline.gameObject.SetActive(def != null);
            if (def != null)
            {
                // 아이콘 뒤 받침: 유닛 색의 둥근 판(아이콘이 없으면 도형 테두리).
                bool icon = UnitStyle.Icon(def.Id) != null;
                Ui.SetShape(_symbolOutline, icon ? Shape.RoundedSquare : UnitStyle.Get(def.Id).Shape);
                _symbolOutline.color = icon ? Ui.WithAlpha(UnitStyle.Get(def.Id).Color, 0.35f) : Palette.Player;
            }
            _ghostAbove.gameObject.SetActive(false);
            _ghostBelow.gameObject.SetActive(false);
            LayoutSymbols(0f, 1f);
        }

        public void Spin(UnitDef final, float startDelay, float stopAt, SlotFeel feel, IReadOnlyList<UnitDef> deck)
        {
            _final = final;
            _feel = feel;
            _deck = deck;
            _t = 0f;
            _startDelay = startDelay;
            _stopAt = stopAt;
            _spinning = true;
            _settleT = -1f;
            _scroll = 0f;
            _cycleIndex = UnityEngine.Random.Range(0, Mathf.Max(1, deck.Count));
            _symbolOutline.gameObject.SetActive(false);
        }

        /// <summary>즉시 멈춘다. 이번에 멈췄으면 true.</summary>
        public bool Finish()
        {
            if (!_spinning)
            {
                return false;
            }
            ShowSymbol(_final);
            return true;
        }

        public void SetLocked(bool locked)
        {
            if (locked && !_locked)
            {
                _lockT = 0f;
            }
            _locked = locked;
            _lockIcon.gameObject.SetActive(locked);
            _lockGlow.gameObject.SetActive(locked);
        }

        public UnitDef ShownDef => _final;

        /// <summary>프레임 진행. 이번 프레임에 멈췄으면 true.</summary>
        public bool Advance(float dt)
        {
            // 자물쇠가 "철컥" 잠기는 모습: 크게 나타났다가 줄어든다.
            if (_locked)
            {
                _lockT += dt;
                float t = Mathf.Clamp01(_lockT / 0.18f);
                float scale = Mathf.Lerp(1.8f, 1f, t * t);
                _lockIcon.localScale = new Vector3(scale, scale, 1f);
                float glow = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
                _lockGlow.color = Ui.WithAlpha(Palette.Lock, glow);
            }

            if (_settleT >= 0f && _feel != null)
            {
                // 멈출 때 살짝 지나쳤다 되돌아오는 반동.
                _settleT += dt;
                float t = Mathf.Clamp01(_settleT / Mathf.Max(0.01f, _feel.SettleTime));
                var h = _window.sizeDelta.y;
                LayoutSymbols(-Mathf.Sin(t * Mathf.PI) * _feel.Overshoot * h, 1f);
                if (t >= 1f)
                {
                    _settleT = -1f;
                }
            }

            if (!_spinning)
            {
                return false;
            }
            _t += dt;
            if (_t < _startDelay)
            {
                return false;
            }
            if (_t >= _stopAt)
            {
                ShowSymbol(_final);
                _settleT = 0f;
                return true;
            }
            // 빠르게 가속하며 그림이 흘러내린다.
            float elapsed = _t - _startDelay;
            float speed = _feel.SymbolsPerSecond * Mathf.Clamp01(elapsed / 0.08f);
            _scroll += speed * dt;
            while (_scroll >= 1f)
            {
                _scroll -= 1f;
                _cycleIndex++;
            }
            var height = _window.sizeDelta.y;
            int count = Mathf.Max(1, _deck.Count);
            SetSymbol(_symbol, _deck.Count > 0 ? _deck[_cycleIndex % count] : null, 0.65f);
            SetSymbol(_ghostAbove, _deck.Count > 0 ? _deck[(_cycleIndex + 1) % count] : null, 0.45f);
            SetSymbol(_ghostBelow, _deck.Count > 0 ? _deck[(_cycleIndex + count - 1) % count] : null, 0.45f);
            // 도는 동안은 세로로 늘려 흐림처럼 보이게 한다.
            LayoutSymbols(-_scroll * height, 1.35f);
            return false;
        }

        private static void SetSymbol(Image image, UnitDef def, float alpha)
        {
            image.gameObject.SetActive(def != null);
            if (def == null)
            {
                return;
            }
            UnitStyle.ApplyIcon(image, def.Id, alpha);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onTapped?.Invoke(Index);
        }
    }
}
