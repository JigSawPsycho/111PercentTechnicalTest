using UnityEngine;
using UnityEngine.UIElements;

namespace HackSlash.UI
{
    /// <summary>
    /// Full-screen overlay that paints the gold "Unstoppable" frame via Painter2D.
    /// Replaces the previous approach of four sibling background-color rectangles,
    /// which only rendered the top strip — Yoga refuses to derive a child's size
    /// from `top:0; bottom:0` / `left:0; right:0;` anchor pairs in this panel,
    /// regardless of nesting. Drawing inside one element bypasses the four-sibling
    /// layout, but the same Yoga limit means we can't size THIS element via USS
    /// either; instead we mirror parent.resolvedStyle into inline width/height on
    /// every parent GeometryChangedEvent so contentRect is always the full panel.
    ///
    /// Two layers per draw:
    ///   1) a soft inner glow (gold → transparent) so the screen reads as bloomed
    ///      in gold, matching the bar/chrome bloom vibe rather than just a flat
    ///      decal at the edges.
    ///   2) a hard solid border ring of `Thickness` pixels along the very edge.
    /// </summary>
    public class UltimateFrame : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<UltimateFrame, UxmlTraits> {}

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlFloatAttributeDescription _thickness = new() { name = "thickness", defaultValue = 10f };
            private readonly UxmlFloatAttributeDescription _glowDepth = new() { name = "glow-depth", defaultValue = 120f };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var f = (UltimateFrame)ve;
                f.Thickness = _thickness.GetValueFromBag(bag, cc);
                f.GlowDepth = _glowDepth.GetValueFromBag(bag, cc);
            }
        }

        private float thickness = 10f;
        private float glowDepth = 120f;
        private Color frameColor = new(1f, 0.84f, 0.42f, 0f);
        private Color glowColor = new(1f, 0.84f, 0.42f, 0f);

        public float Thickness
        {
            get => thickness;
            set { if (Mathf.Approximately(thickness, value)) return; thickness = value; MarkDirtyRepaint(); }
        }

        public float GlowDepth
        {
            get => glowDepth;
            set { if (Mathf.Approximately(glowDepth, value)) return; glowDepth = value; MarkDirtyRepaint(); }
        }

        public Color FrameColor
        {
            get => frameColor;
            set { frameColor = value; MarkDirtyRepaint(); }
        }

        public Color GlowColor
        {
            get => glowColor;
            set { glowColor = value; MarkDirtyRepaint(); }
        }

        public UltimateFrame()
        {
            pickingMode = PickingMode.Ignore;
            // Position ourselves explicitly: USS-driven anchoring with
            // `top:0; bottom:0; left:0; right:0;` does NOT give this element
            // a non-zero contentRect in this panel (Yoga doesn't propagate
            // parent size to absolute-positioned children that derive their
            // dimensions from anchor pairs). Without size, Painter2D draws
            // nothing. So we drive width/height from parent.resolvedStyle
            // every layout pass.
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            generateVisualContent += OnGenerate;
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private VisualElement parentTracked;

        private void OnAttach(AttachToPanelEvent _)
        {
            parentTracked = parent;
            if (parentTracked != null)
            {
                parentTracked.RegisterCallback<GeometryChangedEvent>(OnParentGeometry);
                SyncToParent();
                // Defensive: if the parent's initial layout already happened
                // before we attached, GeometryChangedEvent will not fire again
                // and the SyncToParent above would have read a stale 0×0
                // resolvedStyle. Re-sync each frame until we've measured a
                // non-zero parent size, then stop.
                schedule.Execute(SyncToParent).Until(() =>
                    parent == null
                    || (parent.resolvedStyle.width > 1f && parent.resolvedStyle.height > 1f
                        && Mathf.Approximately(style.width.value.value, parent.resolvedStyle.width)
                        && Mathf.Approximately(style.height.value.value, parent.resolvedStyle.height)));
            }
        }

        private void OnDetach(DetachFromPanelEvent _)
        {
            if (parentTracked != null)
            {
                parentTracked.UnregisterCallback<GeometryChangedEvent>(OnParentGeometry);
                parentTracked = null;
            }
        }

        private void OnParentGeometry(GeometryChangedEvent _) => SyncToParent();

        private void SyncToParent()
        {
            if (parent == null) return;
            float w = parent.resolvedStyle.width;
            float h = parent.resolvedStyle.height;
            if (w > 1f) style.width = w;
            if (h > 1f) style.height = h;
        }

        private void OnGenerate(MeshGenerationContext mgc)
        {
            Rect r = contentRect;
            float w = r.width;
            float h = r.height;
            if (w <= 1f || h <= 1f) return;

            // 1) Soft inner glow — four vertex-coloured quads, one per side,
            // gold at the edge fading to fully transparent inward.
            if (glowColor.a > 0.001f && glowDepth > 0f)
            {
                float d = Mathf.Min(glowDepth, Mathf.Min(w, h) * 0.5f);
                Color near = glowColor;
                Color far = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
                // Each call: (TL, TR, BR, BL) tints. "near" sits on the edge of the screen.
                DrawTintedQuad(mgc, 0f,     0f,     w, d, near, near, far,  far);   // top:    edge=top
                DrawTintedQuad(mgc, 0f,     h - d, w, d, far,  far,  near, near);   // bottom: edge=bottom
                DrawTintedQuad(mgc, 0f,     0f,     d, h, near, far,  far,  near);   // left:   edge=left
                DrawTintedQuad(mgc, w - d, 0f,     d, h, far,  near, near, far);    // right:  edge=right
            }

            // 2) Hard outer ring — four filled rectangles forming a hollow frame.
            if (frameColor.a > 0.001f && thickness > 0f)
            {
                float t = Mathf.Min(thickness, Mathf.Min(w, h) * 0.5f);
                var p = mgc.painter2D;
                p.fillColor = frameColor;

                // Top strip spans the full width.
                FillRect(p, 0f, 0f, w, t);
                // Bottom strip spans the full width.
                FillRect(p, 0f, h - t, w, t);
                // Left and right strips skip the corners that the top/bottom strips
                // already cover, so the painter doesn't double-write those pixels.
                FillRect(p, 0f, t, t, h - 2f * t);
                FillRect(p, w - t, t, t, h - 2f * t);
            }
        }

        private static void FillRect(Painter2D p, float x, float y, float rw, float rh)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x, y));
            p.LineTo(new Vector2(x + rw, y));
            p.LineTo(new Vector2(x + rw, y + rh));
            p.LineTo(new Vector2(x, y + rh));
            p.ClosePath();
            p.Fill();
        }

        private static void DrawTintedQuad(MeshGenerationContext mgc, float x, float y, float qw, float qh,
                                           Color tl, Color tr, Color br, Color bl)
        {
            var mesh = mgc.Allocate(4, 6);
            mesh.SetNextVertex(new Vertex { position = new Vector3(x,        y,        Vertex.nearZ), tint = tl });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x + qw,   y,        Vertex.nearZ), tint = tr });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x + qw,   y + qh,   Vertex.nearZ), tint = br });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x,        y + qh,   Vertex.nearZ), tint = bl });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);
        }
    }
}
