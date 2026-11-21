using System.Collections.Generic;
using UnityEngine;

namespace SlotBattle.Presentation
{
    /// <summary>
    /// 3D 전장(설계서 4장 "3D 전장"): 바닥, 라인, 성, 성 포탑, 카메라, 조명.
    /// 카메라는 렌더 텍스처에 그리고, 그 그림은 캔버스의 전장 자리에 놓인다.
    /// 좌표: 이 전장의 로컬 공간에서 x = 전장 폭 방향(0 ~ fieldWidth), z = 내 성에서의 거리(Core의 Y), y = 높이.
    /// </summary>
    public sealed class World3D
    {
        /// <summary>3D 물체만 담는 레이어. UI 카메라는 이 레이어를 그리지 않는다.</summary>
        public const int Layer = 30;

        private readonly WorldFeel _feel;
        private readonly Transform _root;
        private readonly Camera _camera;
        private readonly Light _light;
        private readonly Transform _ground;
        private readonly List<Renderer> _lanes = new List<Renderer>();
        private readonly List<Renderer> _dropZones = new List<Renderer>();
        private readonly Transform _rangeBand;
        private Renderer _midline;
        private Renderer _enemyCastle;
        private Renderer _playerCastle;
        private RenderTexture _texture;
        private Vector2 _sizePt;
        private float _laneLength = 20f;
        private float _playerZone = 10f;
        private int _laneCount = 2;

