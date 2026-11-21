using System;
using SlotBattle.Core.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace SlotBattle.Presentation
{
    /// <summary>위쪽 HUD: 적 코어 기지 체력, 남은 시간, 설정 버튼.</summary>
    public sealed class HudView
    {
        private readonly RectTransform _root;
        private readonly Text _timer;
        private float _timerPulse;
        private int _lastSecond = -1;

        public HudView(Transform parent, FeelConfig feel)
        {
            _root = Ui.CreateRect("Hud", parent);
            EnemyBar = new CastleBar(_root, "적 코어 기지", Palette.Enemy, feel.Numbers);
            _timer = Ui.CreateText("Timer", _root, "3:00", 20, Palette.Text);
            SettingsButton = PressableButton.Create("SettingsButton", _root, "설정", Palette.PanelLight, feel.Button, 13);
        }

        public RectTransform Root => _root;
        public CastleBar EnemyBar { get; }
        public PressableButton SettingsButton { get; }

        public void Layout(Rect rect)
        {
            Ui.Place(_root, rect);
            float w = rect.width;
            float h = rect.height;
            float button = Mathf.Max(44f, h);
            float timerWidth = 64f;
            Ui.Place(EnemyBar.Root, new Rect(8f, (h - 24f) / 2f, w - button - timerWidth - 20f, 24f));
            Ui.Place(_timer.rectTransform, new Rect(w - button - timerWidth - 6f, 0f, timerWidth, h));
            Ui.Place((RectTransform)SettingsButton.transform, new Rect(w - button - 4f, (h - button) / 2f, button, button));
        }

        /// <summary>남은 시간. 마지막 30초는 색을 바꾸고 초가 바뀔 때마다 강조한다.</summary>
        public void Render(BattleSimulation sim, float dt)
        {
            float remaining = Mathf.Max(0f, sim.RemainingSeconds);
            int seconds = Mathf.CeilToInt(remaining);
            _timer.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            bool final = sim.IsFinalPhase && !sim.IsOver;
            if (final && seconds != _lastSecond)
            {
                _timerPulse = 0.25f;
            }
            _lastSecond = seconds;
            _timerPulse = Mathf.Max(0f, _timerPulse - dt);
            _timer.color = final ? Palette.Energy : Palette.Text;
            float scale = 1f + _timerPulse * 1.2f;
            _timer.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>화면 전체를 덮는 창(결과, 설정) 공통.</summary>
    public abstract class OverlayPanel
    {
        protected readonly RectTransform Root;
        protected readonly RectTransform Card;
        private float _showT;

        protected OverlayPanel(string name, Transform parent)
        {
            Root = Ui.CreateRect(name, parent);
            Root.anchorMin = Vector2.zero;
            Root.anchorMax = Vector2.one;
            Root.sizeDelta = Vector2.zero;
            var dim = Ui.CreateImage("Dim", Root, Shape.Square, new Color(0, 0, 0, 0.6f), true);
            Ui.Fill(dim.rectTransform);
            Card = Ui.CreateRect("Card", Root);
            var bg = Ui.CreateImage("Bg", Card, Shape.Panel, Palette.Panel);
            Ui.Fill(bg.rectTransform);
            Root.gameObject.SetActive(false);
        }

        public bool IsVisible => Root.gameObject.activeSelf;

        public void Show(Rect safeArea)
        {
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            var size = CardSize(safeArea);
            Ui.PlaceCentered(Card, safeArea.center, size);
            _showT = 0f;
        }

        public void Hide()
        {
            Root.gameObject.SetActive(false);
        }

        public void Render(float dt)
        {
            if (!IsVisible)
            {
                return;
            }
            _showT += dt;
            float t = Mathf.Clamp01(_showT / 0.2f);
            float scale = Mathf.Lerp(0.85f, 1f, 1f - (1f - t) * (1f - t));
            Card.localScale = new Vector3(scale, scale, 1f);
        }

        protected abstract Vector2 CardSize(Rect safeArea);
    }

    /// <summary>결과 화면: 한 번의 터치로 바로 다시 시작한다.</summary>
    public sealed class ResultPanel : OverlayPanel
    {
        private readonly Text _title;
        private readonly Text _detail;

        public ResultPanel(Transform parent, FeelConfig feel) : base("Result", parent)
        {
            _title = Ui.CreateText("Title", Card, "", 40, Palette.Text);
            _detail = Ui.CreateText("Detail", Card, "", 16, Palette.TextDim);
            _detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            RetryButton = PressableButton.Create("Retry", Card, "다시 하기", Ui.Hex("#2F7BFF"), feel.Button, 22);
        }

        public PressableButton RetryButton { get; }

        protected override Vector2 CardSize(Rect safeArea)
        {
            return new Vector2(Mathf.Min(320f, safeArea.width - 32f), 250f);
        }

        public void Show(Rect safeArea, BattleSimulation sim)
        {
            base.Show(safeArea);
            var size = Card.sizeDelta;
            string title;
            Color color;
            switch (sim.Outcome)
            {
                case BattleOutcome.Win:
                    title = "승리!";
                    color = Palette.Accent;
                    break;
                case BattleOutcome.Lose:
                    title = "패배";
                    color = Palette.Enemy;
                    break;
                default:
                    title = "무승부";
                    color = Palette.Energy;
                    break;
            }
            _title.text = title;
            _title.color = color;
            string reason = sim.EndedByTimeout ? "시간 종료 · 코어 기지 체력 비율로 판정" : "코어 기지 파괴";
            int seconds = Mathf.RoundToInt(sim.ElapsedSeconds);
            _detail.text = reason + "\n"
                + "내 기지 " + Mathf.RoundToInt(sim.PlayerCastle.HpRatio * 100f) + "%  ·  적 기지 " + Mathf.RoundToInt(sim.EnemyCastle.HpRatio * 100f) + "%\n"
                + "경과 " + (seconds / 60) + ":" + (seconds % 60).ToString("00");
            Ui.PlaceCentered(_title.rectTransform, new Vector2(size.x / 2f, size.y - 50f), new Vector2(size.x, 50f));
            Ui.PlaceCentered(_detail.rectTransform, new Vector2(size.x / 2f, size.y - 120f), new Vector2(size.x - 32f, 70f));
            Ui.Place((RectTransform)RetryButton.transform, new Rect(24f, 20f, size.x - 48f, 60f));
        }
    }

    /// <summary>설정: 진동과 화면 흔들림 끄기, 처음부터. (저장은 M4에서 붙인다.)</summary>
    public sealed class SettingsPanel : OverlayPanel
    {
        public SettingsPanel(Transform parent, FeelConfig feel) : base("Settings", parent)
        {
            Title = Ui.CreateText("Title", Card, "설정", 24, Palette.Text);
            HapticsButton = PressableButton.Create("Haptics", Card, "", Palette.PanelLight, feel.Button, 18);
            ShakeButton = PressableButton.Create("Shake", Card, "", Palette.PanelLight, feel.Button, 18);
            RestartButton = PressableButton.Create("Restart", Card, "처음부터", Ui.Hex("#8A3A4A"), feel.Button, 18);
            CloseButton = PressableButton.Create("Close", Card, "계속하기", Ui.Hex("#2F7BFF"), feel.Button, 18);
        }

        public Text Title { get; }
        public PressableButton HapticsButton { get; }
        public PressableButton ShakeButton { get; }
        public PressableButton RestartButton { get; }
        public PressableButton CloseButton { get; }

        protected override Vector2 CardSize(Rect safeArea)
        {
            return new Vector2(Mathf.Min(300f, safeArea.width - 32f), 330f);
        }

        public void Show(Rect safeArea, bool hapticsOn, bool hapticsSupported, bool shakeOn)
        {
            base.Show(safeArea);
            var size = Card.sizeDelta;
            float w = size.x - 40f;
            Ui.PlaceCentered(Title.rectTransform, new Vector2(size.x / 2f, size.y - 34f), new Vector2(size.x, 30f));
            Ui.Place((RectTransform)HapticsButton.transform, new Rect(20f, size.y - 128f, w, 52f));
            Ui.Place((RectTransform)ShakeButton.transform, new Rect(20f, size.y - 190f, w, 52f));
            Ui.Place((RectTransform)RestartButton.transform, new Rect(20f, 82f, w, 52f));
            Ui.Place((RectTransform)CloseButton.transform, new Rect(20f, 20f, w, 52f));
            Refresh(hapticsOn, hapticsSupported, shakeOn);
        }

        public void Refresh(bool hapticsOn, bool hapticsSupported, bool shakeOn)
        {
            HapticsButton.Label.text = hapticsSupported ? "진동  " + (hapticsOn ? "켜짐" : "꺼짐") : "진동  (이 기기는 지원 안 함)";
            HapticsButton.Interactable = hapticsSupported;
            ShakeButton.Label.text = "화면 흔들림  " + (shakeOn ? "켜짐" : "꺼짐");
        }
    }
}
