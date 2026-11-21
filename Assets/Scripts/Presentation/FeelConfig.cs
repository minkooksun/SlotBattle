using Newtonsoft.Json;
using SlotBattle.Core.Data;

namespace SlotBattle.Presentation
{
    /// <summary>feel.json: 연출 수치. 코드를 고치지 않고 손맛을 조정한다.</summary>
    public sealed class FeelConfig
    {
        public LayoutFeel Layout = new LayoutFeel();
        public WorldFeel World = new WorldFeel();
        public SlotFeel Slot = new SlotFeel();
        public ButtonFeel Button = new ButtonFeel();
        public HitFeel Hit = new HitFeel();
        public AttackFeel Attack = new AttackFeel();
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

    /// <summary>3D 전장(설계서 4장 "3D 전장"): 카메라, 조명, 색, 크기. 거리 단위는 Core와 같다(라인 길이 20).</summary>
    public sealed class WorldFeel
    {
        public float FieldWidth = 10f;
        public float CameraHeight = 19f;
        /// <summary>카메라가 내 성(거리 0)보다 얼마나 뒤에 있는가.</summary>
        public float CameraBack = 9f;
        /// <summary>카메라가 바라보는 바닥 위 거리.</summary>
        public float LookAtZ = 8.5f;
        public float Fov = 44f;
        public string Background = "#0E1424";
        public string Ground = "#161E36";
        public string Lane = "#22305A";
        public string LaneEdge = "#3A4C86";
        public string DropZone = "#2F5BB0";
        public string RangeBand = "#3C7A6A";
        public string Ambient = "#56607E";
        public float LightIntensity = 1.2f;
        public float LightPitch = 55f;
        public float LightYaw = -35f;
        public bool Shadows = true;
        public int Msaa = 4;
        /// <summary>렌더 텍스처의 긴 변 최대 픽셀.</summary>
        public int MaxTexture = 1600;
        /// <summary>로봇 시안의 기준 폭(시안 단위). 이 폭이 유닛 크기(전장 폭 × 단계 비율)가 되게 줄인다.</summary>
        public float RobotFootprint = 2.6f;
        public float AirHeight = 2.4f;
        public float CastleDepth = 2f;
        public float CastleHeight = 1.1f;
        public float LobArcHeight = 2.5f;
        /// <summary>3단계 유닛이 떨어지기 시작하는 높이.</summary>
        public float DropHeight = 6f;
        /// <summary>걸음 한 번에 가는 거리(다리 흔들림 주기).</summary>
        public float WalkStride = 1.6f;
        public float WalkSwing = 28f;
        public float ProjectileSize = 0.5f;
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
    }

    /// <summary>
    /// 유닛별 공격 연출(설계서 8장 "유닛별 공격 연출"). 움직임 거리(pullBack, lunge, chargeLunge)와
    /// 크기 배율(slashSize 등)은 유닛 크기에 곱하고, 나머지 크기는 pt.
    /// </summary>
    public sealed class AttackFeel
    {
        public WindupFeel Windup = new WindupFeel();
        /// <summary>스웜봇 3기가 엇갈려 찌르도록 한 기마다 더하는 준비 시간. 준비 시간 합이 0.2초를 넘지 않게 둔다.</summary>
        public float SwarmStagger = 0.025f;
        public float PullBack = 0.35f;
        public float Lunge = 0.7f;
        public float ChargeLunge = 1.4f;
        public float StrikeTime = 0.18f;
        public float Crouch = 0.7f;
        public float Swell = 1.7f;
        public float SlashSize = 2.4f;
        /// <summary>검 궤적 호의 길이(원 둘레 대비 비율).</summary>
        public float SlashArc = 0.45f;
        public float SlashTime = 0.25f;
        public float BashRingSize = 2.4f;
        public float BashTime = 0.28f;
        public float StabLength = 1.4f;
        public float StabTime = 0.16f;
        public int GhostCount = 3;
        public float GhostTime = 0.3f;
        public float MuzzleFlashSize = 1.0f;
        public float MuzzleFlashTime = 0.14f;
        public float BoltLength = 28f;
        public float BoltWidth = 7f;
        public float DroneBoltSize = 9f;
        public float DroneBurstGap = 14f;
        public float LobSize = 18f;
        /// <summary>곡사탄 포물선의 가장 높은 곳(pt). 규칙과 무관한 모양이다.</summary>
        public float LobArcHeight = 60f;
        public float ShellSize = 14f;
        public int SparkCount = 8;
        public float SparkSpeed = 160f;
        public float SparkTime = 0.3f;
        public float ExplosionTime = 0.45f;
        public float ExplosionShake = 5f;
        /// <summary>성 포탑이 대상 쪽으로 도는 빠르기(클수록 빨리 맞춘다).</summary>
        public float TurretTurnSpeed = 14f;
        public float TurretRecoil = 4f;
    }

    /// <summary>
    /// 공격 준비 동작 시간(초). 화면 연출이라 Core 시간은 바꾸지 않고,
    /// 그 공격에서 나온 사건을 이 시간만큼 늦춰 보여 준다(0.1~0.2초).
    /// </summary>
    public sealed class WindupFeel
    {
        public float Melee = 0.15f;
        public float Bolt = 0.15f;
        public float Lob = 0.2f;
        public float Explode = 0.2f;
        public float Charge = 0.2f;
        public float Castle = 0.2f;

        public float For(AttackType type)
        {
            switch (type)
            {
                case AttackType.Melee: return Melee;
                case AttackType.Bolt: return Bolt;
                case AttackType.Lob: return Lob;
                case AttackType.Explode: return Explode;
                case AttackType.Charge: return Charge;
                default: return 0f;
            }
        }
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
        /// <summary>전장 폭 대비 유닛 크기(단계별). 있으면 SizeByTier 대신 쓴다.</summary>
        public float[] SizeByTierRatio = { 0.16f, 0.19f, 0.23f };
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