        public World3D(Transform host, WorldFeel feel)
        {
            _feel = feel;
            var rootGo = new GameObject("World3D");
            rootGo.transform.SetParent(host, false);
            // UI(원점 근처)와 겹치지 않게 멀리 둔다.
            rootGo.transform.localPosition = new Vector3(1000f, 0f, 0f);
            rootGo.layer = Layer;
            _root = rootGo.transform;
            rootGo.AddComponent<TextureOwner>().World = this;

            var cameraGo = new GameObject("WorldCamera");
            cameraGo.layer = Layer;
            cameraGo.transform.SetParent(_root, false);
            _camera = cameraGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Ui.Hex(feel.Background);
            _camera.cullingMask = 1 << Layer;
            _camera.fieldOfView = feel.Fov;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 200f;
            _camera.enabled = false;

            var lightGo = new GameObject("Sun");
            lightGo.layer = Layer;
            lightGo.transform.SetParent(_root, false);
            lightGo.transform.localRotation = Quaternion.Euler(feel.LightPitch, feel.LightYaw, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.intensity = feel.LightIntensity;
            _light.color = Color.white;
            _light.shadows = feel.Shadows ? LightShadows.Soft : LightShadows.None;
            _light.cullingMask = 1 << Layer;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Ui.Hex(feel.Ambient);

            _ground = Mat3D.Box(_root, "Ground", Vector3.zero, Vector3.one, Ui.Hex(feel.Ground)).transform;
            for (int i = 0; i < 2; i++)
            {
                _lanes.Add(Mat3D.Box(_root, "Lane" + i, Vector3.zero, Vector3.one, Ui.Hex(feel.Lane)).GetComponent<Renderer>());
                var zone = Mat3D.Box(_root, "DropZone" + i, Vector3.zero, Vector3.one, Ui.Hex(feel.DropZone)).GetComponent<Renderer>();
                zone.gameObject.SetActive(false);
                _dropZones.Add(zone);
            }
            _midline = Mat3D.Box(_root, "Midline", Vector3.zero, Vector3.one, Ui.Hex(feel.LaneEdge)).GetComponent<Renderer>();
            _rangeBand = Mat3D.Box(_root, "RangeBand", Vector3.zero, Vector3.one, Ui.Hex(feel.RangeBand)).transform;
            _rangeBand.gameObject.SetActive(false);
            _enemyCastle = Mat3D.Box(_root, "EnemyCore", Vector3.zero, Vector3.one, Palette.Enemy).GetComponent<Renderer>();
            _playerCastle = Mat3D.Box(_root, "PlayerCore", Vector3.zero, Vector3.one, Palette.Player).GetComponent<Renderer>();
            for (int i = 0; i < 2; i++)
            {
                EnemyTurrets[i] = new Turret3D(_root, Palette.Enemy, 180f);
                PlayerTurrets[i] = new Turret3D(_root, Palette.Player, 0f);
            }
            Rebuild();
        }

        public Transform Root => _root;
        public Camera Camera => _camera;
        public RenderTexture Texture => _texture;
        public float FieldWidth => _feel.FieldWidth;
        public float LaneLength => _laneLength;
        public float LaneWidth => _feel.FieldWidth / _laneCount;
        public Turret3D[] EnemyTurrets { get; } = new Turret3D[2];
        public Turret3D[] PlayerTurrets { get; } = new Turret3D[2];
        public bool HasView => _texture != null && _sizePt.x > 1f && _sizePt.y > 1f;

        public void Configure(float laneLength, float playerZone, int laneCount)
        {
            _laneLength = laneLength;
            _playerZone = playerZone;
            _laneCount = Mathf.Clamp(laneCount, 1, 2);
            Rebuild();
        }

        private void Rebuild()
        {
            float w = _feel.FieldWidth;
            float depth = _feel.CastleDepth;
            Place(_ground, new Vector3(w / 2f, -0.06f, _laneLength / 2f), new Vector3(w + 6f, 0.1f, _laneLength + depth * 2f + 8f));
            for (int i = 0; i < 2; i++)
            {
                bool active = i < _laneCount;
                _lanes[i].gameObject.SetActive(active);
                float cx = LaneCenterX(i);
                Place(_lanes[i].transform, new Vector3(cx, -0.01f, _laneLength / 2f), new Vector3(LaneWidth - 0.4f, 0.04f, _laneLength));
                Place(_dropZones[i].transform, new Vector3(cx, 0.015f, _playerZone / 2f), new Vector3(LaneWidth - 0.4f, 0.02f, _playerZone));
            }
            Place(_midline.transform, new Vector3(w / 2f, 0.02f, _playerZone), new Vector3(w - 0.2f, 0.03f, 0.08f));
            float h = _feel.CastleHeight;
            Place(_enemyCastle.transform, new Vector3(w / 2f, h / 2f, _laneLength + depth / 2f), new Vector3(w, h, depth));
            Place(_playerCastle.transform, new Vector3(w / 2f, h / 2f, -depth / 2f), new Vector3(w, h, depth));
            for (int i = 0; i < 2; i++)
            {
                bool active = i < _laneCount;
                EnemyTurrets[i].SetVisible(active);
                PlayerTurrets[i].SetVisible(active);
                // 포탑은 성 위, 전장 쪽 가장자리. 성 포탄은 Core의 성 위치(거리 0 또는 laneLength)에서 나간다.
                EnemyTurrets[i].Place(new Vector3(LaneCenterX(i), h, _laneLength + 0.4f), 1.1f);
                PlayerTurrets[i].Place(new Vector3(LaneCenterX(i), h, -0.4f), 1.1f);
            }
            PlaceCamera(Vector2.zero);
        }

        private static void Place(Transform t, Vector3 position, Vector3 scale)
        {
            t.localPosition = position;
            t.localScale = scale;
        }

        /// <summary>전장 그림의 크기(pt)와 화면 배율에 맞춰 렌더 텍스처를 만든다.</summary>
        public void Resize(Vector2 sizePt, float pointScale)
        {
            _sizePt = sizePt;
            if (sizePt.x < 1f || sizePt.y < 1f)
            {
                return;
            }
            float scale = Mathf.Max(1f, pointScale);
            float longest = Mathf.Max(sizePt.x, sizePt.y) * scale;
            if (longest > _feel.MaxTexture)
            {
                scale *= _feel.MaxTexture / longest;
            }
            int w = Mathf.Max(16, Mathf.RoundToInt(sizePt.x * scale));
            int h = Mathf.Max(16, Mathf.RoundToInt(sizePt.y * scale));
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                ReleaseTexture();
                _texture = new RenderTexture(w, h, 24) { name = "WorldView", antiAliasing = Mathf.Max(1, _feel.Msaa) };
                _texture.Create();
                _camera.targetTexture = _texture;
            }
            _camera.aspect = (float)w / h;
            _camera.enabled = true;
        }

