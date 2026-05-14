using System.Collections.Generic;
using HackSlash.Abilities;
using HackSlash.Core;
using HackSlash.Waves;
using UnityEngine;
using UnityEngine.UIElements;

namespace HackSlash.UI
{
    /// <summary>
    /// UI Toolkit driver for the Neon Brawler HUD. Loads the UXML + USS from Resources,
    /// builds a runtime PanelSettings, and wires the document to the game's Health,
    /// WaveSpawner, combo coordinator, and special abilities. The class name + namespace
    /// match the old uGUI HUD on purpose so the existing scene's MonoBehaviour reference
    /// keeps resolving; any old serialized fields it no longer uses are simply ignored.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HUD : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private WaveSpawner spawner;

        [Header("Display")]
        [SerializeField] private string playerName = "RAVEN KOJIMA";
        [SerializeField] private string districtTag = "DISTRICT 7 · LOWER NEON";
        [SerializeField] private Color accent = new(1f, 0.18f, 0.29f, 1f);

        [Header("Tuning")]
        [SerializeField, Min(0.01f)] private float trailLag = 1.2f;
        [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.25f;

        [Header("Hit Shake")]
        [SerializeField, Min(0f)] private float shakeDuration = 0.22f;
        [SerializeField, Min(0f)] private float shakeMagnitude = 6f;

        [Header("Hit Flash")]
        // Timing only — the color and edge geometry live on the
        // ScreenBorderOverlay component, which owns the actual render.
        [SerializeField, Min(0f)] private float hitFlashDuration = 0.45f;
        [SerializeField, Range(0f, 1f)] private float hitFlashPeakAlpha = 0.55f;

        private UIDocument doc;
        private PanelSettings runtimePanel;
        private VisualElement root;
        private UIDocument inputStripDoc;
        private PanelSettings inputStripPanel;
        private GameObject inputStripGO;

        // bound elements
        private SkewedBar hpBar, chgBar, ultBar;
        private Label hpVal, hpMaxLbl;
        private Label chgVal, ultVal;
        private VisualElement abilityCharge, abilityDodge, abilityUlt;
        private VisualElement ultBarRow;
        private VisualElement eventBanner, comboPanel, playerPanel;
        // Full-screen gold/red edge effects live on a separate UIDocument
        // managed by ScreenBorderOverlay. HUD owns the timers + state and
        // pushes a single Show/Hide decision per frame; the overlay enforces
        // mutex so the two effects can't overlap. Chrome elements still have
        // their existing 1px outlines retinted gold inline during Unstoppable.
        private GameObject screenBorderGO;
        private ScreenBorderOverlay screenBorder;
        private float currentUltAlpha;
        private float currentHitAlpha;
        private VisualElement enemyTracker;
        private readonly List<VisualElement> chromeBorderTargets = new();
        private bool chromeBordersCleared = true; // skip per-frame Null writes when already idle
        private float ultFrameVisibility; // manual 0→1 fade-in / 1→0 fade-out tween
        private Label playerNameLabel, waveEyebrowLabel, waveNum, waveOf;
        private VisualElement wavePips, enemyIconsContainer;
        private Label etNow, etTot, etypeMelee, etypeRanged;
        private VisualElement enemyBarFill;
        private Label ebLine, ebSub, comboNum;

        private readonly List<Pip> pips = new();
        private readonly List<VisualElement> enemyIcons = new();

        // bound systems
        private Health playerHealth;
        private MeleeComboCoordinator combo;
        private ChargeDashAbility chargeDash;
        private DodgeAbility dodge;
        private UnstoppableAbility unstoppable;
        private System.Action unstoppableActivatedHandler;
        private bool subscribedGameManager;

        // smoothing state
        private float hpDisplay;
        private float hpTrailDisplay;
        private float chgDisplay;
        private float ultDisplay;
        private bool wasCritical;
        private float criticalAccum;
        private float currentPipPhase;
        private float shakeTimeLeft;
        private bool shakeApplied;
        private float hitFlashTimeLeft;

        // banner / combo tasks
        private IVisualElementScheduledItem bannerHideTask;
        private IVisualElementScheduledItem followupShowTask;
        private IVisualElementScheduledItem comboHideTask;

        // wave caching
        private int lastWaveIndex = int.MinValue;
        private int lastWaveCount = -1;
        private int lastEnemyTotal = -1;
        private float lastEnemyBarPct = -1f;

        // colour caches so we don't rebuild gradients every frame
        private Color hpFillStart, hpFillEnd;
        private Color chgFillStart, chgFillEnd;
        private Color ultFillStart, ultFillEnd, ultGlow;
        private Color ultActiveStart, ultActiveEnd;
        private bool koShown;

        private void Awake()
        {
            HideLegacyCanvas();
            BuildUIDocument();
            BuildScreenBorderOverlay();
        }

        private void OnEnable()
        {
            if (root != null) PaintAccent();
        }

        private void Start()
        {
            if (spawner == null) spawner = FindFirstObjectByType<WaveSpawner>();
            if (spawner != null)
            {
                spawner.WaveStarted += OnWaveStarted;
                spawner.WaveCleared += OnWaveCleared;
                spawner.AllWavesCleared += OnAllWavesCleared;
            }
        }

        private void OnDestroy()
        {
            if (spawner != null)
            {
                spawner.WaveStarted -= OnWaveStarted;
                spawner.WaveCleared -= OnWaveCleared;
                spawner.AllWavesCleared -= OnAllWavesCleared;
            }
            UnbindPlayer();
            if (subscribedGameManager && GameManager.Instance != null)
            {
                GameManager.Instance.PlayerDied -= OnPlayerDied;
                GameManager.Instance.GameWon -= OnGameWon;
            }
            if (runtimePanel != null) Destroy(runtimePanel);
            if (inputStripPanel != null) Destroy(inputStripPanel);
            if (inputStripGO != null) Destroy(inputStripGO);
            if (screenBorderGO != null) Destroy(screenBorderGO);
        }

        private void HideLegacyCanvas()
        {
            // The old uGUI HUD lives on this GameObject. Disable the Canvas so nothing
            // it draws lingers underneath the new UI Toolkit panel.
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = false;
        }

        private void BuildUIDocument()
        {
            doc = GetComponent<UIDocument>();
            if (doc == null) doc = gameObject.AddComponent<UIDocument>();

            runtimePanel = ScriptableObject.CreateInstance<PanelSettings>();
            runtimePanel.name = "NeonHUD_PanelSettings";
            runtimePanel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            runtimePanel.referenceResolution = new Vector2Int(1920, 1080);
            runtimePanel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            runtimePanel.match = 0.5f;
            runtimePanel.sortingOrder = 100;
            doc.panelSettings = runtimePanel;

            var tree = Resources.Load<VisualTreeAsset>("NeonHUD");
            if (tree == null)
            {
                Debug.LogError("[HUD] Missing Resources/NeonHUD.uxml.");
                return;
            }
            doc.visualTreeAsset = tree;
            root = doc.rootVisualElement;
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;

            // The UXML embeds <Style src="HUD.uss"/> so the stylesheet usually loads
            // automatically. Re-attach defensively if the linked reference didn't
            // resolve at build/import time.
            var sheet = Resources.Load<StyleSheet>("HUD");
            if (sheet != null && !root.styleSheets.Contains(sheet))
                root.styleSheets.Add(sheet);

            BindElements();
            // HUD is display-only — block every element from intercepting pointer events
            // so the game still receives clicks/keys cleanly.
            root.Query<VisualElement>().ForEach(v => v.pickingMode = PickingMode.Ignore);
            PaintAccent();
            if (playerNameLabel != null) playerNameLabel.text = playerName;
            if (waveEyebrowLabel != null) waveEyebrowLabel.text = districtTag;

            BuildInputStripDocument();
        }

        // The input strip used to live inside NeonHUD.uxml under a `.hud-br`
        // wrapper, but Yoga refuses to propagate the main panel's dimensions to
        // its absolute children (see the `.ult-frame` note in HUD.uss), so a
        // strip anchored with `position: absolute; bottom; right` collapsed to
        // 0×0. Loading it into its own UIDocument gives it a fresh panel root
        // that Unity sizes directly from PanelSettings — the strip's anchors
        // resolve against a real rectangle and render where the design intends.
        private void BuildInputStripDocument()
        {
            // Must be a root-level GameObject (NOT parented to HUD): Unity's
            // UIDocument asserts that a child UIDocument shares its parent's
            // PanelSettings. We want a separate panel here so the strip lays
            // out independently, so we keep the GameObjects unparented.
            inputStripGO = new GameObject("InputStripDoc");

            inputStripDoc = inputStripGO.AddComponent<UIDocument>();

            inputStripPanel = ScriptableObject.CreateInstance<PanelSettings>();
            inputStripPanel.name = "InputStrip_PanelSettings";
            inputStripPanel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            inputStripPanel.referenceResolution = new Vector2Int(1920, 1080);
            inputStripPanel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            inputStripPanel.match = 0.5f;
            inputStripPanel.sortingOrder = 101; // one above the main HUD panel.
            // Inherit the main panel's theme — without one, Unity logs a warning
            // and falls back to a partially-styled render.
            inputStripPanel.themeStyleSheet = runtimePanel.themeStyleSheet;
            inputStripDoc.panelSettings = inputStripPanel;

            var tree = Resources.Load<VisualTreeAsset>("InputStrip");
            if (tree == null)
            {
                Debug.LogError("[HUD] Missing Resources/InputStrip.uxml.");
                return;
            }
            inputStripDoc.visualTreeAsset = tree;

            var stripRoot = inputStripDoc.rootVisualElement;
            if (stripRoot == null) return;
            stripRoot.pickingMode = PickingMode.Ignore;
            stripRoot.Query<VisualElement>().ForEach(v => v.pickingMode = PickingMode.Ignore);

            // Programmatically-created UIDocument roots don't auto-size to
            // the panel reliably: when every child uses position: absolute
            // (like our `.input-strip`), there's no flex content to drag
            // the root's layout, so it stays at 0×0 and absolute children
            // anchor to nothing — they render at negative coordinates or
            // are clipped out entirely. Forcing explicit pixel dimensions
            // matching the panel's reference resolution gives the strip a
            // real rectangle to anchor against. ScaleWithScreenSize on the
            // panel scales the rendered output to fit the actual window.
            stripRoot.style.position = Position.Absolute;
            stripRoot.style.left = 0;
            stripRoot.style.top = 0;
            stripRoot.style.width = inputStripPanel.referenceResolution.x;
            stripRoot.style.height = inputStripPanel.referenceResolution.y;

            // Same defensive stylesheet attach as BuildUIDocument: the <Style
            // src="HUD.uss"/> in InputStrip.uxml usually resolves at import,
            // but if not the .input-strip rules (including the bottom/left
            // anchoring that puts it in the corner) wouldn't apply at all.
            var stripSheet = Resources.Load<StyleSheet>("HUD");
            if (stripSheet != null && !stripRoot.styleSheets.Contains(stripSheet))
                stripRoot.styleSheets.Add(stripSheet);

            // BindElements already populated chromeBorderTargets from the main
            // document. Append the strip's outline + keycap labels so they
            // still pulse gold during Unstoppable.
            var stripEl = stripRoot.Q("input-strip");
            if (stripEl != null) chromeBorderTargets.Add(stripEl);
            stripRoot.Query<Label>().Class("ks-key").ForEach(chromeBorderTargets.Add);
        }

        // Finds or spawns the sibling MonoBehaviour that owns its own UIDocument
        // for the full-screen edge effects (gold ult ring, red hit flash).
        // Keeping it on a separate GameObject (and a separate panel) means it
        // lays out independently of NeonHUD — same reasoning that drove the
        // input strip's split. Show/Hide is mutex: only one mode renders at a
        // time. If the user has placed a ScreenBorderOverlay in the scene
        // (e.g. to tweak colors in the Inspector) we use that one and skip
        // the auto-spawn so we don't end up with two.
        private void BuildScreenBorderOverlay()
        {
            screenBorder = FindFirstObjectByType<ScreenBorderOverlay>();
            if (screenBorder != null) return;

            // Root-level GameObject for the same UIDocument-asserts-shared-
            // PanelSettings reason as InputStripDoc.
            screenBorderGO = new GameObject("ScreenBorderOverlayDoc");
            screenBorder = screenBorderGO.AddComponent<ScreenBorderOverlay>();
        }

        private void BindElements()
        {
            hpBar = root.Q<SkewedBar>("hp-bar");
            chgBar = root.Q<SkewedBar>("chg-bar");
            hpVal = root.Q<Label>("hp-val");
            hpMaxLbl = root.Q<Label>("hp-max");
            chgVal = root.Q<Label>("chg-val");
            playerPanel = root.Q("player-panel");
            playerNameLabel = root.Q<Label>("player-name");
            waveEyebrowLabel = root.Q<Label>("wave-eyebrow");
            waveNum = root.Q<Label>("wave-num");
            waveOf = root.Q<Label>("wave-of");
            wavePips = root.Q("wave-pips");
            enemyIconsContainer = root.Q("enemy-icons");
            etNow = root.Q<Label>("et-now");
            etTot = root.Q<Label>("et-tot");
            etypeMelee = root.Q<Label>("etype-melee");
            etypeRanged = root.Q<Label>("etype-ranged");
            enemyBarFill = root.Q("enemy-bar-fill");
            eventBanner = root.Q("event-banner");
            ebLine = root.Q<Label>("eb-line");
            ebSub = root.Q<Label>("eb-sub");
            comboPanel = root.Q("combo-panel");
            comboNum = root.Q<Label>("combo-num");
            abilityCharge = root.Q("ability-charge");
            abilityDodge = root.Q("ability-dodge");
            ultBar = root.Q<SkewedBar>("ult-bar");
            ultVal = root.Q<Label>("ult-val");
            abilityUlt = root.Q("ability-ult");
            ultBarRow = root.Q("ult-row");
            enemyTracker = root.Q("enemy-tracker");

            // Every existing HUD outline we want to pulse during Ultimate.
            // Skipping .player-panel and .wave-panel intentionally — they have
            // no native outline; adding one creates a "box around the group"
            // rather than highlighting an existing chrome element. The input
            // strip + its .ks-key labels live in a separate UIDocument now;
            // BuildInputStripDocument appends them to this same list.
            chromeBorderTargets.Clear();
            if (enemyTracker != null) chromeBorderTargets.Add(enemyTracker);
            root.Query<VisualElement>().Class("ability").ForEach(chromeBorderTargets.Add);
            // .ab-key.passive has its border deliberately zeroed; skip it so we
            // don't paint a border on what's meant to be a frameless badge.
            root.Query<Label>().Class("ab-key").ForEach(e =>
            {
                if (!e.ClassListContains("passive")) chromeBorderTargets.Add(e);
            });
        }

        private void PaintAccent()
        {
            hpFillStart = accent;
            hpFillEnd = Color.Lerp(accent, new Color(1f, 0.5f, 0.2f, 1f), 0.6f);

            if (hpBar != null)
            {
                hpBar.FillStart = hpFillStart;
                hpBar.FillEnd = hpFillEnd;
                hpBar.TrailColor = new Color(1f, 1f, 1f, 0.55f);
                hpBar.GlowColor = new Color(accent.r, accent.g, accent.b, 0.4f);
            }

            chgFillStart = new Color(0.12f, 0.53f, 0.78f, 1f);
            chgFillEnd = new Color(0.73f, 0.98f, 1f, 1f);
            if (chgBar != null)
            {
                chgBar.FillStart = chgFillStart;
                chgBar.FillEnd = chgFillEnd;
                chgBar.TrailColor = new Color(0, 0, 0, 0);
                chgBar.GlowColor = new Color(0.26f, 0.9f, 1f, 0.5f);
            }

            // Hot amber/gold — deliberately hotter than the #ffcf4d already used by
            // ranged-enemy / combo / victory tokens so the player's ult reads as its
            // own thing instead of rhyming with hostile UI.
            ultFillStart = new Color(1f, 0.69f, 0f, 1f);
            ultFillEnd = new Color(1f, 0.89f, 0.54f, 1f);
            ultGlow = new Color(1f, 0.59f, 0.12f, 0.6f);
            ultActiveStart = new Color(1f, 1f, 1f, 1f);
            ultActiveEnd = new Color(1f, 0.84f, 0.42f, 1f);
            if (ultBar != null)
            {
                ultBar.FillStart = ultFillStart;
                ultBar.FillEnd = ultFillEnd;
                ultBar.TrailColor = new Color(0, 0, 0, 0);
                ultBar.GlowColor = ultGlow;
            }

            foreach (var p in pips) p.Accent = accent;
        }

        private void Update()
        {
            if (root == null) return;
            TryBindPlayer();
            float dt = Time.unscaledDeltaTime;
            currentPipPhase += dt;
            UpdatePlayerStats(dt);
            UpdateAbilities(dt);
            UpdateWave();
            UpdateEnemyTracker();
            UpdateShake(dt);
            UpdateHitFlash(dt);
            UpdateScreenBorder();
        }

        private void TryBindPlayer()
        {
            if (!subscribedGameManager && GameManager.Instance != null)
            {
                GameManager.Instance.PlayerDied += OnPlayerDied;
                GameManager.Instance.GameWon += OnGameWon;
                subscribedGameManager = true;
            }

            if (playerHealth != null || GameManager.Instance == null) return;

            var h = GameManager.Instance.PlayerHealth;
            if (h == null) return;

            playerHealth = h;
            playerHealth.Damaged += OnPlayerDamaged;
            hpDisplay = playerHealth.Normalized;
            hpTrailDisplay = hpDisplay;

            var player = GameManager.Instance.Player;
            if (player != null)
            {
                combo = player.GetComponentInChildren<MeleeComboCoordinator>();
                chargeDash = player.GetComponentInChildren<ChargeDashAbility>();
                dodge = player.GetComponentInChildren<DodgeAbility>();
                unstoppable = player.GetComponentInChildren<UnstoppableAbility>();
                if (combo != null) combo.ComboHit += OnComboHit;
                if (unstoppable != null)
                {
                    unstoppableActivatedHandler = OnUnstoppableActivated;
                    unstoppable.Activated += unstoppableActivatedHandler;
                }
            }
        }

        private void UnbindPlayer()
        {
            if (playerHealth != null) playerHealth.Damaged -= OnPlayerDamaged;
            if (combo != null) combo.ComboHit -= OnComboHit;
            if (unstoppable != null && unstoppableActivatedHandler != null)
                unstoppable.Activated -= unstoppableActivatedHandler;
            playerHealth = null;
            combo = null;
            chargeDash = null;
            dodge = null;
            unstoppable = null;
            unstoppableActivatedHandler = null;
            ultDisplay = 0f;
        }

        private void UpdatePlayerStats(float dt)
        {
            float target = playerHealth != null ? playerHealth.Normalized : 0f;
            float max = playerHealth != null ? playerHealth.Max : 0f;
            float cur = playerHealth != null ? playerHealth.Current : 0f;

            // Main fill catches up briskly (CSS used ~0.4s ease-out); trail lingers behind.
            hpDisplay = Mathf.MoveTowards(hpDisplay, target, dt / 0.4f);
            if (target < hpTrailDisplay)
                hpTrailDisplay = Mathf.MoveTowards(hpTrailDisplay, target, dt / trailLag);
            else
                hpTrailDisplay = Mathf.MoveTowards(hpTrailDisplay, target, dt / 0.4f);

            if (hpBar != null)
            {
                hpBar.Progress = hpDisplay;
                hpBar.Trail = hpTrailDisplay;
            }
            if (hpVal != null) hpVal.text = Pad(Mathf.RoundToInt(cur), 3);
            if (hpMaxLbl != null) hpMaxLbl.text = $"/{Mathf.RoundToInt(max)}";

            bool critical = playerHealth != null && playerHealth.IsAlive && target > 0f && target < criticalThreshold;
            if (critical != wasCritical)
            {
                wasCritical = critical;
                if (playerPanel != null)
                {
                    if (critical) playerPanel.AddToClassList("critical");
                    else playerPanel.RemoveFromClassList("critical");
                }
                if (!critical && hpBar != null)
                {
                    // Restore the base gradient when we leave critical state.
                    hpBar.FillStart = hpFillStart;
                    hpBar.FillEnd = hpFillEnd;
                }
            }

            if (critical && hpBar != null)
            {
                // Mimic the CSS hpFlash @keyframes: brightness flicker twice per second.
                criticalAccum += dt;
                float k = 0.5f + 0.5f * Mathf.Sin(criticalAccum * Mathf.PI * 4f);
                Color hot = new(1f, 0.95f, 0.55f, 1f);
                hpBar.FillStart = Color.Lerp(hpFillStart, hot, k);
                hpBar.FillEnd = Color.Lerp(hpFillEnd, hot, k);
            }
        }

        private void UpdateAbilities(float dt)
        {
            float chargePct = 0f;
            bool ready = false;

            if (chargeDash != null)
            {
                chargePct = chargeDash.CooldownProgress;
                ready = chargeDash.IsReady && !chargeDash.IsActive && !chargeDash.IsCharging;
            }

            chgDisplay = Mathf.MoveTowards(chgDisplay, chargePct, dt / 0.2f);
            if (chgBar != null) chgBar.Progress = chgDisplay;
            if (chgVal != null) chgVal.text = ready ? "READY" : $"{Mathf.RoundToInt(chgDisplay * 100f)}%";

            if (abilityCharge != null)
                ToggleClass(abilityCharge, "lit", ready);

            if (abilityDodge != null)
            {
                bool dr = dodge != null && dodge.IsReady && !dodge.IsActive;
                ToggleClass(abilityDodge, "lit-accent", dr);
            }

            if (chgBar != null)
            {
                if (ready)
                {
                    // Bar shimmer when charge is locked in — replaces the CSS chgShift
                    // keyframes (USS has no looping animation primitive).
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f);
                    chgBar.FillStart = Color.Lerp(new Color(0.26f, 0.9f, 1f, 1f), Color.white, k * 0.6f);
                    chgBar.FillEnd = Color.Lerp(new Color(0.73f, 0.98f, 1f, 1f), Color.white, k * 0.3f);
                }
                else if (chgBar.FillStart != chgFillStart)
                {
                    chgBar.FillStart = chgFillStart;
                    chgBar.FillEnd = chgFillEnd;
                }
            }

            UpdateUltimate(dt);
        }

