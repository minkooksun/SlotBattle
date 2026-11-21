using System;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlotBattle.Presentation
{
    /// <summary>
    /// 하단 조작 영역(엄지가 닿는 곳): 대기 칸 4개, 조립 라인 3칸, 에너지 게이지, 버튼 3개.
    /// iPad에서는 최대 폭 안에 가운데 정렬된다(설계서 4장).
    /// </summary>
    public sealed class ControlPanel
    {
        public const float QueueSlotSize = 56f;
        public const float ReelSize = 72f;
        public const float ButtonHeight = 56f;
        public const float SideButtonWidth = 84f;

        private readonly RectTransform _root;
        private readonly Image _background;

        public ControlPanel(Transform parent, FeelConfig feel, int queueCapacity, int reelCount, Action<int> onReelTapped)
        {
            _root = Ui.CreateRect("Control", parent);
            _background = Ui.CreateImage("Background", _root, Shape.Panel, Palette.Panel);

            QueueSlots = new QueueSlotView[queueCapacity];
            for (int i = 0; i < queueCapacity; i++)
            {
                QueueSlots[i] = QueueSlotView.Create(_root, i, feel);
            }
            Reels = new SlotReels(_root, feel.Slot, reelCount, onReelTapped);
            Mana = new ManaGauge(_root, feel.Mana);
            SkillButton = PressableButton.Create("EmpButton", _root, "EMP", Ui.Hex("#5B4BB7"), feel.Button, 18);
            MainButton = PressableButton.Create("MainButton", _root, "조립", Ui.Hex("#2F7BFF"), feel.Button, 22);
            RespinButton = PressableButton.Create("RespinButton", _root, "재조립", Ui.Hex("#3A4A7A"), feel.Button, 16);
            SkillButton.SubLabel = AddSubLabel(SkillButton);
            MainButton.SubLabel = AddSubLabel(MainButton);
            RespinButton.SubLabel = AddSubLabel(RespinButton);

            SkillCooldown = Ui.CreateImage("Cooldown", SkillButton.Visual, Shape.Panel, new Color(0, 0, 0, 0.55f));
            Ui.Fill(SkillCooldown.rectTransform);
            SkillCooldown.type = Image.Type.Filled;
            SkillCooldown.fillMethod = Image.FillMethod.Radial360;
            SkillCooldown.fillOrigin = (int)Image.Origin360.Top;
            SkillCooldown.fillClockwise = false;

            Fx = new FxLayer("ControlFx", _root);
        }

        public RectTransform Root => _root;
        public QueueSlotView[] QueueSlots { get; }
        public SlotReels Reels { get; }
        public ManaGauge Mana { get; }
        public PressableButton SkillButton { get; }
        public PressableButton MainButton { get; }
        public PressableButton RespinButton { get; }
        public Image SkillCooldown { get; }
        public FxLayer Fx { get; }
        public Vector2 Size { get; private set; }

        private static Text AddSubLabel(PressableButton button)
        {
            var sub = Ui.CreateText("Sub", button.Visual, "", 12, Palette.Energy);
            sub.rectTransform.anchorMin = new Vector2(0f, 0f);
            sub.rectTransform.anchorMax = new Vector2(1f, 0f);
            sub.rectTransform.pivot = new Vector2(0.5f, 0f);
            sub.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            sub.rectTransform.sizeDelta = new Vector2(0f, 16f);
            button.Label.rectTransform.anchoredPosition = new Vector2(0f, 6f);
            return sub;
        }

        public void Layout(Rect rect, int manaCells)
        {
            Ui.Place(_root, rect);
            Size = rect.size;
            float w = rect.width;
            float h = rect.height;
            Ui.Place(_background.rectTransform, new Rect(0, 0, w, h));

            // 아래에서 위로: 버튼, 에너지, 조립 라인, 대기 칸.
            float buttonsY = 8f;
            float manaY = buttonsY + ButtonHeight + 8f;
            float reelsY = manaY + 16f + 10f;
            float queueY = reelsY + ReelSize + 12f;

            float mainWidth = Mathf.Min(180f, w - SideButtonWidth * 2f - 36f);
            float centerX = w / 2f;
            Ui.Place((RectTransform)SkillButton.transform, new Rect(12f, buttonsY, SideButtonWidth, ButtonHeight));
            Ui.Place((RectTransform)MainButton.transform, new Rect(centerX - mainWidth / 2f, buttonsY, mainWidth, ButtonHeight));
            Ui.Place((RectTransform)RespinButton.transform, new Rect(w - 12f - SideButtonWidth, buttonsY, SideButtonWidth, ButtonHeight));

            Mana.Build(manaCells, new Rect(16f, manaY, w - 32f, 16f));

            float reelGap = 10f;
            float reelsWidth = Reels.Count * ReelSize + (Reels.Count - 1) * reelGap;
            var reelRects = new Rect[Reels.Count];
            for (int i = 0; i < Reels.Count; i++)
            {
                reelRects[i] = new Rect(centerX - reelsWidth / 2f + i * (ReelSize + reelGap), reelsY, ReelSize, ReelSize);
            }
            Reels.Layout(reelRects);

            float queueGap = 10f;
            float queueWidth = QueueSlots.Length * QueueSlotSize + (QueueSlots.Length - 1) * queueGap;
            for (int i = 0; i < QueueSlots.Length; i++)
            {
                QueueSlots[i].Layout(new Rect(centerX - queueWidth / 2f + i * (QueueSlotSize + queueGap), queueY, QueueSlotSize, QueueSlotSize));
            }
        }

        public Vector2 ReelCenter(int index)
        {
            var rt = Reels[index].Rect;
            return rt.anchoredPosition + rt.sizeDelta / 2f;
        }

        public Vector2 QueueCenter(int index)
        {
            var rt = QueueSlots[index].Rect;
            return rt.anchoredPosition + rt.sizeDelta / 2f;
        }
    }

    /// <summary>
    /// 대기 칸 한 칸. 잡으면 살짝 커지며 손가락을 따라오고, 전장에 놓으면 배치된다.
    /// 끌기 처리는 BattleController가 한다.
    /// </summary>
    public sealed class QueueSlotView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform _root;
        private Image _frame;
        private Image _glow;
        private Image _outline;
        private Image _icon;
        private Image[] _pips;
        private Text _tierText;
        private FeelConfig _feel;
        private float _popT = 1f;
        private float _denyT;
        private bool _pressed;
        private float _revealAt;

        public int Index { get; private set; }
        public RectTransform Rect => _root;
        public bool HasUnit { get; private set; }
        public bool IsDragging { get; set; }

        public Action<int, PointerEventData> BeginDrag;
        public Action<int, PointerEventData> Drag;
        public Action<int, PointerEventData> EndDrag;

        public static QueueSlotView Create(Transform parent, int index, FeelConfig feel)
        {
            var root = Ui.CreateRect("Queue" + index, parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;
            var view = root.gameObject.AddComponent<QueueSlotView>();
            view._root = root;
            view.Index = index;
            view._feel = feel;
            view._frame = Ui.CreateImage("Frame", root, Shape.Panel, Palette.PanelLight);
            view._glow = Ui.CreateImage("Glow", root, Shape.Glow, Color.white);
            view._outline = Ui.CreateImage("Outline", root, Shape.Circle, Palette.Player);
            view._icon = Ui.CreateImage("Icon", root, Shape.Circle, Color.white);
            view._pips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                view._pips[i] = Ui.CreateImage("Pip" + i, root, Shape.Circle, Palette.Energy);
            }
            view._tierText = Ui.CreateText("Tier", root, "", 10, Palette.TextDim);
            view.SetContent(null, 0);
            return view;
        }

        public void Layout(Rect rect)
        {
            Ui.Place(_root, rect);
            var s = rect.size;
            Ui.Place(_frame.rectTransform, new Rect(0, 0, s.x, s.y));
            var center = new Vector2(s.x / 2f, s.y / 2f + 4f);
            Ui.PlaceCentered(_glow.rectTransform, center, s * 1.3f);
            Ui.PlaceCentered(_outline.rectTransform, center, new Vector2(30f, 30f));
            Ui.PlaceCentered(_icon.rectTransform, center, new Vector2(24f, 24f));
            for (int i = 0; i < 3; i++)
            {
                Ui.PlaceCentered(_pips[i].rectTransform, new Vector2(s.x / 2f + (i - 1) * 9f, 8f), new Vector2(6f, 6f));
            }
            Ui.PlaceCentered(_tierText.rectTransform, new Vector2(s.x / 2f, s.y - 7f), new Vector2(s.x, 12f));
        }

        public void SetContent(UnitDef def, int tier)
        {
            bool had = HasUnit;
            HasUnit = def != null;
            _icon.gameObject.SetActive(HasUnit);
            _outline.gameObject.SetActive(HasUnit);
            _tierText.gameObject.SetActive(HasUnit);
            for (int i = 0; i < _pips.Length; i++)
            {
                _pips[i].gameObject.SetActive(HasUnit && i < tier);
            }
            if (!HasUnit)
            {
                _glow.gameObject.SetActive(false);
                return;
            }
            var style = UnitStyle.Get(def.Id);
            Ui.SetShape(_icon, style.Shape);
            Ui.SetShape(_outline, style.Shape);
            _icon.color = style.Color;
            _tierText.text = UnitStyle.TierName(tier);
            int tierIndex = Mathf.Clamp(tier - 1, 0, 2);
            float glow = _feel.Unit.GlowByTier.Length > tierIndex ? _feel.Unit.GlowByTier[tierIndex] : 0f;
            _glow.gameObject.SetActive(glow > 0f);
            _glow.color = Ui.WithAlpha(style.Color, glow * 0.8f);
            float iconSize = 22f + tier * 4f;
            _icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            _outline.rectTransform.sizeDelta = new Vector2(iconSize + 6f, iconSize + 6f);
            if (!had)
            {
                _popT = 0f;
            }
        }

        /// <summary>날아오는 아이콘이 도착할 때까지 숨겨 둔다.</summary>
        public void RevealAfter(float seconds)
        {
            _revealAt = Time.unscaledTime + seconds;
        }

        public void Deny()
        {
            _denyT = 0.4f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool hidden = Time.unscaledTime < _revealAt;
            if (!hidden && _popT < 1f)
            {
                _popT += dt / 0.2f;
            }
            float pop = _popT < 1f ? 1f + Mathf.Sin(Mathf.Clamp01(_popT) * Mathf.PI) * 0.35f : 1f;
            float press = _pressed && HasUnit ? _feel.Unit.DragScale : 1f;
            float alpha = IsDragging ? 0.25f : 1f;
            if (hidden)
            {
                alpha = 0f;
            }
            _icon.rectTransform.localScale = Vector3.one * pop * press;
            _outline.rectTransform.localScale = Vector3.one * pop * press;
            _icon.color = Ui.WithAlpha(_icon.color, alpha);
            _outline.color = Ui.WithAlpha(Palette.Player, alpha);
            _denyT = Mathf.Max(0f, _denyT - dt);
            _frame.color = _denyT > 0f && Mathf.Sin(_denyT * 40f) > 0f ? Palette.Deny : Palette.PanelLight;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (HasUnit)
            {
                BeginDrag?.Invoke(Index, eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (IsDragging)
            {
                Drag?.Invoke(Index, eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _pressed = false;
            if (IsDragging)
            {
                EndDrag?.Invoke(Index, eventData);
            }
        }
    }
}
