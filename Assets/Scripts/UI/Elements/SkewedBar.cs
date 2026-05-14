using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HackSlash.UI
{
    /// <summary>
    /// Parallelogram-shaped progress bar painted via UI Toolkit's MeshGenerationContext
    /// (Painter2D + a vertex-coloured mesh). USS can't do CSS clip-path, so this element
    /// draws the angled track, a "trail" overlay that lags the main fill, the gradient
    /// fill, optional notches, and a 1px stroke — all in one pass.
    /// </summary>
    public class SkewedBar : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<SkewedBar, UxmlTraits> {}

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlFloatAttributeDescription _progress = new() { name = "progress", defaultValue = 0f };
            private readonly UxmlFloatAttributeDescription _trail = new() { name = "trail", defaultValue = 0f };
            private readonly UxmlFloatAttributeDescription _skew = new() { name = "skew", defaultValue = 8f };
            private readonly UxmlBoolAttributeDescription _notches = new() { name = "notches", defaultValue = false };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var bar = (SkewedBar)ve;
                bar.Progress = _progress.GetValueFromBag(bag, cc);
                bar.Trail = _trail.GetValueFromBag(bag, cc);
                bar.Skew = _skew.GetValueFromBag(bag, cc);
                bar.DrawNotches = _notches.GetValueFromBag(bag, cc);
            }
        }

        private float progress;
        private float trail;
        private float skew = 8f;
        private bool drawNotches;

        private Color fillStart = new(1f, 0.18f, 0.29f, 1f);
        private Color fillEnd = new(1f, 0.42f, 0.23f, 1f);
        private Color trailColor = new(1f, 1f, 1f, 0.6f);
        private Color trackColor = new(0.04f, 0.05f, 0.07f, 0.8f);
        private Color borderColor = new(1f, 1f, 1f, 0.13f);
        private Color notchColor = new(1f, 1f, 1f, 0.19f);
        private Color glowColor = new(1f, 0.18f, 0.29f, 0.4f);

        public float Progress
        {
            get => progress;
            set { float v = Mathf.Clamp01(value); if (Mathf.Approximately(v, progress)) return; progress = v; MarkDirtyRepaint(); }
        }

        public float Trail
        {
            get => trail;
            set { float v = Mathf.Clamp01(value); if (Mathf.Approximately(v, trail)) return; trail = v; MarkDirtyRepaint(); }
        }

        public float Skew
        {
            get => skew;
            set { if (Mathf.Approximately(value, skew)) return; skew = value; MarkDirtyRepaint(); }
        }

        public bool DrawNotches
        {
            get => drawNotches;
            set { if (drawNotches == value) return; drawNotches = value; MarkDirtyRepaint(); }
        }

        public Color FillStart { get => fillStart; set { fillStart = value; MarkDirtyRepaint(); } }
        public Color FillEnd { get => fillEnd; set { fillEnd = value; MarkDirtyRepaint(); } }
        public Color TrailColor { get => trailColor; set { trailColor = value; MarkDirtyRepaint(); } }
        public Color TrackColor { get => trackColor; set { trackColor = value; MarkDirtyRepaint(); } }
        public Color BorderColor { get => borderColor; set { borderColor = value; MarkDirtyRepaint(); } }
        public Color NotchColor { get => notchColor; set { notchColor = value; MarkDirtyRepaint(); } }
        public Color GlowColor { get => glowColor; set { glowColor = value; MarkDirtyRepaint(); } }

        public SkewedBar()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerate;
        }

        private void OnGenerate(MeshGenerationContext mgc)
        {
            Rect r = contentRect;
            if (r.width <= 1f || r.height <= 1f) return;

            float w = r.width;
            float h = r.height;
            float s = Mathf.Min(skew, w * 0.5f);

            var p = mgc.painter2D;

            // 1) Track (dark parallelogram background)
            p.fillColor = trackColor;
            BuildParallelogramPath(p, 0f, w, h, s);
            p.Fill();

            // 2) Trail (lags behind main fill — used for the "damage trail" white wash)
            if (trail > 0f)
            {
                float trailW = w * trail;
                p.fillColor = trailColor;
                BuildClippedFillPath(p, trailW, w, h, s);
                p.Fill();
            }

            // 3) Main fill — drawn as a vertex-coloured mesh so we get a horizontal gradient
            if (progress > 0f)
                DrawGradientFill(mgc, w * progress, w, h, s);

            // 4) Notches
            if (drawNotches)
            {
                p.fillColor = notchColor;
                for (int i = 1; i <= 3; i++)
                {
                    float t = i * 0.25f;
                    float xt = Mathf.Lerp(s, w, t);
                    float xb = Mathf.Lerp(0f, w - s, t);
                    p.BeginPath();
                    p.MoveTo(new Vector2(xt - 0.5f, 0f));
                    p.LineTo(new Vector2(xt + 0.5f, 0f));
                    p.LineTo(new Vector2(xb + 0.5f, h));
                    p.LineTo(new Vector2(xb - 0.5f, h));
                    p.ClosePath();
                    p.Fill();
                }
            }

            // 5) Border
            if (borderColor.a > 0f)
            {
                p.strokeColor = borderColor;
                p.lineWidth = 1f;
                BuildParallelogramPath(p, 0f, w, h, s);
                p.Stroke();
            }
        }

        private static void BuildParallelogramPath(Painter2D p, float x0, float x1, float h, float s)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x0 + s, 0f));
            p.LineTo(new Vector2(x1, 0f));
            p.LineTo(new Vector2(x1 - s, h));
            p.LineTo(new Vector2(x0, h));
            p.ClosePath();
        }

        /// <summary>
        /// Build a left-anchored fill path that respects the parallelogram's left slope
        /// (so very low fills "peek" through the chamfer) and follows the right slope when
        /// the fill nears 100%. The fill's right edge stays vertical in the middle range
        /// (matching the CSS rectangle-clipped-by-clip-path look).
        /// </summary>
        private static void BuildClippedFillPath(Painter2D p, float wFill, float w, float h, float s)
        {
            p.BeginPath();
            if (wFill <= 0f) return;

            if (wFill <= s)
            {
                // Tiny fill — only a small bottom-left triangle peeks below the chamfer.
                // Chamfer line is x = s·(1 − y/h), so it crosses x=wFill at y=h·(1 − wFill/s).
                float yTop = h * (1f - wFill / s);
                p.MoveTo(new Vector2(wFill, yTop));
                p.LineTo(new Vector2(wFill, h));
                p.LineTo(new Vector2(0f, h));
                p.ClosePath();
                return;
            }

            p.MoveTo(new Vector2(s, 0f));
            float topRightX = Mathf.Min(wFill, w);
            p.LineTo(new Vector2(topRightX, 0f));

            if (wFill > w - s)
            {
                // Right edge follows the parallelogram's right slope from the entry point downward.
                float yIntersect = Mathf.Clamp((w - wFill) * h / s, 0f, h);
                p.LineTo(new Vector2(wFill, yIntersect));
                p.LineTo(new Vector2(w - s, h));
            }
            else
            {
                p.LineTo(new Vector2(wFill, h));
            }
            p.LineTo(new Vector2(0f, h));
            p.ClosePath();
        }

        private readonly List<Vector2> _polyBuf = new(6);

        private void DrawGradientFill(MeshGenerationContext mgc, float wFill, float w, float h, float s)
        {
            if (wFill <= 0f) return;

            _polyBuf.Clear();

            if (wFill <= s)
            {
                float yTop = h * (1f - wFill / s);
                _polyBuf.Add(new Vector2(wFill, yTop));
                _polyBuf.Add(new Vector2(wFill, h));
                _polyBuf.Add(new Vector2(0f, h));
            }
            else
            {
                _polyBuf.Add(new Vector2(s, 0f));
                float topRightX = Mathf.Min(wFill, w);
                _polyBuf.Add(new Vector2(topRightX, 0f));
                if (wFill > w - s)
                {
                    float yIntersect = Mathf.Clamp((w - wFill) * h / s, 0f, h);
                    _polyBuf.Add(new Vector2(wFill, yIntersect));
                    _polyBuf.Add(new Vector2(w - s, h));
                }
                else
                {
                    _polyBuf.Add(new Vector2(wFill, h));
                }
                _polyBuf.Add(new Vector2(0f, h));
            }

            int n = _polyBuf.Count;
            if (n < 3) return;

            var mesh = mgc.Allocate(n, (n - 2) * 3);

            // Gradient is mapped across the full bar width (not just the fill) so the
            // colour at each x stays put as the bar drains/refills — matches CSS
            // background-image stretching with the element's width.
            for (int i = 0; i < n; i++)
            {
                Vector2 pt = _polyBuf[i];
                float t = Mathf.Clamp01(pt.x / Mathf.Max(1f, w));
                Color c = Color.Lerp(fillStart, fillEnd, t);
                mesh.SetNextVertex(new Vertex
                {
                    position = new Vector3(pt.x, pt.y, Vertex.nearZ),
                    tint = c
                });
            }
            // Fan triangulation around vertex 0
            for (int i = 1; i < n - 1; i++)
            {
                mesh.SetNextIndex(0);
                mesh.SetNextIndex((ushort)i);
                mesh.SetNextIndex((ushort)(i + 1));
            }
        }
    }
}
