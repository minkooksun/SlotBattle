using System.Collections.Generic;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;
using UnityEngine;

namespace SlotBattle.Presentation
{
    /// <summary>
    /// 로봇 얼굴 아이콘(슬롯 릴, 대기 칸, 끌기 그림): 3D 로봇을 화면 밖 무대에 세워 비스듬한 앞모습을 한 번 찍는다.
    /// 로봇 모델을 구매한 에셋으로 바꾸면 아이콘도 함께 바뀐다.
    /// </summary>
    public sealed class RobotIcons
    {
        private const int Pixels = 192;

        private readonly World3D _world;
        private readonly GameData _data;
        private readonly WorldFeel _feel;
        private readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private Transform _stage;
        private Camera _camera;

        public RobotIcons(World3D world, GameData data, WorldFeel feel)
        {
            _world = world;
            _data = data;
            _feel = feel;
        }

        public Sprite Get(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return null;
            }
            if (_cache.TryGetValue(unitId, out var cached))
            {
                return cached;
            }
            UnitDef def;
            try
            {
                def = _data.GetUnit(unitId);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
            var sprite = Render(def);
            _cache[unitId] = sprite;
            return sprite;
        }

        private void EnsureStage()
        {
            if (_stage != null)
            {
                return;
            }
            _stage = new GameObject("IconStage").transform;
            _stage.gameObject.layer = World3D.Layer;
            _stage.SetParent(_world.Root, false);
            // 전장 카메라에 보이지 않는 먼 곳.
            _stage.localPosition = new Vector3(-300f, 0f, 0f);
            var cameraGo = new GameObject("IconCamera");
            cameraGo.layer = World3D.Layer;
            cameraGo.transform.SetParent(_stage, false);
            _camera = cameraGo.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            // 투명 배경. 알파가 지원되지 않아도 조작 영역 색과 비슷하게 보이게 한다.
            _camera.backgroundColor = new Color(0.12f, 0.15f, 0.26f, 0f);
            _camera.cullingMask = 1 << World3D.Layer;
            _camera.fieldOfView = 28f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 50f;
        }

        private Sprite Render(UnitDef def)
        {
            EnsureStage();
            var model = RobotFactory.Create(def, Side.Player, 1, _stage, 2f, _feel.RobotFootprint, 0f);
            model.Place(Vector3.zero, true, Vector2.one);
            model.Animate(0f, 0f, false, 0f, 0, 0f, 0f);

            var renderers = model.Root.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(model.Root.position, Vector3.one * 0.5f);
            foreach (var r in renderers)
            {
                if (r.name != "TeamRing")
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            // 비스듬한 앞모습: 얼굴(위쪽)에 조금 더 가깝게.
            var focus = bounds.center + Vector3.up * bounds.extents.y * 0.3f;
            float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
            var direction = _stage.TransformDirection(new Vector3(0.55f, 0.5f, 1f).normalized);
            _camera.transform.position = focus + direction * radius * 2.7f;
            _camera.transform.LookAt(focus);

            var texture = RenderTexture.GetTemporary(Pixels, Pixels, 24, RenderTextureFormat.ARGB32);
            texture.antiAliasing = 4;
            _camera.targetTexture = texture;
            _camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false) { name = "Icon_" + def.Id };
            image.ReadPixels(new Rect(0, 0, Pixels, Pixels), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            _camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(texture);
            model.Destroy();
            _textures.Add(image);
            return Sprite.Create(image, new Rect(0, 0, Pixels, Pixels), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