        public void ReleaseTexture()
        {
            if (_texture == null)
            {
                return;
            }
            if (_camera != null)
            {
                _camera.targetTexture = null;
            }
            _texture.Release();
            Object.Destroy(_texture);
            _texture = null;
        }

        /// <summary>카메라: 내 성 뒤 위쪽에서 적 성 쪽을 내려다본다. shake는 화면 흔들림(전장 단위).</summary>
        public void PlaceCamera(Vector2 shake)
        {
            float cx = _feel.FieldWidth / 2f;
            var position = new Vector3(cx + shake.x, _feel.CameraHeight + shake.y, -_feel.CameraBack);
            var target = new Vector3(cx + shake.x, 0f, _feel.LookAtZ);
            _camera.transform.localPosition = position;
            _camera.transform.localRotation = Quaternion.LookRotation(target - position, Vector3.up);
        }

        // ───────────── 좌표 ─────────────

        public float LaneCenterX(int lane)
        {
            return LaneWidth * (lane + 0.5f);
        }

        /// <summary>라인과 거리(Core 좌표)를 바닥 위 점(전장 로컬)으로.</summary>
        public Vector3 Ground(int lane, float laneY, float height = 0f)
        {
            return new Vector3(LaneCenterX(lane), height, laneY);
        }

        /// <summary>전장 로컬 점을 전장 그림 안의 pt 좌표(왼쪽 아래 원점)로.</summary>
        public Vector2 ToLocal(Vector3 local)
        {
            if (!HasView)
            {
                return Vector2.zero;
            }
            var viewport = _camera.WorldToViewportPoint(_root.TransformPoint(local));
            return new Vector2(viewport.x * _sizePt.x, viewport.y * _sizePt.y);
        }

        /// <summary>그 점에서 전장 거리 1이 몇 pt로 보이는가(가로 방향).</summary>
        public float PointsPerUnit(Vector3 local)
        {
            return (ToLocal(local + Vector3.right * 0.5f) - ToLocal(local - Vector3.right * 0.5f)).magnitude;
        }

        /// <summary>바닥 위 원(반지름 radius)이 그림에서 보이는 타원의 지름(pt).</summary>
        public Vector2 ProjectedCircle(Vector3 center, float radius)
        {
            float w = (ToLocal(center + Vector3.right * radius) - ToLocal(center - Vector3.right * radius)).magnitude;
            float h = (ToLocal(center + Vector3.forward * radius) - ToLocal(center - Vector3.forward * radius)).magnitude;
            return new Vector2(w, h);
        }

        /// <summary>그림 안의 pt 좌표를 바닥에 쏜 광선으로 라인과 거리로 되돌린다. 바닥을 벗어나면 false.</summary>
        public bool TryFromLocal(Vector2 local, out int lane, out float laneY)
        {
            lane = 0;
            laneY = 0f;
            if (!HasView)
            {
                return false;
            }
            var ray = _camera.ViewportPointToRay(new Vector3(local.x / _sizePt.x, local.y / _sizePt.y, 0f));
            float planeY = _root.position.y;
            if (Mathf.Abs(ray.direction.y) < 1e-4f)
            {
                return false;
            }
            float t = (planeY - ray.origin.y) / ray.direction.y;
            if (t < 0f)
            {
                return false;
            }
            var hit = _root.InverseTransformPoint(ray.origin + ray.direction * t);
            lane = Mathf.Clamp(Mathf.FloorToInt(hit.x / LaneWidth), 0, _laneCount - 1);
            laneY = hit.z;
            bool inside = local.x >= 0f && local.x <= _sizePt.x && local.y >= 0f && local.y <= _sizePt.y;
            return inside && hit.x >= -0.5f && hit.x <= _feel.FieldWidth + 0.5f;
        }

        // ───────────── 배치 미리보기와 성 ─────────────

        public void ShowDropZones(bool visible, int highlightLane)
        {
            for (int i = 0; i < _dropZones.Count; i++)
            {
                _dropZones[i].gameObject.SetActive(visible && i < _laneCount);
                Mat3D.Tint(_dropZones[i], Ui.Hex(_feel.DropZone), i == highlightLane ? 0.35f : 0f, Color.white);
            }
        }

