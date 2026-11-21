using System;
using System.IO;
using SlotBattle.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SlotBattle.EditorTools
{
    /// <summary>
    /// 프로젝트 설정을 코드로 적용한다(설계서 4장·5장 M2): iOS 빌드 설정, URP, 유일한 씬 Bootstrap.
    /// 에디터를 손으로 조작하지 않고 같은 설정을 반복해서 만든다. 여러 번 실행해도 결과가 같다.
    /// 번들 ID와 Apple 팀 ID는 환경 변수 SLOTBATTLE_BUNDLE_ID, SLOTBATTLE_TEAM_ID로 바꿀 수 있다.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        public const string DefaultBundleId = "com.minkooksun.slotbattle";
        private const string PipelinePath = "Assets/Settings/URP.asset";
        private const string RendererPath = "Assets/Settings/URP_Renderer.asset";

        [MenuItem("SlotBattle/프로젝트 설정 적용")]
        public static void Run()
        {
            ConfigurePlayer();
            ConfigureRenderPipeline();
            CreateBootstrapScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[SlotBattle] 프로젝트 설정 적용 완료");
        }

        public static string BundleId => Env("SLOTBATTLE_BUNDLE_ID", DefaultBundleId);

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "SlotBattle";
            PlayerSettings.productName = "SlotBattle";
            PlayerSettings.bundleVersion = "0.2.0";

            // 세로 고정.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.statusBarHidden = true;

            // iOS: IL2CPP, ARM64, iPhone+iPad.
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(ios, BundleId);
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(ios, 1); // 1 = ARM64
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            // iPad에서 세로만 지원하려면 전체 화면(멀티태스킹 분할 화면 제외)이어야 한다.
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            string team = Env("SLOTBATTLE_TEAM_ID", null);
            if (!string.IsNullOrEmpty(team))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = team;
            }
            if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber))
            {
                PlayerSettings.iOS.buildNumber = "1";
            }

            // 입력은 Unity 기본 입력(Input Manager)을 쓴다.
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings != null && settings.Length > 0)
            {
                var so = new SerializedObject(settings[0]);
                var input = so.FindProperty("activeInputHandler");
                if (input != null && input.intValue != 0)
                {
                    input.intValue = 0;
                    so.ApplyModifiedProperties();
                }
            }
        }

        private static void ConfigureRenderPipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                Directory.CreateDirectory("Assets/Settings");
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        private static void CreateBootstrapScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Bootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.UnitsJson = LoadText("units.json");
            bootstrap.SlotJson = LoadText("slot.json");
            bootstrap.BattleJson = LoadText("battle.json");
            bootstrap.StagesJson = LoadText("stages.json");
            bootstrap.FeelJson = LoadText("feel.json");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static TextAsset LoadText(string file)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/" + file);
            if (asset == null)
            {
                throw new InvalidOperationException("Assets/Data/" + file + "를 찾지 못했습니다.");
            }
            return asset;
        }

        private static string Env(string name, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
