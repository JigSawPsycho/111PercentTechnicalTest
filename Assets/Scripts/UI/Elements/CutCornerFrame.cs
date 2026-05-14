using UnityEngine;
using UnityEngine.UIElements;

namespace HackSlash.UI
{
    /// <summary>
    /// Hexagonal "cut-corner" frame (top-right and bottom-left clipped at a 45°).
    /// Mirrors the portrait frame in the design (CSS clip-path: polygon(0 0, 100% - 16px 0,
    /// 100% 16px, 100% 100%, 16px 100%, 0 100% - 16px)).
    ///
    /// Drawn via Painter2D — child elements are not visually clipped by this shape; the
    /// child container should sit slightly inset so the cuts read as a frame around it.
    /// </summary>
    public class CutCornerFrame : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<CutCornerFrame, UxmlTraits> {}

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlFloatAttributeDescription _cut = new() { name = "cut", defaultValue = 16f };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                ((CutCornerFrame)ve).Cut = _cut.GetValueFromBag(bag, cc);
            }
        }

        private float cut = 16f;
        private Color frameFill = new(0.0f, 0.0f, 0.0f, 0f);
        private Color frameBorder = new(1f, 0.18f, 0.29f, 1f);
        private float borderWidth = 2f;

        public float Cut { get => cut; set { if (Mathf.Approximately(cut, value)) return; cut = value; MarkDirtyRepaint(); } }
        public Color FrameFill { get => frameFill; set { frameFill = value; MarkDirtyRepaint(); } }
        public Color FrameBorder { get => frameBorder; set { frameBorder = value; MarkDirtyRepaint(); } }
        public float BorderWidth { get => borderWidth; set { borderWidth = value; MarkDirtyRepaint(); } }

        public CutCornerFrame()
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
            float c = Mathf.Min(cut, w * 0.5f, h * 0.5f);

            var p = mgc.painter2D;

            // Fill
            if (frameFill.a > 0f)
            {
                p.fillColor = frameFill;
                BuildPath(p, w, h, c);
                p.Fill();
            }

            // Stroke
            if (frameBorder.a > 0f && borderWidth > 0f)
            {
                p.strokeColor = frameBorder;
                p.lineWidth = borderWidth;
                BuildPath(p, w, h, c);
                p.Stroke();
            }
        }

        private static void BuildPath(Painter2D p, float w, float h, float c)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(0f, 0f));
            p.LineTo(new Vector2(w - c, 0f));
            p.LineTo(new Vector2(w, c));
            p.LineTo(new Vector2(w, h));
            p.LineTo(new Vector2(c, h));
            p.LineTo(new Vector2(0f, h - c));
            p.ClosePath();
        }
    }
}
