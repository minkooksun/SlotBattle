#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace SlotBattle.Services
{
    /// <summary>
    /// iOS 진동: Assets/Plugins/iOS/SlotBattleHaptics.mm의 UIImpactFeedbackGenerator를 부른다.
    /// iPad에서는 네이티브 쪽이 지원하지 않음을 알려 아무 일도 하지 않는다.
    /// </summary>
    public sealed class IOSHapticService : IHapticService
    {
        [DllImport("__Internal")]
        private static extern bool SlotBattle_HapticsSupported();

        [DllImport("__Internal")]
        private static extern void SlotBattle_Haptic(int strength);

        public IOSHapticService()
        {
            IsSupported = SlotBattle_HapticsSupported();
        }

        public bool IsSupported { get; }
        public bool Enabled { get; set; } = true;

        public void Play(HapticStrength strength)
        {
            if (!Enabled || !IsSupported)
            {
                return;
            }
            SlotBattle_Haptic((int)strength);
        }
    }
}
#endif
