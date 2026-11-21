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
        /// <summary>홈 화면에 보이는 앱 이름(설계서 1장).</summary>
        public const string DisplayName = "트라이봇";

        private static readonly Regex LinkIntoBuildState = new Regex(
            "ln -sF \\\\\"\\$PROJECT_DIR/(?<name>[A-Za-z0-9_]+)\\\\\" \\\\\"(?<dir>\\$CONFIGURATION_TEMP_DIR/artifacts/[^\\\\\"]+/buildstate)\\\\\"");

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }
            SetDisplayName(Path.Combine(pathToBuiltProject, "Info.plist"));
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

        /// <summary>Info.plist의 CFBundleDisplayName을 한글 앱 이름으로 바꾼다.</summary>
        private static void SetDisplayName(string plistPath)
        {
            if (!File.Exists(plistPath))
            {
                return;
            }
            string text = File.ReadAllText(plistPath);
            var key = new Regex("(<key>CFBundleDisplayName</key>\\s*<string>)[^<]*(</string>)");
            string fixedText;
            if (key.IsMatch(text))
            {
                fixedText = key.Replace(text, "${1}" + DisplayName + "${2}");
            }
            else
            {
                int at = text.IndexOf("<dict>", System.StringComparison.Ordinal) + "<dict>".Length;
                fixedText = text.Insert(at, "\n\t<key>CFBundleDisplayName</key>\n\t<string>" + DisplayName + "</string>");
            }
            File.WriteAllText(plistPath, fixedText);
            Debug.Log("[SlotBattle] 앱 표시 이름: " + DisplayName);
        }

        /// <summary>테스트용: 고친 결과 문자열.</summary>
        public static string Fix(string text)
        {
            return LinkIntoBuildState.Replace(text, m =>
                "ln -sfn \\\"$PROJECT_DIR/" + m.Groups["name"].Value + "\\\" \\\"" + m.Groups["dir"].Value + "/" + m.Groups["name"].Value + "\\\"");
        }
    }
}
