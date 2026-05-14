using UnityEngine;
using UnityEngine.UIElements;

namespace HackSlash.UI
{
    /// <summary>
    /// Wave-progress pip. By default a slim left-leaning parallelogram. The boss state
    /// switches to a diamond. State (cleared / current / boss) is exposed as USS pseudo
    /// classes via toggling C# properties — the controller swaps them as the wave changes.
    /// </summary>
    public class Pip : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<Pip, UxmlTraits> {}

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlBoolAttributeDescription _cleared = new() { name = "cleared", defaultValue = false };
            private readonly UxmlBoolAttributeDescription _current = new() { name = "current", defaultValue = false };
            private readonly UxmlBoolAttributeDescription _boss = new() { name = "boss", defaultValue = false };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var pip = (Pip)ve;
                pip.Cleared = _cleared.GetValueFromBag(bag, cc);
                pip.Current = _current.GetValueFromBag(bag, cc);
                pip.Boss = _boss.GetValueFromBag(bag, cc);
            }
        }

        private bool cleared;
        private bool current;
        private bool boss;
        private float pulseT;

        public Color Accent { get; set; } = new(1f, 0.18f, 0.29f, 1f);
        public Color Gold { get; set; } = new(1f, 0.81f, 0.30f, 1f);
        public Color EmptyFill { get; set; } = new(1f, 1f, 1f, 0.06f);
        public Color EmptyBorder { get; set; } = new(1f, 1f, 1f, 0.13f);
        public Color CurrentBorder { get; set; } = new(0.96f, 0.95f, 0.92f, 1f);

        public bool Cleared { get => cleared; set { if (cleared == value) return; cleared = value; MarkDirtyRepaint(); } }
        public bool Current { get => current; set { if (current == value) return; current = value; MarkDirtyRepaint(); } }
        public bool Boss { get => boss; set { if (boss == value) return; boss = value; MarkDirtyRepaint(); } }

        /// <summary>0..1 brightness pulse for the current pip; driven by the controller.</summary>
        public float Pulse
        {
            get => pulseT;
            set { if (Mathf.Approximately(pulseT, value)) return; pulseT = value; if (current) MarkDirtyRepaint(); }
        }

        public Pip()
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
            var p = mgc.painter2D;

            if (boss)
            {
                Vector2 c = new(w * 0.5f, h * 0.5f);
                p.fillColor = new Color(0.16f, 0.10f, 0.03f, 1f);
                p.BeginPath();
                p.MoveTo(new Vector2(c.x, 0f));
                p.LineTo(new Vector2(w, c.y));
                p.LineTo(new Vector2(c.x, h));
                p.LineTo(new Vector2(0f, c.y));
                p.ClosePath();
                p.Fill();

                Color stroke = cleared ? Color.Lerp(Gold, Color.white, 0.4f) : Gold;
                if (current)
                {
                    float pulse = 0.6f + 0.4f * Mathf.Sin(pulseT * Mathf.PI * 2f);
                    stroke = Color.Lerp(Gold, Color.white, pulse);
                }
                p.strokeColor = stroke;
                p.lineWidth = 1.5f;
                p.BeginPath();
                p.MoveTo(new Vector2(c.x, 0.5f));
                p.LineTo(new Vector2(w - 0.5f, c.y));
                p.LineTo(new Vector2(c.x, h - 0.5f));
                p.LineTo(new Vector2(0.5f, c.y));
                p.ClosePath();
                p.Stroke();
                return;
            }

            float s = Mathf.Min(4f, h * 0.5f, w * 0.5f);

            Color fill, border;
            if (current)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(pulseT * Mathf.PI * 2f);
                fill = Accent * pulse;
                fill.a = 1f;
                border = CurrentBorder;
            }
            else if (cleared)
            {
                fill = Accent;
                border = Accent;
            }
            else
            {
                fill = EmptyFill;
                border = EmptyBorder;
            }

            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(new Vector2(s, 0f));
            p.LineTo(new Vector2(w, 0f));
            p.LineTo(new Vector2(w - s, h));
            p.LineTo(new Vector2(0f, h));
            p.ClosePath();
            p.Fill();

            p.strokeColor = border;
            p.lineWidth = 1f;
            p.BeginPath();
            p.MoveTo(new Vector2(s, 0.5f));
            p.LineTo(new Vector2(w - 0.5f, 0.5f));
            p.LineTo(new Vector2(w - s - 0.5f, h - 0.5f));
            p.LineTo(new Vector2(0.5f, h - 0.5f));
            p.ClosePath();
            p.Stroke();
        }
    }
}
