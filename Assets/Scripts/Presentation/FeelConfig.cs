using Newtonsoft.Json;

namespace SlotBattle.Presentation
{
    /// <summary>feel.json: 연출 수치. 코드를 고치지 않고 손맛을 조정한다.</summary>
    public sealed class FeelConfig
    {
        public LayoutFeel Layout = new LayoutFeel();
        public SlotFeel Slot = new SlotFeel();
        public ButtonFeel Button = new ButtonFeel();
        public HitFeel Hit = new HitFeel();
        public ShakeFeel Shake = new ShakeFeel();
        public DeathFeel Death = new DeathFeel();
        public UnitFeel Unit = new UnitFeel();
        public NumbersFeel Numbers = new NumbersFeel();
        public ManaFeel Mana = new ManaFeel();
        public HapticsFeel Haptics = new HapticsFeel();

        public static FeelConfig FromJson(string json)
        {
            return JsonConvert.DeserializeObject<FeelConfig>(json) ?? new FeelConfig();
        }
    }

    public sealed class LayoutFeel
    {
        /// <summary>전장의 가로/세로 비율. 화면이 이 비율과 다르면 남는 곳은 여백.</summary>
        public float BattlefieldAspect = 0.85f;
        /// <summary>하단 조작 영역의 최대 폭(pt). iPad에서 슬롯과 대기 칸이 지나치게 커지지 않게 한다.</summary>
        public float ControlMaxWidth = 440f;
        public float HudHeight = 44f;
        public float ControlHeight = 250f;
        public float MinTouchSize = 44f;
        public string MarginColor = "#0B0F1A";
    }

    public sealed class SlotFeel
    {
        public float SpinStartDelay = 0.05f;
        public float FirstStop = 0.35f;
        public float StopInterval = 0.25f;
        public float PairExtraDelay = 0.6f;
        public float SymbolsPerSecond = 22f;
        public float Overshoot = 0.18f;
        public float SettleTime = 0.12f;
        public float JackpotPause = 0.3f;
        public float JackpotFlashAlpha = 0.75f;
        public float JackpotFlashTime = 0.35f;
    }

    public sealed class ButtonFeel
    {
        public float PressScale = 0.92f;
        public float ReleaseTime = 0.18f;
        public float PulseScale = 1.06f;
        public float PulsePeriod = 1.1f;
        public float DenyShakeDistance = 8f;
        public float DenyShakeTime = 0.3f;
    }

    public sealed class HitFeel
    {
        public float FlashTime = 0.08f;
        /// <summary>뒤로 밀리는 거리(전장 거리 단위).</summary>
        public float Knockback = 0.12f;
        public float KnockbackTime = 0.1f;
        /// <summary>타격 순간 멈춤(화면 표시에만 적용, Core 시간은 그대로).</summary>
        public float HitStop = 0.04f;
        public float DamageNumberRise = 28f;
        public float DamageNumberTime = 0.6f;
        /// <summary>성 포격 포탄이 날아가는 시간과 크기.</summary>
        public float CastleShellTime = 0.22f;
        public float CastleShellSize = 10f;
    }

    public sealed class ShakeFeel
    {
        public float Tier3Attack = 6f;
        public float CastleHit = 4f;
        public float CastleDestroyed = 18f;
        public float JackpotLanding = 12f;
        public float Decay = 9f;
    }

    public sealed class DeathFeel
    {
        public int DebrisCount = 6;
        public float DebrisSpeed = 140f;
        public float DebrisTime = 0.45f;
        public float CastleSlowMotion = 0.35f;
        public float CastleSlowTime = 1.2f;
        public int CastleDebrisCount = 28;
    }

    public sealed class UnitFeel
    {
        public float[] SizeByTier = { 33f, 45f, 63f };
        public float[] GlowByTier = { 0f, 0.35f, 0.8f };
        public float DragScale = 1.2f;
        public float LandingTime = 0.25f;
        public float JackpotDropHeight = 160f;
        public float FlyToQueueTime = 0.35f;
    }

    public sealed class NumbersFeel
    {
        /// <summary>표시 숫자가 실제 값을 따라가는 속도(초당 남은 차이의 비율).</summary>
        public float CountSpeed = 10f;
        /// <summary>체력 막대의 흰 부분이 따라 줄기 시작할 때까지의 시간.</summary>
        public float TrailDelay = 0.35f;
        /// <summary>흰 부분이 줄어드는 속도(초당 최대 체력의 비율).</summary>
        public float TrailSpeed = 1.6f;
    }

    public sealed class ManaFeel
    {
        public float FillFlashTime = 0.18f;
        public float SpendBurstTime = 0.3f;
    }

    public sealed class HapticsFeel
    {
        public string ReelStop = "light";
        public string Jackpot = "heavy";
        public string CastleDestroyed = "heavy";
        public string UnitLanding = "medium";
    }
}
