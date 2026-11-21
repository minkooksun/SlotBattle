using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlotBattle.Presentation
{
    /// <summary>
    /// 누르는 순간 줄었다가 탄성 있게 돌아오는 버튼(설계서 8장 "모든 버튼").
    /// 눌림 판정 영역은 그대로 두고 안쪽 Visual만 움직인다.
    /// </summary>
    public sealed class PressableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public RectTransform Visual;
        public Image Background;
        public Text Label;
        public Text SubLabel;
        public Action OnClick;
        public bool Pulse;

        private ButtonFeel _feel;
        private bool _pressed;
        private float _releaseT = 1f;
        private float _denyT = -1f;
        private bool _interactable = true;
        private Color _baseColor;

        public bool Interactable
        {
            get => _interactable;
            set
            {
                _interactable = value;
                if (Background != null)
                {
                    Background.color = value ? _baseColor : Color.Lerp(_baseColor, Palette.Panel, 0.65f);
                }
            }
        }

        public static PressableButton Create(string name, Transform parent, string label, Color color, ButtonFeel feel, int fontSize = 20)
        {
            var root = Ui.CreateRect(name, parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;

            var visual = Ui.CreateRect("Visual", root);
            Ui.Fill(visual);
            var bg = visual.gameObject.AddComponent<Image>();
            Ui.SetShape(bg, Shape.Panel);
            bg.color = color;
            bg.raycastTarget = false;

            var text = Ui.CreateText("Label", visual, label, fontSize, Palette.Text);
            Ui.Fill(text.rectTransform);

            var button = root.gameObject.AddComponent<PressableButton>();
            button.Visual = visual;
            button.Background = bg;
            button.Label = text;
            button._feel = feel;
            button._baseColor = color;
            return button;
        }

        public void SetColor(Color color)
        {
            _baseColor = color;
            Interactable = _interactable;
        }

        /// <summary>안 되는 조작일 때 좌우로 짧게 떤다.</summary>
        public void Deny()
        {
            _denyT = 0f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            _releaseT = 0f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClick?.Invoke();
        }

        /// <summary>테스트용: 누른 것과 같게 처리한다.</summary>
        public void SimulateClick()
        {
            _releaseT = 0f;
            OnClick?.Invoke();
        }

        private void Update()
        {
            if (Visual == null || _feel == null)
            {
                return;
            }
            float dt = Time.unscaledDeltaTime;
            float scale;
            if (_pressed)
            {
                scale = _feel.PressScale;
            }
            else
            {
                _releaseT += dt;
                float t = Mathf.Clamp01(_releaseT / Mathf.Max(0.01f, _feel.ReleaseTime));
                scale = 1f + (_feel.PressScale - 1f) * (1f - t) * Mathf.Cos(t * Mathf.PI * 2.5f);
                if (Pulse && _interactable && t >= 1f)
                {
                    float phase = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.1f, _feel.PulsePeriod));
                    scale *= 1f + (_feel.PulseScale - 1f) * 0.5f * (1f + phase);
                }
            }
            Visual.localScale = new Vector3(scale, scale, 1f);

            float offset = 0f;
            if (_denyT >= 0f)
            {
                _denyT += dt;
                float t = _denyT / Mathf.Max(0.01f, _feel.DenyShakeTime);
                if (t >= 1f)
                {
                    _denyT = -1f;
                }
                else
                {
                    offset = Mathf.Sin(_denyT * 60f) * _feel.DenyShakeDistance * (1f - t);
                }
            }
            Visual.anchoredPosition = new Vector2(offset, 0f);
        }
    }

    /// <summary>
    /// 성 체력 막대: 숫자는 빠르게 세어 내려가고, 줄어든 부분은 흰색으로 남았다가 뒤늦게 따라 준다.
    /// </summary>
    public sealed class CastleBar
    {
        private readonly RectTransform _root;
        private readonly Image _trail;
        private readonly Image _fill;
        private readonly Image _flash;
        private readonly Text _label;
        private readonly string _title;
        private readonly NumbersFeel _feel;
        private float _max = 1f;
        private float _target = 1f;
        private float _shown = 1f;
        private float _trailValue = 1f;
        private float _trailWait;
        private float _flashT;

        public CastleBar(Transform parent, string title, Color color, NumbersFeel feel)
        {
            _title = title;
            _feel = feel;
            _root = Ui.CreateRect("CastleBar_" + title, parent);
            var bg = Ui.CreateImage("Bg", _root, Shape.Panel, Palette.EnergyEmpty);
            Ui.Fill(bg.rectTransform);
            _trail = Ui.CreateImage("Trail", _root, Shape.Square, Palette.Trail);
            Ui.Fill(_trail.rectTransform);
            _trail.type = Image.Type.Filled;
            _trail.fillMethod = Image.FillMethod.Horizontal;
            _fill = Ui.CreateImage("Fill", _root, Shape.Square, color);
            Ui.Fill(_fill.rectTransform);
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _flash = Ui.CreateImage("Flash", _root, Shape.Square, new Color(1, 1, 1, 0));
            Ui.Fill(_flash.rectTransform);
            _label = Ui.CreateText("Label", _root, title, 13, Palette.Text);
            Ui.Fill(_label.rectTransform);
            var outline = _label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.6f);
        }

        public RectTransform Root => _root;
        public float ShownValue => _shown;

        public void Reset(float max)
        {
            _max = Mathf.Max(1f, max);
            _target = _max;
            _shown = _max;
            _trailValue = _max;
            _flashT = 0f;
            Render(0f);
        }

        public void SetValue(float hp)
        {
            float clamped = Mathf.Max(0f, hp);
            if (clamped < _target)
            {
                _trailWait = _feel.TrailDelay;
                _flashT = 0.12f;
            }
            _target = clamped;
        }

        public void Render(float dt)
        {
            _shown = Mathf.Lerp(_shown, _target, Mathf.Clamp01(dt * _feel.CountSpeed));
            if (Mathf.Abs(_shown - _target) < 0.5f)
            {
                _shown = _target;
            }
            if (_trailWait > 0f)
            {
                _trailWait -= dt;
            }
            else
            {
                _trailValue = Mathf.MoveTowards(_trailValue, _shown, _feel.TrailSpeed * _max * dt);
            }
            _trailValue = Mathf.Max(_trailValue, _shown);
            _fill.fillAmount = _shown / _max;
            _trail.fillAmount = _trailValue / _max;
            _flashT = Mathf.Max(0f, _flashT - dt);
            _flash.color = new Color(1, 1, 1, _flashT * 2f);
            _label.text = _title + "  " + Mathf.CeilToInt(_shown).ToString("N0");
        }
    }

    /// <summary>
    /// 마나(에너지) 게이지: 칸 단위로 부드럽게 차오르고, 한 칸이 찰 때마다 반짝인다.
    /// 쓸 때는 쓴 만큼 한 번에 빠지며 퍼지고, 모자라면 모자란 칸이 빨갛게 깜빡인다.
    /// </summary>
    public sealed class ManaGauge
    {
        private readonly RectTransform _root;
        private readonly ManaFeel _feel;
        private Image[] _cells = new Image[0];
        private Image[] _fills = new Image[0];
        private Image[] _flashes = new Image[0];
        private float[] _flashT = new float[0];
        private float[] _burstT = new float[0];
        private float[] _denyT = new float[0];
        private float _last = -1f;
        private Text _label;

        public ManaGauge(Transform parent, ManaFeel feel)
        {
            _feel = feel;
            _root = Ui.CreateRect("ManaGauge", parent);
            _label = Ui.CreateText("Label", _root, "", 13, Palette.Energy, TextAnchor.MiddleRight);
        }

        public RectTransform Root => _root;

        public void Build(int cells, Rect area)
        {
            Ui.Place(_root, area);
            foreach (var cell in _cells)
            {
                if (cell != null)
                {
                    UnityEngine.Object.Destroy(cell.gameObject);
                }
            }
            _cells = new Image[cells];
            _fills = new Image[cells];
            _flashes = new Image[cells];
            _flashT = new float[cells];
            _burstT = new float[cells];
            _denyT = new float[cells];
            float labelWidth = 34f;
            float gap = 3f;
            float width = (area.width - labelWidth - gap * (cells - 1)) / cells;
            for (int i = 0; i < cells; i++)
            {
                var cell = Ui.CreateImage("Cell" + i, _root, Shape.Panel, Palette.EnergyEmpty);
                Ui.Place(cell.rectTransform, new Rect(i * (width + gap), 0f, width, area.height));
                cell.pixelsPerUnitMultiplier = 8f;
                var fill = Ui.CreateImage("Fill", cell.transform, Shape.Square, Palette.Energy);
                Ui.Fill(fill.rectTransform);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                var flash = Ui.CreateImage("Flash", cell.transform, Shape.Panel, new Color(1, 1, 1, 0));
                Ui.Fill(flash.rectTransform);
                flash.pixelsPerUnitMultiplier = 8f;
                _cells[i] = cell;
                _fills[i] = fill;
                _flashes[i] = flash;
            }
            Ui.Place(_label.rectTransform, new Rect(area.width - labelWidth, 0f, labelWidth, area.height));
            _last = -1f;
        }

        /// <summary>모자란 칸(가진 양부터 필요한 양까지)을 빨갛게 깜빡인다.</summary>
        public void Deny(float have, int need)
        {
            for (int i = Mathf.FloorToInt(have); i < need && i < _denyT.Length; i++)
            {
                _denyT[i] = 0.45f;
            }
        }

        public void Render(float mana, float dt)
        {
            int cells = _cells.Length;
            if (_last >= 0f)
            {
                int before = Mathf.FloorToInt(_last + 1e-4f);
                int after = Mathf.FloorToInt(mana + 1e-4f);
                if (after > before && after - 1 < cells)
                {
                    _flashT[after - 1] = _feel.FillFlashTime;
                }
                if (mana < _last - 0.5f)
                {
                    for (int i = Mathf.FloorToInt(mana); i < Mathf.CeilToInt(_last) && i < cells; i++)
                    {
                        _burstT[i] = _feel.SpendBurstTime;
                    }
                }
            }
            _last = mana;
            for (int i = 0; i < cells; i++)
            {
                float amount = Mathf.Clamp01(mana - i);
                _fills[i].fillAmount = amount;
                _fills[i].color = amount >= 1f ? Palette.Energy : Color.Lerp(Palette.EnergyEmpty, Palette.Energy, 0.55f);

                _flashT[i] = Mathf.Max(0f, _flashT[i] - dt);
                _burstT[i] = Mathf.Max(0f, _burstT[i] - dt);
                _denyT[i] = Mathf.Max(0f, _denyT[i] - dt);
                float flash = _feel.FillFlashTime > 0f ? _flashT[i] / _feel.FillFlashTime : 0f;
                float burst = _feel.SpendBurstTime > 0f ? _burstT[i] / _feel.SpendBurstTime : 0f;
                float deny = _denyT[i] > 0f ? (Mathf.Sin(_denyT[i] * 40f) > 0f ? 0.85f : 0.2f) : 0f;
                var color = deny > 0f ? Ui.WithAlpha(Palette.Deny, deny) : new Color(1, 1, 1, Mathf.Max(flash, burst) * 0.8f);
                _flashes[i].color = color;
                float burstScale = 1f + (1f - burst) * 0.35f * (burst > 0f ? 1f : 0f);
                _flashes[i].rectTransform.localScale = new Vector3(burstScale, burstScale, 1f);
            }
            _label.text = Mathf.FloorToInt(mana + 1e-4f) + "/" + cells;
        }
    }
}
