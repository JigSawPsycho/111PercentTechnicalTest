using System.Collections;
using HackSlash.Abilities;
using HackSlash.Core;
using HackSlash.Waves;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HackSlash.UI
{
    public class HUD : MonoBehaviour
    {
        // ── Existing serialized fields – keep names so scene wiring stays intact ──
        [SerializeField] private Slider healthBar;
        [SerializeField] private Text waveLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private WaveSpawner spawner;

        // ── Health bar extras ──
        [Header("Health Bar")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Image healthTrail;
        [SerializeField] private float trailLag = 1.2f;

        // ── TMP banner (large centred overlay – "FIGHT!", "K.O.", etc.) ──
        [Header("Banner")]
        [SerializeField] private TextMeshProUGUI bannerText;

        // ── Combo popup (top-left or near health bar) ──
        [Header("Combo Popup")]
        [SerializeField] private TextMeshProUGUI comboText;
        [SerializeField] private MeleeComboCoordinator comboCoordinator;

        // ── Colors ──
        private static readonly Color HealthHigh   = new Color(0.18f, 0.85f, 0.22f);
        private static readonly Color HealthMid    = new Color(0.95f, 0.78f, 0.08f);
        private static readonly Color HealthLow    = new Color(0.90f, 0.15f, 0.12f);

        private static readonly Color BannerReady  = new Color(1.00f, 0.90f, 0.10f);
        private static readonly Color BannerFight  = new Color(0.20f, 1.00f, 0.25f);
        private static readonly Color BannerClear  = new Color(0.20f, 0.80f, 1.00f);
        private static readonly Color BannerVictory= new Color(1.00f, 0.85f, 0.10f);

        // ── Runtime state ──
        private Health playerHealth;
        private float trailValue = 1f;
        private float healthShakeTimer;
        private Vector2 healthBarBasePos;
        private RectTransform healthBarRect;
        private Coroutine bannerRoutine;
        private Coroutine comboRoutine;

        // ──────────────────────────────────────────────────────────────────────────

        private void Start()
        {
            if (GameManager.Instance != null)
                BindPlayerHealth(GameManager.Instance.PlayerHealth);

            if (healthBar != null)
            {
                healthBarRect = healthBar.GetComponent<RectTransform>();
                if (healthBarRect != null) healthBarBasePos = healthBarRect.anchoredPosition;
            }

            if (trailValue > 0f && healthTrail != null)
                healthTrail.fillAmount = 1f;

            if (bannerText != null)
            {
                bannerText.text = string.Empty;
                bannerText.alpha = 0f;
            }

            if (comboText != null)
            {
                comboText.text = string.Empty;
                comboText.alpha = 0f;
            }

            if (spawner != null)
            {
                spawner.WaveStarted  += OnWaveStarted;
                spawner.WaveCleared  += OnWaveCleared;
                spawner.AllWavesCleared += OnAllWavesCleared;
            }

            if (comboCoordinator != null)
                comboCoordinator.ComboHit += OnComboHit;
        }

        private void OnDestroy()
        {
            if (spawner != null)
            {
                spawner.WaveStarted     -= OnWaveStarted;
                spawner.WaveCleared     -= OnWaveCleared;
                spawner.AllWavesCleared -= OnAllWavesCleared;
            }

            if (comboCoordinator != null)
                comboCoordinator.ComboHit -= OnComboHit;

            UnbindPlayerHealth();
        }

        private void BindPlayerHealth(Health h)
        {
            if (h == null) return;
            playerHealth = h;
            playerHealth.Damaged += OnPlayerDamaged;
        }

        private void UnbindPlayerHealth()
        {
            if (playerHealth != null)
                playerHealth.Damaged -= OnPlayerDamaged;
            playerHealth = null;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Update
        // ──────────────────────────────────────────────────────────────────────────

        private void Update()
        {
            // Late-bind player health if GameManager registers the player after Start.
            if (playerHealth == null && GameManager.Instance != null)
                BindPlayerHealth(GameManager.Instance.PlayerHealth);

            UpdateHealthBar();
            UpdateWaveLabel();
            UpdateStatusLabel();
            UpdateHealthBarShake();
        }

        private void UpdateHealthBar()
        {
            if (healthBar == null || playerHealth == null) return;

            float target = playerHealth.Normalized;
            healthBar.value = target;

            if (healthFill != null)
                healthFill.color = HealthColor(target);

            if (healthTrail != null)
            {
                trailValue = Mathf.MoveTowards(trailValue, target, Time.deltaTime / trailLag);
                healthTrail.fillAmount = trailValue;
            }
        }

        private void UpdateWaveLabel()
        {
            if (waveLabel == null || spawner == null) return;

            int idx = Mathf.Clamp(spawner.WaveIndex + 1, 1, Mathf.Max(1, spawner.WaveCount));
            string roundTag = (idx >= spawner.WaveCount && spawner.WaveCount > 1)
                ? "FINAL ROUND"
                : $"ROUND {idx}/{spawner.WaveCount}";
            int foes = spawner.AliveEnemies;
            string foeTag = foes <= 0
                ? "STAGE CLEARING…"
                : (foes == 1 ? "1 FOE LEFT — FINISH HIM!" : $"{foes} FOES INCOMING");
            waveLabel.text = $"◆ {roundTag} ◆   {foeTag}";
        }

        private void UpdateStatusLabel()
        {
            if (statusLabel == null) return;
            if (playerHealth != null && !playerHealth.IsAlive)
                statusLabel.text = "!! K.O. !!\nINSERT COIN…";
            else if (spawner != null && spawner.AllWavesDone)
                statusLabel.text = "★ YOU WIN! ★\nSTAGE CLEAR — NEXT BATTLE…";
            else
                statusLabel.text = string.Empty;
        }

        private void UpdateHealthBarShake()
        {
            if (healthBarRect == null || healthShakeTimer <= 0f) return;
            healthShakeTimer -= Time.deltaTime;
            if (healthShakeTimer <= 0f)
            {
                healthBarRect.anchoredPosition = healthBarBasePos;
            }
            else
            {
                float mag = 5f * (healthShakeTimer / 0.35f);
                healthBarRect.anchoredPosition = healthBarBasePos + Random.insideUnitCircle * mag;
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Event handlers
        // ──────────────────────────────────────────────────────────────────────────

        private void OnPlayerDamaged(DamageInfo info)
        {
            healthShakeTimer = 0.35f;
        }

        private void OnWaveStarted(int index)
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(ReadyFightSequence());
        }

        private void OnWaveCleared(int index)
        {
            if (spawner != null && index < spawner.WaveCount - 1)
                ShowBanner("WAVE COMPLETE!", BannerClear, 1.4f);
        }

        private void OnAllWavesCleared()
        {
            ShowBanner("VICTORY!", BannerVictory, 3f);
        }

        private void OnComboHit(int step)
        {
            if (comboText == null) return;
            string msg = step >= 2 ? "x2 COMBO!" : "HIT!";
            if (comboRoutine != null) StopCoroutine(comboRoutine);
            comboRoutine = StartCoroutine(PopupRoutine(comboText, msg, Color.white, 0.9f));
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Banner coroutines
        // ──────────────────────────────────────────────────────────────────────────

        private void ShowBanner(string msg, Color color, float hold)
        {
            if (bannerText == null) return;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(msg, color, hold));
        }

        private IEnumerator ReadyFightSequence()
        {
            if (bannerText == null) yield break;
            yield return BannerRoutine("READY?", BannerReady, 0.7f);
            yield return BannerRoutine("FIGHT!", BannerFight, 0.6f);
        }

        /// <summary>
        /// Scale-punch in, hold, then fade out. Non-allocating; reuses the same TMP element.
        /// </summary>
        private IEnumerator BannerRoutine(string msg, Color color, float hold)
        {
            if (bannerText == null) yield break;

            bannerText.text = msg;
            bannerText.color = color;
            bannerText.alpha = 1f;

            // Punch scale in from 0 → overshoot → settle
            float punchDuration = 0.18f;
            float t = 0f;
            while (t < punchDuration)
            {
                t += Time.deltaTime;
                float n = t / punchDuration;
                float scale = PunchCurve(n);
                bannerText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            bannerText.transform.localScale = Vector3.one;

            yield return new WaitForSeconds(hold);

            // Fade out
            float fadeTime = 0.25f;
            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                bannerText.alpha = 1f - (t / fadeTime);
                yield return null;
            }

            bannerText.alpha = 0f;
            bannerText.text = string.Empty;
        }

        /// <summary>
        /// Small popup with punch-in and quick fade for combo/hit text.
        /// </summary>
        private IEnumerator PopupRoutine(TextMeshProUGUI label, string msg, Color color, float hold)
        {
            label.text = msg;
            label.color = color;
            label.alpha = 1f;

            float punchDuration = 0.12f;
            float t = 0f;
            while (t < punchDuration)
            {
                t += Time.deltaTime;
                float scale = PunchCurve(t / punchDuration);
                label.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            label.transform.localScale = Vector3.one;

            yield return new WaitForSeconds(hold);

            float fadeTime = 0.2f;
            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                label.alpha = 1f - (t / fadeTime);
                yield return null;
            }

            label.alpha = 0f;
            label.text = string.Empty;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────────

        private static Color HealthColor(float normalized)
        {
            if (normalized > 0.5f)
                return Color.Lerp(HealthMid, HealthHigh, (normalized - 0.5f) * 2f);
            return Color.Lerp(HealthLow, HealthMid, normalized * 2f);
        }

        /// <summary>
        /// Elastic overshoot curve: 0→1.25→1 over normalized time 0..1.
        /// </summary>
        private static float PunchCurve(float n)
        {
            // Simple hand-crafted spring: overshoot at ~70% then settle.
            return 1f + 0.25f * Mathf.Sin(n * Mathf.PI) * (1f - n);
        }
    }
}