        public void ShowRangeBand(bool visible, int lane, float from, float to)
        {
            _rangeBand.gameObject.SetActive(visible);
            if (visible)
            {
                Place(_rangeBand, new Vector3(LaneCenterX(lane), 0.03f, (from + to) / 2f), new Vector3(LaneWidth - 1.2f, 0.02f, Mathf.Max(0.1f, to - from)));
            }
        }

        public Vector3 CastleCenter(bool enemy)
        {
            float d = _feel.CastleDepth / 2f;
            return new Vector3(_feel.FieldWidth / 2f, _feel.CastleHeight, enemy ? _laneLength + d : -d);
        }

        public void FlashCastle(bool enemy, float amount)
        {
            var renderer = enemy ? _enemyCastle : _playerCastle;
            Mat3D.Tint(renderer, enemy ? Palette.Enemy : Palette.Player, amount, Color.white);
        }

        public void SetCastleDestroyed(bool enemy, bool destroyed)
        {
            var renderer = enemy ? _enemyCastle : _playerCastle;
            renderer.transform.localScale = new Vector3(_feel.FieldWidth, _feel.CastleHeight * (destroyed ? 0.3f : 1f), _feel.CastleDepth);
            var p = renderer.transform.localPosition;
            renderer.transform.localPosition = new Vector3(p.x, _feel.CastleHeight * (destroyed ? 0.15f : 0.5f), p.z);
            Mat3D.Tint(renderer, enemy ? Palette.Enemy : Palette.Player, destroyed ? 0.6f : 0f, Color.black);
            foreach (var turret in enemy ? EnemyTurrets : PlayerTurrets)
            {
                turret.Reset(destroyed);
            }
        }

        /// <summary>호스트가 사라질 때 렌더 텍스처를 놓아준다.</summary>
        private sealed class TextureOwner : MonoBehaviour
        {
            public World3D World;

            private void OnDestroy()
            {
                World?.ReleaseTexture();
            }
        }
    }

    /// <summary>성의 포탑(라인마다 하나): 받침, 대상 쪽으로 도는 몸체와 포신. 쏠 때 뒤로 튄다.</summary>
    public sealed class Turret3D
    {
        private readonly Transform _base;
        private readonly Transform _head;
        private readonly Transform _barrel;
        private readonly Renderer _barrelRenderer;
        private readonly Color _color;
        private readonly float _restYaw;
        private float _yaw;
        private float _targetYaw;
        private float _glowT = -1f;
        private float _glowDuration;
        private float _recoil;
        private bool _destroyed;
        private float _size = 1f;

        public Turret3D(Transform parent, Color color, float restYaw)
        {
            _color = color;
            _restYaw = restYaw;
            _base = Mat3D.Cylinder(parent, "TurretBase", Vector3.zero, Vector3.one, Color.Lerp(color, Color.black, 0.3f)).transform;
            _head = new GameObject("TurretHead").transform;
            _head.gameObject.layer = World3D.Layer;
            _head.SetParent(parent, false);
            Mat3D.Box(_head, "TurretBody", new Vector3(0f, 0f, 0f), new Vector3(0.7f, 0.45f, 0.7f), Color.Lerp(color, Color.white, 0.2f));
            var barrel = Mat3D.Box(_head, "TurretBarrel", new Vector3(0f, 0.05f, 0.55f), new Vector3(0.22f, 0.22f, 0.8f), Ui.Hex("#C8D2E4"));
            _barrel = barrel.transform;
            _barrelRenderer = barrel.GetComponent<Renderer>();
            Reset(false);
        }

        public Vector3 Muzzle => _head.localPosition + _head.localRotation * (new Vector3(0f, 0.05f, 0.95f) * _size);

        public void Place(Vector3 top, float size)
        {
            _size = size;
            _base.localPosition = top + new Vector3(0f, 0.15f * size, 0f);
            _base.localScale = new Vector3(0.9f, 0.15f, 0.9f) * size;
            _head.localPosition = top + new Vector3(0f, 0.45f * size, 0f);
            _head.localScale = Vector3.one * size;
            Apply();
        }

