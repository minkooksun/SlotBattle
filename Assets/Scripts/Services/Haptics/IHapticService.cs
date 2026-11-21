namespace SlotBattle.Services
{
    public enum HapticStrength
    {
        Light,
        Medium,
        Heavy,
    }

    /// <summary>
    /// 진동. 게임 코드는 이 인터페이스로만 진동을 낸다.
    /// 진동 장치가 없는 기기(iPad)에서는 아무 일도 하지 않는다.
    /// </summary>
    public interface IHapticService
    {
        /// <summary>이 기기가 진동을 낼 수 있는가.</summary>
        bool IsSupported { get; }

        /// <summary>설정에서 끌 수 있다.</summary>
        bool Enabled { get; set; }

        void Play(HapticStrength strength);
    }

    /// <summary>진동이 없는 환경(에디터, 진동 장치 없는 기기)용.</summary>
    public sealed class NullHapticService : IHapticService
    {
        public bool IsSupported => false;
        public bool Enabled { get; set; } = true;
        public int PlayCount { get; private set; }
        public HapticStrength LastStrength { get; private set; }

        public void Play(HapticStrength strength)
        {
            // 아무 일도 하지 않는다. 테스트에서 호출 여부만 볼 수 있게 기록한다.
            PlayCount++;
            LastStrength = strength;
        }
    }

    public static class HapticServiceFactory
    {
        public static IHapticService Create()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new IOSHapticService();
#else
            return new NullHapticService();
#endif
        }
    }
}
