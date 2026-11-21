using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace SlotBattle.EditorTools
{
    /// <summary>
    /// Unity가 만든 Xcode 프로젝트의 문제를 빌드 직후 고친다.
    ///
    /// Unity 6000.0.1의 IL2CPP 빌드 단계는 `ln -sF 원본 기존폴더`로 링크를 만든다.
    /// macOS 26의 ln은 이때 기존 폴더를 지우고 그 자리를 링크로 바꿔 버려서,
    /// Xcode 빌드가 "il2cpp: No such file or directory"로 실패한다.
    /// 링크 이름을 명시한 `ln -sfn 원본 폴더/이름`으로 바꾼다.
    /// (Unity를 최신 6000.0 LTS로 올리면 필요 없어질 수 있다.)
    /// </summary>
    public static class XcodeProjectFixes
    {
        private static readonly Regex LinkIntoBuildState = new Regex(
            "ln -sF \\\\\"\\$PROJECT_DIR/(?<name>[A-Za-z0-9_]+)\\\\\" \\\\\"(?<dir>\\$CONFIGURATION_TEMP_DIR/artifacts/[^\\\\\"]+/buildstate)\\\\\"");

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }
            string pbxproj = Path.Combine(pathToBuiltProject, "Unity-iPhone.xcodeproj", "project.pbxproj");
            if (!File.Exists(pbxproj))
            {
                return;
            }
            string text = File.ReadAllText(pbxproj);
            int count = 0;
            string fixedText = LinkIntoBuildState.Replace(text, m =>
            {
                count++;
                string name = m.Groups["name"].Value;
                string dir = m.Groups["dir"].Value;
                return "ln -sfn \\\"$PROJECT_DIR/" + name + "\\\" \\\"" + dir + "/" + name + "\\\"";
            });
            if (count > 0)
            {
                File.WriteAllText(pbxproj, fixedText);
                Debug.Log("[SlotBattle] Xcode 빌드 단계의 링크 명령 " + count + "개를 고쳤습니다(macOS ln -sF 문제).");
            }
        }

        /// <summary>테스트용: 고친 결과 문자열.</summary>
        public static string Fix(string text)
        {
            return LinkIntoBuildState.Replace(text, m =>
                "ln -sfn \\\"$PROJECT_DIR/" + m.Groups["name"].Value + "\\\" \\\"" + m.Groups["dir"].Value + "/" + m.Groups["name"].Value + "\\\"");
        }
    }
}
