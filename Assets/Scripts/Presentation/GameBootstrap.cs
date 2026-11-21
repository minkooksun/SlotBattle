using SlotBattle.Core.Data;
using SlotBattle.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlotBattle.Presentation
{
    /// <summary>
    /// 유일한 씬(Bootstrap)에 놓이는 시작점. 데이터를 읽고 카메라·캔버스·화면 요소를 모두 코드로 만든다.
    /// JSON 데이터는 에디터 설정 스크립트가 이 컴포넌트에 연결해 둔다.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public TextAsset UnitsJson;
        public TextAsset SlotJson;
        public TextAsset BattleJson;
        public TextAsset StagesJson;
        public TextAsset FeelJson;

        public BattleController Controller { get; private set; }

        private void Awake()
        {
            // 화면은 초당 60장 고정, 세로 고정(설계서 4장).
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var data = GameData.FromJson(UnitsJson.text, SlotJson.text, BattleJson.text, StagesJson.text);
            var feel = FeelJson != null ? FeelConfig.FromJson(FeelJson.text) : new FeelConfig();
            Controller = Build(gameObject, data, feel, HapticServiceFactory.Create());
        }

        /// <summary>카메라, 캔버스, 이벤트 시스템, 전투 화면을 만든다.</summary>
        public static BattleController Build(GameObject host, GameData data, FeelConfig feel, IHapticService haptics)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(host.transform, false);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // 전장 비율과 맞지 않아 남는 곳은 여백 색으로 칠한다.
            camera.backgroundColor = Ui.Hex(feel.Layout.MarginColor);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.layer = 5;
            canvasGo.transform.SetParent(host.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            // 1 캔버스 단위 = 1pt. 배율은 기기의 @2x/@3x에 맞춘다.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                eventSystem.transform.SetParent(host.transform, false);
            }

            var controller = host.AddComponent<BattleController>();
            controller.Initialize(data, feel, haptics, canvas, scaler, camera);
            return controller;
        }
    }
}
