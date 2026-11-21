// SlotBattle 진동(iOS). IOSHapticService가 DllImport("__Internal")로 부른다.
// iPad에는 진동 장치가 없으므로 지원하지 않음으로 알리고 아무 일도 하지 않는다.
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator* SB_generators[3];

static bool SB_IsPad(void)
{
    return [UIDevice currentDevice].userInterfaceIdiom == UIUserInterfaceIdiomPad;
}

static UIImpactFeedbackGenerator* SB_Generator(int strength)
{
    if (strength < 0) strength = 0;
    if (strength > 2) strength = 2;
    if (SB_generators[strength] == nil)
    {
        UIImpactFeedbackStyle style = strength == 0 ? UIImpactFeedbackStyleLight
            : (strength == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy);
        SB_generators[strength] = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
        [SB_generators[strength] prepare];
    }
    return SB_generators[strength];
}

extern "C" {

bool SlotBattle_HapticsSupported(void)
{
    return !SB_IsPad();
}

void SlotBattle_Haptic(int strength)
{
    if (SB_IsPad())
    {
        return;
    }
    UIImpactFeedbackGenerator* generator = SB_Generator(strength);
    [generator impactOccurred];
    [generator prepare];
}

/// 화면의 포인트당 픽셀 배율(@2x, @3x). 44pt 같은 크기를 픽셀로 바꿀 때 쓴다.
float SlotBattle_ScreenScale(void)
{
    return (float)[UIScreen mainScreen].nativeScale;
}

}