        private void UpdateUltimate(float dt)
        {
            float ultPct = 0f;
            bool ultActive = false;
            float ultSecondsLeft = 0f;
            float chargeNorm = 0f;
            if (unstoppable != null)
            {
                ultActive = unstoppable.IsUnstoppable;
                chargeNorm = unstoppable.ChargeNormalized;
                ultSecondsLeft = unstoppable.UnstoppableSecondsLeft;
                ultPct = ultActive ? unstoppable.UnstoppableNormalized : chargeNorm;
            }

            ultDisplay = Mathf.MoveTowards(ultDisplay, ultPct, dt / 0.2f);

            if (ultBar != null)
            {
                ultBar.Progress = ultDisplay;
                if (ultActive)
                {
                    // Inverted gradient + brightness pulse; 8 Hz in the final second to
                    // telegraph "about to end".
                    float hz = ultSecondsLeft <= 1f ? 8f : 6f;
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * hz);
                    ultBar.FillStart = Color.Lerp(ultActiveStart, Color.white, k * 0.3f);
                    ultBar.FillEnd = Color.Lerp(ultActiveEnd, Color.white, k * 0.3f);
                    ultBar.GlowColor = new Color(1f, 0.84f, 0.2f, 0.7f);
                }
                else if (ultBar.FillStart != ultFillStart)
                {
                    ultBar.FillStart = ultFillStart;
                    ultBar.FillEnd = ultFillEnd;
                    ultBar.GlowColor = ultGlow;
                }
            }

