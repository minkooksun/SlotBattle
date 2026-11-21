using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SlotBattle.Presentation
{
    /// <summary>화면 크기(픽셀), 안전 영역(픽셀), 포인트 배율. 테스트에서 iPhone·iPad 크기를 흉내 낼 수 있게 값으로 둔다.</summary>
    public struct ScreenInfo : IEquatable<ScreenInfo>
    {
        public Vector2 SizePx;
        public Rect SafeAreaPx;
        /// <summary>1pt가 몇 픽셀인가(@2x면 2, @3x면 3).</summary>
        public float PointScale;

        public ScreenInfo(Vector2 sizePx, Rect safeAreaPx, float pointScale)
        {
            SizePx = sizePx;
            SafeAreaPx = safeAreaPx;
            PointScale = pointScale;
        }

        public static ScreenInfo FromDevice()
        {
            var size = new Vector2(Screen.width, Screen.height);
            return new ScreenInfo(size, Screen.safeArea, DevicePointScale(size));
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern float SlotBattle_ScreenScale();
#endif

        private static float DevicePointScale(Vector2 sizePx)
        {
#if UNITY_IOS && !UNITY_EDITOR
            float native = SlotBattle_ScreenScale();
            if (native > 0f)
            {
                // Unity가 해상도를 낮춰 그릴 때를 대비해 실제 화면 픽셀과 렌더 픽셀의 비율도 반영한다.
                float renderRatio = Screen.currentResolution.width > 0 ? sizePx.x / Screen.currentResolution.width : 1f;
                return native * Mathf.Clamp(renderRatio, 0.25f, 1f);
            }
#endif
            // 에디터·기타: 짧은 변이 iPhone(393pt) 정도로 보이게 맞춘다.
            float shortSide = Mathf.Min(sizePx.x, sizePx.y);
            return Mathf.Max(1f, shortSide / 393f);
        }

        public bool Equals(ScreenInfo other)
        {
            return SizePx == other.SizePx && SafeAreaPx == other.SafeAreaPx && Mathf.Approximately(PointScale, other.PointScale);
        }

        public override bool Equals(object obj) => obj is ScreenInfo other && Equals(other);
        public override int GetHashCode() => SizePx.GetHashCode() ^ SafeAreaPx.GetHashCode() ^ PointScale.GetHashCode();
    }

    /// <summary>화면 배치 결과. 모든 값은 캔버스 pt 좌표(왼쪽 아래가 원점).</summary>
    public sealed class LayoutResult
    {
        public Vector2 CanvasSize;
        public Rect SafeArea;
        public Rect Hud;
        public Rect Battlefield;
        public Rect PlayerCastleBar;
        public Rect Control;
    }

    /// <summary>
    /// 설계서 4장 "대상 기기와 화면": 전장은 고정 비율, 남는 곳은 여백,
    /// 모든 UI는 안전 영역 안, 하단 조작 영역은 최대 폭.
    /// </summary>
    public static class LayoutCalculator
    {
        public const float BarHeight = 22f;
        public const float Gap = 6f;

        public static LayoutResult Compute(ScreenInfo screen, LayoutFeel feel)
        {
            float scale = Mathf.Max(0.01f, screen.PointScale);
            var canvas = screen.SizePx / scale;
            var safe = new Rect(screen.SafeAreaPx.x / scale, screen.SafeAreaPx.y / scale,
                screen.SafeAreaPx.width / scale, screen.SafeAreaPx.height / scale);

            float controlWidth = Mathf.Min(safe.width, feel.ControlMaxWidth);
            var control = new Rect(safe.center.x - controlWidth / 2f, safe.yMin, controlWidth, feel.ControlHeight);
            var hud = new Rect(safe.center.x - controlWidth / 2f, safe.yMax - feel.HudHeight, controlWidth, feel.HudHeight);

            // 전장이 들어갈 수 있는 곳: HUD 아래, 내 성 막대와 조작 영역 위.
            float availBottom = control.yMax + BarHeight + Gap * 2f;
            float availTop = hud.yMin - Gap;
            var avail = new Rect(safe.xMin, availBottom, safe.width, Mathf.Max(0f, availTop - availBottom));

            float aspect = Mathf.Max(0.1f, feel.BattlefieldAspect);
            float width = Mathf.Min(avail.width, avail.height * aspect);
            float height = width / aspect;
            // 가로는 가운데, 세로는 조작 영역 쪽(엄지 쪽)에 붙인다. 남는 곳은 여백.
            var field = new Rect(avail.center.x - width / 2f, avail.yMin, width, height);

            var bar = new Rect(field.xMin, field.yMin - Gap - BarHeight, field.width, BarHeight);

            return new LayoutResult
            {
                CanvasSize = canvas,
                SafeArea = safe,
                Hud = hud,
                Battlefield = field,
                PlayerCastleBar = bar,
                Control = control,
            };
        }
    }
}
