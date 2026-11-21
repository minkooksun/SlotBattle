using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlotBattle.Presentation
{
    /// <summary>
    /// 짧은 연출 모음: 피해 숫자, 파편, 충격파 고리, 화면 번쩍임, 날아가는 아이콘.
    /// 모두 미리 만들어 둔 것을 돌려 써서 프레임이 끊기지 않게 한다.
    /// 시간은 화면 표시용(dt)만 쓰고 Core 진행과는 무관하다.
    /// </summary>
    public sealed class FxLayer
    {
        private const int MaxTexts = 48;
        private const int MaxDebris = 160;
        private const int MaxRings = 24;
        private const int MaxFlyers = 8;

        private readonly RectTransform _root;
        private readonly List<TextFx> _texts = new List<TextFx>();
        private readonly List<DebrisFx> _debris = new List<DebrisFx>();
        private readonly List<RingFx> _rings = new List<RingFx>();
        private readonly List<FlyerFx> _flyers = new List<FlyerFx>();
        private readonly Image _flash;
        private float _flashT;
        private float _flashDuration;
        private float _flashAlpha;
        private int _nextText;
        private int _nextDebris;
        private int _nextRing;

        public FxLayer(string name, Transform parent)
        {
            _root = Ui.CreateRect(name, parent);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.pivot = Vector2.zero;
            _root.anchoredPosition = Vector2.zero;
            _root.sizeDelta = Vector2.zero;
            _flash = Ui.CreateImage("Flash", _root, Shape.Square, new Color(1, 1, 1, 0));
            Ui.Fill(_flash.rectTransform);
            _flash.gameObject.SetActive(false);
        }

        public RectTransform Root => _root;
        public int ActiveTextCount { get; private set; }

        // ───────────── 만들기 ─────────────

        public void DamageNumber(Vector2 position, float damage, Color color, float rise, float duration, float scale = 1f)
        {
            if (_texts.Count < MaxTexts)
            {
                var text = Ui.CreateText("Damage", _root, "", 16, color);
                var outline = text.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, 0.7f);
                _texts.Add(new TextFx { Text = text });
            }
            var fx = _texts[_nextText % _texts.Count];
            _nextText++;
            fx.Text.gameObject.SetActive(true);
            fx.Text.text = Mathf.RoundToInt(damage).ToString();
            fx.Text.color = color;
            fx.Start = position + new Vector2(UnityEngine.Random.Range(-6f, 6f), 6f);
            fx.Rise = rise;
            fx.Duration = duration;
            fx.Scale = scale;
            fx.T = 0f;
            Ui.PlaceCentered(fx.Text.rectTransform, fx.Start, new Vector2(60f, 24f));
        }

        public void Text(Vector2 position, string value, Color color, int size, float rise, float duration)
        {
            DamageNumber(position, 0f, color, rise, duration);
            var fx = _texts[(_nextText - 1) % _texts.Count];
            fx.Text.text = value;
            fx.Text.fontSize = size;
            Ui.PlaceCentered(fx.Text.rectTransform, fx.Start, new Vector2(320f, size * 1.6f));
        }

        public void Debris(Vector2 position, Color color, int count, float speed, float duration, float size = 6f)
        {
            for (int i = 0; i < count; i++)
            {
                if (_debris.Count < MaxDebris)
                {
                    _debris.Add(new DebrisFx { Image = Ui.CreateImage("Debris", _root, Shape.Square, color) });
                }
                var fx = _debris[_nextDebris % _debris.Count];
                _nextDebris++;
                fx.Image.gameObject.SetActive(true);
                fx.Image.color = color;
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float s = speed * UnityEngine.Random.Range(0.5f, 1.2f);
                fx.Position = position;
                fx.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * s + new Vector2(0f, speed * 0.4f);
                fx.Spin = UnityEngine.Random.Range(-720f, 720f);
                fx.Rotation = UnityEngine.Random.Range(0f, 360f);
                fx.Duration = duration * UnityEngine.Random.Range(0.7f, 1.2f);
                fx.T = 0f;
                float sz = size * UnityEngine.Random.Range(0.6f, 1.4f);
                Ui.PlaceCentered(fx.Image.rectTransform, position, new Vector2(sz, sz));
            }
        }

        public void Ring(Vector2 center, float fromSize, float toSize, Color color, float duration)
        {
            if (_rings.Count < MaxRings)
            {
                _rings.Add(new RingFx { Image = Ui.CreateImage("Ring", _root, Shape.Ring, color) });
            }
            var fx = _rings[_nextRing % _rings.Count];
            _nextRing++;
            fx.Image.gameObject.SetActive(true);
            fx.Center = center;
            fx.From = fromSize;
            fx.To = toSize;
            fx.Color = color;
            fx.Duration = duration;
            fx.T = 0f;
            Ui.PlaceCentered(fx.Image.rectTransform, center, new Vector2(fromSize, fromSize));
        }

        public void Flash(Color color, float alpha, float duration)
        {
            _flash.gameObject.SetActive(true);
            _flash.transform.SetAsLastSibling();
            _flash.color = Ui.WithAlpha(color, alpha);
            _flashAlpha = alpha;
            _flashDuration = Mathf.Max(0.01f, duration);
            _flashT = 0f;
        }

        /// <summary>아이콘이 from에서 to로 포물선을 그리며 날아간다. 도착하면 onArrive.</summary>
        public void Fly(Shape shape, Color color, Vector2 from, Vector2 to, float size, float duration, Action onArrive)
        {
            FlyerFx fx = null;
            foreach (var f in _flyers)
            {
                if (!f.Image.gameObject.activeSelf)
                {
                    fx = f;
                    break;
                }
            }
            if (fx == null)
            {
                if (_flyers.Count >= MaxFlyers)
                {
                    // 너무 많으면 가장 오래된 것을 바로 도착시킨다.
                    var oldest = _flyers[0];
                    oldest.Finish();
                    _flyers.RemoveAt(0);
                    _flyers.Add(oldest);
                    fx = oldest;
                }
                else
                {
                    fx = new FlyerFx { Image = Ui.CreateImage("Flyer", _root, shape, color) };
                    _flyers.Add(fx);
                }
            }
            Ui.SetShape(fx.Image, shape);
            fx.Image.color = color;
            fx.Image.gameObject.SetActive(true);
            fx.From = from;
            fx.To = to;
            fx.Size = size;
            fx.Duration = Mathf.Max(0.01f, duration);
            fx.T = 0f;
            fx.OnArrive = onArrive;
            Ui.PlaceCentered(fx.Image.rectTransform, from, new Vector2(size, size));
        }

        /// <summary>진행 중인 비행을 모두 바로 끝낸다(연출이 조작을 막지 않게).</summary>
        public void FinishFlyers()
        {
            foreach (var fx in _flyers)
            {
                if (fx.Image.gameObject.activeSelf)
                {
                    fx.Finish();
                }
            }
        }

        public void Clear()
        {
            foreach (var fx in _texts) fx.Text.gameObject.SetActive(false);
            foreach (var fx in _debris) fx.Image.gameObject.SetActive(false);
            foreach (var fx in _rings) fx.Image.gameObject.SetActive(false);
            foreach (var fx in _flyers)
            {
                fx.OnArrive = null;
                fx.Image.gameObject.SetActive(false);
            }
            _flash.gameObject.SetActive(false);
        }

        // ───────────── 진행 ─────────────

        public void Update(float dt)
        {
            int activeTexts = 0;
            foreach (var fx in _texts)
            {
                if (!fx.Text.gameObject.activeSelf)
                {
                    continue;
                }
                fx.T += dt;
                float t = fx.T / Mathf.Max(0.01f, fx.Duration);
                if (t >= 1f)
                {
                    fx.Text.gameObject.SetActive(false);
                    continue;
                }
                activeTexts++;
                // 튀어 오르고(처음 30%) 천천히 사라진다.
                float pop = t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : 1f;
                float rise = fx.Rise * (1f - (1f - t) * (1f - t));
                fx.Text.rectTransform.anchoredPosition = fx.Start + new Vector2(0f, rise);
                fx.Text.rectTransform.localScale = Vector3.one * pop * fx.Scale;
                var c = fx.Text.color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                fx.Text.color = c;
            }
            ActiveTextCount = activeTexts;

            foreach (var fx in _debris)
            {
                if (!fx.Image.gameObject.activeSelf)
                {
                    continue;
                }
                fx.T += dt;
                if (fx.T >= fx.Duration)
                {
                    fx.Image.gameObject.SetActive(false);
                    continue;
                }
                fx.Velocity += new Vector2(0f, -420f) * dt;
                fx.Position += fx.Velocity * dt;
                fx.Rotation += fx.Spin * dt;
                var rt = fx.Image.rectTransform;
                rt.anchoredPosition = fx.Position;
                rt.localRotation = Quaternion.Euler(0f, 0f, fx.Rotation);
                var c = fx.Image.color;
                c.a = 1f - fx.T / fx.Duration;
                fx.Image.color = c;
            }

            foreach (var fx in _rings)
            {
                if (!fx.Image.gameObject.activeSelf)
                {
                    continue;
                }
                fx.T += dt;
                float t = fx.T / Mathf.Max(0.01f, fx.Duration);
                if (t >= 1f)
                {
                    fx.Image.gameObject.SetActive(false);
                    continue;
                }
                float eased = 1f - (1f - t) * (1f - t);
                float size = Mathf.Lerp(fx.From, fx.To, eased);
                fx.Image.rectTransform.sizeDelta = new Vector2(size, size);
                fx.Image.color = Ui.WithAlpha(fx.Color, fx.Color.a * (1f - t));
            }

            if (_flash.gameObject.activeSelf)
            {
                _flashT += dt;
                float t = _flashT / _flashDuration;
                if (t >= 1f)
                {
                    _flash.gameObject.SetActive(false);
                }
                else
                {
                    _flash.color = Ui.WithAlpha(_flash.color, _flashAlpha * (1f - t));
                }
            }

            foreach (var fx in _flyers)
            {
                if (!fx.Image.gameObject.activeSelf)
                {
                    continue;
                }
                fx.T += dt;
                float t = Mathf.Clamp01(fx.T / fx.Duration);
                float eased = t * t * (3f - 2f * t);
                var pos = Vector2.Lerp(fx.From, fx.To, eased) + new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 40f);
                var rt = fx.Image.rectTransform;
                rt.anchoredPosition = pos;
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
                rt.localScale = new Vector3(scale, scale, 1f);
                if (t >= 1f)
                {
                    fx.Finish();
                }
            }
        }

        private sealed class TextFx
        {
            public Text Text;
            public Vector2 Start;
            public float Rise;
            public float Duration;
            public float Scale;
            public float T;
        }

        private sealed class DebrisFx
        {
            public Image Image;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Spin;
            public float Rotation;
            public float Duration;
            public float T;
        }

        private sealed class RingFx
        {
            public Image Image;
            public Vector2 Center;
            public float From;
            public float To;
            public Color Color;
            public float Duration;
            public float T;
        }

        private sealed class FlyerFx
        {
            public Image Image;
            public Vector2 From;
            public Vector2 To;
            public float Size;
            public float Duration;
            public float T;
            public Action OnArrive;

            public void Finish()
            {
                Image.gameObject.SetActive(false);
                var callback = OnArrive;
                OnArrive = null;
                callback?.Invoke();
            }
        }
    }
}