        public void SetVisible(bool visible)
        {
            _base.gameObject.SetActive(visible);
            _head.gameObject.SetActive(visible);
        }

        /// <summary>준비 동작: 대상 쪽으로 돌며 포신이 빛난다.</summary>
        public void Aim(Vector3 target, float windup)
        {
            if (_destroyed)
            {
                return;
            }
            var d = target - _head.localPosition;
            _targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            _glowDuration = Mathf.Max(0.01f, windup);
            _glowT = 0f;
        }

        public void Fire(float recoil)
        {
            if (_destroyed)
            {
                return;
            }
            _recoil = recoil;
            _glowT = -1f;
        }

        public void Reset(bool destroyed)
        {
            _destroyed = destroyed;
            _yaw = _restYaw;
            _targetYaw = _restYaw;
            _glowT = -1f;
            _recoil = 0f;
            _head.gameObject.SetActive(!destroyed);
            Apply();
        }

        public void Render(float dt, float turnSpeed)
        {
            if (_destroyed)
            {
                return;
            }
            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, 1f - Mathf.Exp(-turnSpeed * dt));
            _recoil *= Mathf.Exp(-20f * dt);
            float glow = 0f;
            if (_glowT >= 0f)
            {
                _glowT += dt;
                glow = Mathf.Clamp01(_glowT / _glowDuration);
            }
            Mat3D.Tint(_barrelRenderer, Ui.Hex("#C8D2E4"), glow, Color.Lerp(_color, Color.white, 0.6f));
            Apply();
        }

        private void Apply()
        {
            _head.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            // 반동: pt 단위 값을 포신 길이에 견준 비율로 쓴다.
            _barrel.localPosition = new Vector3(0f, 0.05f, 0.55f - _recoil * 0.05f);
        }
    }

    /// <summary>3D 기본 도형과 재질. 같은 색의 재질은 하나를 돌려 쓰고, 번쩍임은 물체마다 속성 블록으로 바꾼다.</summary>
    public static class Mat3D
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly Dictionary<long, Material> Cache = new Dictionary<long, Material>();
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static Shader _shader;

        private static Shader LitShader
        {
            get
            {
                if (_shader == null)
                {
                    _shader = Shader.Find("Universal Render Pipeline/Simple Lit")
                        ?? Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard");
                }
                return _shader;
            }
        }

        public static Material Get(Color color, bool emissive = false)
        {
            var c32 = (Color32)color;
            long key = ((long)c32.r << 24) | ((long)c32.g << 16) | ((long)c32.b << 8) | c32.a | (emissive ? 1L << 40 : 0L);
            if (Cache.TryGetValue(key, out var material) && material != null)
            {
                return material;
            }
            material = new Material(LitShader) { name = "M_" + ColorUtility.ToHtmlStringRGB(color) + (emissive ? "_E" : "") };
            material.SetColor(BaseColorId, color);
            material.SetColor(ColorId, color);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionId, color * 1.6f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            Cache[key] = material;
            return material;
        }

        public static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Color color, bool emissive = false)
        {
            return Make(PrimitiveType.Cube, parent, name, position, scale, color, emissive);
        }

        public static GameObject Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Color color, bool emissive = false)
        {
            return Make(PrimitiveType.Cylinder, parent, name, position, scale, color, emissive);
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 position, Vector3 scale, Color color, bool emissive = false)
        {
            return Make(PrimitiveType.Sphere, parent, name, position, scale, color, emissive);
        }

        private static GameObject Make(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Color color, bool emissive)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = World3D.Layer;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = Get(color, emissive);
            return go;
        }

        /// <summary>물체 하나의 색을 base에서 target 쪽으로 amount만큼 바꾼다(0이면 원래 색).</summary>
        public static void Tint(Renderer renderer, Color baseColor, float amount, Color target)
        {
            if (renderer == null)
            {
                return;
            }
            if (amount <= 0.001f)
            {
                renderer.SetPropertyBlock(null);
                return;
            }
            var c = Color.Lerp(baseColor, target, Mathf.Clamp01(amount));
            Block.Clear();
            Block.SetColor(BaseColorId, c);
            Block.SetColor(ColorId, c);
            renderer.SetPropertyBlock(Block);
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
