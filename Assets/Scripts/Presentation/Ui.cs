using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlotBattle.Presentation
{
    public enum Shape
    {
        Square,
        Circle,
        RoundedSquare,
        Triangle,
        Diamond,
        Hexagon,
        Ring,
        Glow,
        Panel,
        PanelOutline,
    }

    /// <summary>
    /// 화면 요소를 코드로 만드는 도우미. 1차 개발 중의 그림은 코드로 그린 색깔 도형이다(설계서 4장).
    /// 모든 위치는 부모의 왼쪽 아래를 원점으로 하는 pt 좌표다.
    /// </summary>
    public static class Ui
    {
        private const int TextureSize = 128;
        private static readonly Dictionary<Shape, Sprite> Sprites = new Dictionary<Shape, Sprite>();
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    // 한글이 나오는 시스템 글꼴을 쓴다(iOS, macOS). 없으면 Unity 기본 글꼴.
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "AppleSDGothicNeo-Bold", "Apple SD Gothic Neo", "Helvetica Neue", "Arial" }, 32);
                    if (_font == null)
                    {
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                }
                return _font;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            return rt;
        }

        /// <summary>부모의 왼쪽 아래 기준으로 사각형에 맞춘다.</summary>
        public static void Place(RectTransform rt, Rect rect)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = rect.position;
            rt.sizeDelta = rect.size;
        }

        /// <summary>중심점 기준으로 놓는다(크기 애니메이션이 가운데에서 일어나게).</summary>
        public static void PlaceCentered(RectTransform rt, Vector2 center, Vector2 size)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
        }

        /// <summary>부모를 꽉 채운다.</summary>
        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        public static Image CreateImage(string name, Transform parent, Shape shape, Color color, bool raycast = false)
        {
            var rt = CreateRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            SetShape(image, shape);
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static void SetShape(Image image, Shape shape)
        {
            image.sprite = GetSprite(shape);
            if (shape == Shape.Panel || shape == Shape.PanelOutline)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 2.5f;
            }
            else
            {
                image.type = Image.Type.Simple;
            }
        }

        public static Text CreateText(string name, Transform parent, string value, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rt = CreateRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Color Hex(string hex, float alpha = 1f)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var color))
            {
                color.a = alpha;
                return color;
            }
            return new Color(1f, 0f, 1f, alpha);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        // ───────────── 도형 그림 ─────────────

        public static Sprite GetSprite(Shape shape)
        {
            if (Sprites.TryGetValue(shape, out var sprite) && sprite != null)
            {
                return sprite;
            }
            sprite = BuildSprite(shape);
            Sprites[shape] = sprite;
            return sprite;
        }

        private static Sprite BuildSprite(Shape shape)
        {
            int n = TextureSize;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "Shape_" + shape,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[n * n];
            Func<Vector2, float> sdf = Distance(shape);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                    float alpha;
                    if (shape == Shape.Glow)
                    {
                        float t = Mathf.Clamp01(1f - p.magnitude);
                        alpha = t * t;
                    }
                    else
                    {
                        float d = sdf(p);
                        alpha = Mathf.Clamp01(0.5f - d * n * 0.5f);
                    }
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var border = shape == Shape.Panel || shape == Shape.PanelOutline ? new Vector4(40, 40, 40, 40) : Vector4.zero;
            return Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>[-1,1] 좌표에서의 부호 있는 거리(안쪽이 음수).</summary>
        private static Func<Vector2, float> Distance(Shape shape)
        {
            switch (shape)
            {
                case Shape.Circle:
                    return p => p.magnitude - 0.94f;
                case Shape.Ring:
                    return p => Mathf.Abs(p.magnitude - 0.84f) - 0.1f;
                case Shape.RoundedSquare:
                    return p => RoundedBox(p, 0.94f, 0.3f);
                case Shape.Panel:
                    return p => RoundedBox(p, 0.98f, 0.6f);
                case Shape.PanelOutline:
                    return p => Mathf.Abs(RoundedBox(p, 0.94f, 0.56f)) - 0.05f;
                case Shape.Triangle:
                    return p => Polygon(p, new[] { new Vector2(0f, 0.95f), new Vector2(-0.9f, -0.8f), new Vector2(0.9f, -0.8f) });
                case Shape.Diamond:
                    return p => Polygon(p, new[] { new Vector2(0f, 0.97f), new Vector2(-0.75f, 0f), new Vector2(0f, -0.97f), new Vector2(0.75f, 0f) });
                case Shape.Hexagon:
                    return p => Polygon(p, Regular(6, 0.95f, 0f));
                default:
                    return p => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) - 0.98f;
            }
        }

        private static float RoundedBox(Vector2 p, float half, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x) - half + radius, Mathf.Abs(p.y) - half + radius);
            var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        private static Vector2[] Regular(int sides, float radius, float rotation)
        {
            var points = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = rotation + Mathf.PI * 2f * i / sides + Mathf.PI / 2f;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        /// <summary>볼록 다각형(반시계 또는 시계 방향)의 근사 거리.</summary>
        private static float Polygon(Vector2 p, Vector2[] vertices)
        {
            float area = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Length];
                area += a.x * b.y - b.x * a.y;
            }
            float sign = area > 0f ? 1f : -1f;
            float d = float.MinValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Length];
                var edge = (b - a).normalized;
                var normal = new Vector2(edge.y, -edge.x) * sign;
                d = Mathf.Max(d, Vector2.Dot(p - a, normal));
            }
            return d;
        }
    }

    /// <summary>색 모음.</summary>
    public static class Palette
    {
        public static readonly Color Background = Ui.Hex("#141B2D");
        public static readonly Color Lane = Ui.Hex("#1B2440");
        public static readonly Color LaneLine = Ui.Hex("#2C3A63");
        public static readonly Color Panel = Ui.Hex("#1E2742");
        public static readonly Color PanelLight = Ui.Hex("#2B3760");
        public static readonly Color Text = Ui.Hex("#EAF0FF");
        public static readonly Color TextDim = Ui.Hex("#8A97BF");
        public static readonly Color Player = Ui.Hex("#3D8BFF");
        public static readonly Color Enemy = Ui.Hex("#FF4767");
        public static readonly Color Energy = Ui.Hex("#FFD54A");
        public static readonly Color EnergyEmpty = Ui.Hex("#2A3354");
        public static readonly Color Deny = Ui.Hex("#FF3B3B");
        public static readonly Color Accent = Ui.Hex("#43E0A0");
        public static readonly Color Lock = Ui.Hex("#FFC93C");
        public static readonly Color Trail = Color.white;
    }
}
