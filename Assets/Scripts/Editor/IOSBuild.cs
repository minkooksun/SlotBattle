using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SlotBattle.EditorTools
{
    /// <summary>
    /// 명령줄에서 Xcode 프로젝트를 만든다(설계서 5장 M2).
    /// Tools/build-ios.sh가 이 메서드를 부른다:
    ///   Unity -batchmode -quit -projectPath . -buildTarget iOS -executeMethod SlotBattle.EditorTools.IOSBuild.Build
    /// 결과: Builds/iOS/Unity-iPhone.xcodeproj
    /// </summary>
    public static class IOSBuild
    {
        public const string DefaultOutput = "Builds/iOS";

        [MenuItem("SlotBattle/iOS Xcode 프로젝트 만들기")]
        public static void Build()
        {
            ProjectSetup.Run();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            }
            string output = Arg("-buildPath") ?? DefaultOutput;
            Directory.CreateDirectory(output);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = output,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = Application.isBatchMode ? BuildOptions.None : BuildOptions.ShowBuiltPlayer,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[SlotBattle] iOS 빌드 실패: " + summary.result + ", 오류 " + summary.totalErrors + "개");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }
            Debug.Log("[SlotBattle] iOS Xcode 프로젝트 생성: " + Path.GetFullPath(output) + " (" + summary.totalTime.TotalSeconds.ToString("0") + "초)");
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