            if (ultVal != null)
            {
                // ⛨ shield glyph carries the ACTIVE meaning so the state reads even
                // without colour (color-not-only accessibility).
                ultVal.text = ultActive
                    ? $"⛨ {ultSecondsLeft:0.0}"
                    : $"{Mathf.RoundToInt(ultDisplay * 100f)}%";
            }

            if (ultBarRow != null)
                ToggleClass(ultBarRow, "ult-arming", !ultActive && chargeNorm >= 0.8f);

            if (abilityUlt != null)
                ToggleClass(abilityUlt, "lit-ult", ultActive);

            UpdateUltimateFrame(ultActive, ultSecondsLeft);
        }

        // Pulses the screen-edge frame and major HUD panels in gold while
        // Unstoppable is active. Locked to the same 6/8 Hz curve the bar uses
        // so the whole HUD reads as one coherent state.
        private void UpdateUltimateFrame(bool ultActive, float ultSecondsLeft)
        {
            // Manual fade-in (250ms) / fade-out (150ms — exit-faster-than-enter,
            // Animation §7). USS transitions are intentionally NOT used here so
            // the 6/8 Hz pulse stays crisp instead of smearing.
            float dt = Time.unscaledDeltaTime;
            float target = ultActive ? 1f : 0f;
            float rate = ultActive ? dt / 0.25f : dt / 0.15f;
            ultFrameVisibility = Mathf.MoveTowards(ultFrameVisibility, target, rate);

            float pulse;
            if (ultActive)
            {
                float hz = ultSecondsLeft <= 1f ? 8f : 6f;
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * hz);
                // Floor at 0.55 so the frame never blinks fully dark — that
                // would read as "broken / flickering" rather than "pulsing".
                pulse = 0.55f + 0.45f * k;
            }
            else
            {
                pulse = 0.55f;
            }
            float alpha = ultFrameVisibility * pulse;

            // Hand the computed alpha to UpdateScreenBorder via a field; it
            // decides each frame whether to show ult or hit (mutex). The
            // screen-edge frame itself lives on ScreenBorderOverlay.
            currentUltAlpha = alpha;

            // Pulse the existing chrome outlines (enemy tracker, input strip,
            // ability slots, keycaps). When the fade-out fully completes we
            // clear the inline border colours so each element reverts to its
            // USS-defined idle/lit state — no need to cache original colours.
            if (ultFrameVisibility <= 0.001f)
            {
                if (!chromeBordersCleared)
                {
                    foreach (var ve in chromeBorderTargets) ClearBorderColor(ve);
                    chromeBordersCleared = true;
                }
            }
            else
            {
                Color chromeGold = new(1f, 0.84f, 0.42f, alpha);
                foreach (var ve in chromeBorderTargets) ApplyBorderColor(ve, chromeGold);
                chromeBordersCleared = false;
            }
        }

        private static void ApplyBorderColor(VisualElement ve, Color c)
        {
            ve.style.borderLeftColor = c;
            ve.style.borderRightColor = c;
            ve.style.borderTopColor = c;
            ve.style.borderBottomColor = c;
        }

        private static void ClearBorderColor(VisualElement ve)
        {
            ve.style.borderLeftColor = StyleKeyword.Null;
            ve.style.borderRightColor = StyleKeyword.Null;
            ve.style.borderTopColor = StyleKeyword.Null;
            ve.style.borderBottomColor = StyleKeyword.Null;
        }

        private void OnUnstoppableActivated()
        {
            ShowBanner("UNSTOPPABLE", "NO PAIN · 5s", 1.0f, "victory");
        }

        private void UpdateWave()
        {
            if (spawner == null) return;
            int idx = Mathf.Max(0, spawner.WaveIndex);
            int total = spawner.WaveCount;
            if (waveNum != null) waveNum.text = Pad(Mathf.Clamp(idx + 1, 1, Mathf.Max(1, total)), 2);
            if (waveOf != null) waveOf.text = $"/{Pad(total, 2)}";

            if (idx != lastWaveIndex || total != lastWaveCount)
            {
                RebuildPips(idx, total);
                lastWaveIndex = idx;
                lastWaveCount = total;
            }

            if (pips.Count > 0)
            {
                int curIdx = Mathf.Clamp(idx, 0, pips.Count - 1);
                pips[curIdx].Pulse = currentPipPhase;
            }
        }

        private void RebuildPips(int currentIdx, int waveCount)
        {
            if (wavePips == null) return;
            wavePips.Clear();
            pips.Clear();
            if (waveCount <= 0) return;

            for (int i = 0; i < waveCount; i++)
            {
                bool isBoss = i == waveCount - 1 && waveCount > 1;
                var pip = new Pip
                {
                    Boss = isBoss,
                    Cleared = i < currentIdx,
                    Current = i == currentIdx,
                    Accent = accent,
                };
                pip.AddToClassList("pip");
                if (isBoss) pip.AddToClassList("boss");
                wavePips.Add(pip);
                pips.Add(pip);
            }
        }

        private void UpdateEnemyTracker()
        {
            if (spawner == null) return;
            int alive = spawner.AliveEnemies;
            int total = spawner.CurrentWaveTotal;
            int killed = Mathf.Max(0, spawner.EnemiesKilledThisWave);

            if (etNow != null) etNow.text = Pad(alive, 2);
            if (etTot != null) etTot.text = Pad(total, 2);

            if (enemyBarFill != null)
            {
                float pct = total > 0 ? (float)killed / total : 0f;
                if (!Mathf.Approximately(pct, lastEnemyBarPct))
                {
                    enemyBarFill.style.width = new Length(pct * 100f, LengthUnit.Percent);
                    lastEnemyBarPct = pct;
                }
            }

            int meleeCount = 0, rangedCount = 0;
            var wave = spawner.CurrentWave;
            if (wave != null)
            {
                foreach (var e in wave.entries)
                {
                    if (e.kind == EnemyKind.Melee) meleeCount += e.count;
                    else rangedCount += e.count;
                }
            }
            if (etypeMelee != null) etypeMelee.text = $"× {meleeCount} MELEE";
            if (etypeRanged != null) etypeRanged.text = $"× {rangedCount} RANGED";

            RebuildEnemyIcons(total, meleeCount, spawner.MeleeKilledThisWave, spawner.RangedKilledThisWave);
        }

        private void RebuildEnemyIcons(int total, int meleeCount, int meleeKilled, int rangedKilled)
        {
            if (enemyIconsContainer == null) return;
            if (total != lastEnemyTotal)
            {
                enemyIconsContainer.Clear();
                enemyIcons.Clear();
                for (int i = 0; i < total; i++)
                {
                    var icon = new VisualElement();
                    icon.AddToClassList("enemy-icon");
                    bool ranged = i >= meleeCount;
                    if (ranged) icon.AddToClassList("ranged");
                    icon.pickingMode = PickingMode.Ignore;
                    icon.Add(new Label(ranged ? "►" : "▼"));
                    enemyIconsContainer.Add(icon);
                    enemyIcons.Add(icon);
                }
                lastEnemyTotal = total;
            }
            // Icons are laid out melee-first (indices 0..meleeCount-1) then ranged
            // (meleeCount..total-1). Mark only the first N icons of each kind as down
            // so killing a ranged enemy doesn't grey out a melee slot.
            for (int i = 0; i < enemyIcons.Count; i++)
            {
                bool ranged = i >= meleeCount;
                int idxWithinType = ranged ? i - meleeCount : i;
                int killedOfType = ranged ? rangedKilled : meleeKilled;
                ToggleClass(enemyIcons[i], "down", idxWithinType < killedOfType);
            }
        }

        private void UpdateShake(float dt)
        {
            if (shakeTimeLeft <= 0f)
            {
                if (shakeApplied)
                {
                    root.style.translate = new Translate(0f, 0f, 0f);
                    shakeApplied = false;
                }
                return;
            }

            shakeTimeLeft = Mathf.Max(0f, shakeTimeLeft - dt);
            float t = shakeDuration > 0f ? shakeTimeLeft / shakeDuration : 0f;
            float decay = t * t;
            float x = (Random.value - 0.5f) * 2f * shakeMagnitude * decay;
            float y = (Random.value - 0.5f) * 2f * shakeMagnitude * decay;
            root.style.translate = new Translate(x, y, 0f);
            shakeApplied = true;
        }

        private void UpdateHitFlash(float dt)
        {
            if (hitFlashTimeLeft <= 0f)
            {
                currentHitAlpha = 0f;
                return;
            }
            hitFlashTimeLeft = Mathf.Max(0f, hitFlashTimeLeft - dt);
            // t^2 falloff: bright snap at impact, smooth tail. Mirrors the
            // shake's decay curve so the two effects feel synchronised.
            float t = hitFlashDuration > 0f ? hitFlashTimeLeft / hitFlashDuration : 0f;
            currentHitAlpha = hitFlashPeakAlpha * t * t;
        }

        // Mutex driver for the ScreenBorderOverlay. Ultimate wins when both
        // would otherwise show — the player is invulnerable during ult, so a
        // hit-flash shouldn't even fire concurrently, but if a stray damage
        // event slips through we'd rather show the bigger event uninterrupted.
        private void UpdateScreenBorder()
        {
            if (screenBorder == null) return;
            if (currentUltAlpha > 0.001f)
                screenBorder.Show(ScreenBorderOverlay.Mode.Ultimate, currentUltAlpha);
            else if (currentHitAlpha > 0.001f)
                screenBorder.Show(ScreenBorderOverlay.Mode.Hit, currentHitAlpha);
            else
                screenBorder.Hide();
        }

        // ────────────────────────── events ──────────────────────────

        private void OnPlayerDamaged(DamageInfo info)
        {
            shakeTimeLeft = shakeDuration;
            hitFlashTimeLeft = hitFlashDuration;
            if (playerPanel == null) return;
            // Brief panel jolt — the controller toggles a class the USS hooks via
            // transition. The class itself doesn't define translation explicitly here
            // because USS lacks @keyframes; the visual cue is the critical-class shift
            // when HP gets low.
            playerPanel.AddToClassList("hit");
            root.schedule.Execute(() =>
            {
                if (playerPanel != null) playerPanel.RemoveFromClassList("hit");
            }).StartingIn(120);
        }

        private void OnWaveStarted(int index)
        {
            // Cancel any followup that was queued for a previous wave.
            followupShowTask?.Pause();

            ShowBanner($"WAVE {Pad(index + 1, 2)} INCOMING", "PREPARE YOURSELF", 1.5f, null);
            followupShowTask = root.schedule.Execute(() =>
                ShowBanner("FIGHT!", "BREAK THEM", 0.9f, null)).StartingIn(1500);
        }

        private void OnWaveCleared(int index)
        {
            if (spawner != null && index < spawner.WaveCount - 1)
                ShowBanner("WAVE CLEAR", "REGROUP — NEXT WAVE INBOUND", 1.4f, null);
        }

        private void OnAllWavesCleared()
        {
            // Handled by GameManager.GameWon as well; either path triggers the banner.
            ShowBanner("VICTORY", "ALL WAVES CLEARED", 3f, "victory");
        }

        private void OnGameWon()
        {
            if (koShown) return; // K.O. already showing — let it stand.
            ShowBanner("VICTORY", "ALL WAVES CLEARED", 3f, "victory");
        }

        private void OnPlayerDied()
        {
            koShown = true;
            ShowBanner("K.O.", "RESTARTING…", 3f, "ko");
        }

        private void OnComboHit(int step)
        {
            if (comboPanel == null || comboNum == null) return;
            comboNum.text = step >= 2 ? "2" : "1";
            comboPanel.AddToClassList("visible");
            comboHideTask?.Pause();
            comboHideTask = root.schedule.Execute(() =>
            {
                if (comboPanel != null) comboPanel.RemoveFromClassList("visible");
            }).StartingIn(step >= 2 ? 1100 : 700);
        }

        private void ShowBanner(string main, string sub, float seconds, string variant)
        {
            if (eventBanner == null) return;
            if (ebLine != null) ebLine.text = main;
            if (ebSub != null) ebSub.text = sub;
            eventBanner.RemoveFromClassList("victory");
            eventBanner.RemoveFromClassList("ko");
            if (!string.IsNullOrEmpty(variant)) eventBanner.AddToClassList(variant);
            eventBanner.AddToClassList("visible");

            bannerHideTask?.Pause();
            bannerHideTask = root.schedule.Execute(() =>
            {
                if (eventBanner != null) eventBanner.RemoveFromClassList("visible");
            }).StartingIn((long)(seconds * 1000));
        }

        private static void ToggleClass(VisualElement ve, string className, bool on)
        {
            if (on) ve.AddToClassList(className);
            else ve.RemoveFromClassList(className);
        }

        private static string Pad(int n, int width) => n.ToString("D" + width);
    }
}
