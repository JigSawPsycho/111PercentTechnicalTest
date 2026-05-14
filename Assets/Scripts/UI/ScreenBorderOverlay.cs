using UnityEngine;
using UnityEngine.UIElements;

namespace HackSlash.UI
{
    /// <summary>
    /// Owns a dedicated UIDocument that paints full-screen edge effects.
    /// Two modes are supported and they are mutually exclusive — only the
    /// gold "Unstoppable" frame OR the red "took damage" flash can be
    /// visible at any moment, never both. Other code (typically the HUD)
    /// calls Show(mode, alpha) every frame while an effect is active and
    /// Hide() when both are idle; switching modes automatically clears the
    /// other.
    ///
    /// The document is intentionally separate from the main HUD UIDocument
    /// so the border can render above HUD chrome on its own panel without
    /// polluting the HUD's UXML or its layout passes.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScreenBorderOverlay : MonoBehaviour
    {
        public enum Mode { None, Ultimate, Hit }

        [Header("Ultimate frame")]
        [SerializeField] private Color ultColor = new(1f, 0.84f, 0.42f, 1f);
        [SerializeField] private Color ultGlowColor = new(1f, 0.74f, 0.28f, 1f);
        [SerializeField, Range(0f, 1f)] private float ultGlowAlphaScale = 0.45f;
        [SerializeField, Min(0f)] private float ultThickness = 5f;
        [SerializeField, Min(0f)] private float ultGlowDepth = 70f;

        [Header("Hit-flash frame")]
        // Hit-flash has no hard outer ring — just a soft inward glow. Two
        // perpendicular glows overlap at each corner, which produces the
        // brighter vignette read the design wanted without a hard line at
        // the screen edge that would compete with the gold ult ring.
        [SerializeField] private Color hitColor = new(1f, 0.13f, 0.2f, 1f);
        [SerializeField, Min(0f)] private float hitGlowDepth = 55f;

        [Header("Panel")]
        // Higher than HUD's panel (sortingOrder = 100) so the border paints
        // on top of HUD chrome. Lower would let the player-panel etc. cover
        // the inner glow where they overlap the screen edges.
        [SerializeField, Min(0)] private int sortingOrder = 110;
        [SerializeField] private Vector2Int referenceResolution = new(1920, 1080);

        private UIDocument doc;
        private PanelSettings panel;
        private UltimateFrame ultFrame;
        private UltimateFrame hitFrame;

        public Mode CurrentMode { get; private set; }

        private void Awake()
        {
            BuildDocument();
        }

        private void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }

        private void BuildDocument()
        {
            doc = GetComponent<UIDocument>();
            if (doc == null) doc = gameObject.AddComponent<UIDocument>();

            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "ScreenBorder_PanelSettings";
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = referenceResolution;
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = sortingOrder;
            doc.panelSettings = panel;

            // The visual tree is small (two frames) and has no styling beyond
            // what the elements paint themselves, so we build it in code
            // rather than authoring a UXML — keeps the asset surface smaller.
            var root = doc.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            // Programmatically-created UIDocument roots don't auto-size to
            // the panel reliably: without flex content to drag the layout,
            // the root stays at 0×0 and our absolute-positioned UltimateFrame
            // children measure their parent as 0×0 too, so Painter2D draws
            // nothing. Pin the root to the panel's reference resolution; the
            // panel's ScaleWithScreenSize handles fitting to the real window.
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.width = referenceResolution.x;
            root.style.height = referenceResolution.y;

            ultFrame = new UltimateFrame
            {
                name = "ult-frame",
                Thickness = ultThickness,
                GlowDepth = ultGlowDepth,
            };
            hitFrame = new UltimateFrame
            {
                name = "hit-frame",
                Thickness = 0f,
                GlowDepth = hitGlowDepth,
            };

            root.Add(ultFrame);
            root.Add(hitFrame);
        }

        /// <summary>
        /// Show one mode at <paramref name="alpha"/> (0..1). The other mode is
        /// cleared so the two effects can never overlap. An alpha at or below
        /// 0.001 is treated as Hide().
        /// </summary>
        public void Show(Mode mode, float alpha)
        {
            if (mode == Mode.None || alpha <= 0.001f)
            {
                Hide();
                return;
            }

            CurrentMode = mode;
            switch (mode)
            {
                case Mode.Ultimate:
                    ApplyUlt(alpha);
                    ClearHit();
                    break;
                case Mode.Hit:
                    ApplyHit(alpha);
                    ClearUlt();
                    break;
            }
        }

        public void Hide()
        {
            CurrentMode = Mode.None;
            ClearUlt();
            ClearHit();
        }

        private void ApplyUlt(float alpha)
        {
            if (ultFrame == null) return;
            ultFrame.FrameColor = new Color(ultColor.r, ultColor.g, ultColor.b, alpha);
            ultFrame.GlowColor = new Color(ultGlowColor.r, ultGlowColor.g, ultGlowColor.b, alpha * ultGlowAlphaScale);
        }

        private void ApplyHit(float alpha)
        {
            if (hitFrame == null) return;
            hitFrame.GlowColor = new Color(hitColor.r, hitColor.g, hitColor.b, alpha);
        }

        private void ClearUlt()
        {
            if (ultFrame == null) return;
            ultFrame.FrameColor = Color.clear;
            ultFrame.GlowColor = Color.clear;
        }

        private void ClearHit()
        {
            if (hitFrame == null) return;
            hitFrame.FrameColor = Color.clear;
            hitFrame.GlowColor = Color.clear;
        }
    }
}
