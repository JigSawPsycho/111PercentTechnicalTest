using System;
using HackSlash.Core;
using UnityEngine;

namespace HackSlash.Abilities
{
    /// <summary>
    /// Passive ultimate: every successful player hit on an enemy adds charge.
    /// At threshold, auto-activates a damage-immune "Unstoppable" window during
    /// which the player sprite tints gold and a halo sprite pulses behind it.
    /// </summary>
    public class UnstoppableAbility : Ability
    {
        [Header("Charge")]
        [SerializeField] private float chargePerHit = 10f;
        [SerializeField] private float maxCharge = 100f;
        [SerializeField] private float unstoppableDuration = 5f;
        [SerializeField, Min(0f)] private float decayIdleSeconds = 2f;
        [SerializeField, Min(0f)] private float decayPerSecond = 2f;

        [Header("Tint")]
        [SerializeField] private Color goldColor = new Color(1f, 0.84f, 0.42f, 1f);
        [SerializeField, Range(0f, 1f)] private float tintAmount = 0.5f;
        [SerializeField, Range(0f, 1f)] private float haloMaxAlpha = 0.7f;
        [SerializeField, Range(0f, 1f)] private float haloMinAlpha = 0.3f;
        [SerializeField, Min(0.1f)] private float tintPulseHz = 3f;
        [SerializeField, Min(0.1f)] private float endgamePulseHz = 8f;
        [SerializeField, Min(0f)] private float endgameThresholdSeconds = 1f;

        [Header("Refs")]
        [SerializeField] private Health health;
        [SerializeField] private SpriteRenderer bodySprite;

        private SpriteRenderer halo;
        private Color baseSpriteColor = Color.white;
        private float charge;
        private float lastHitTime = float.NegativeInfinity;
        private bool wasActive;

        public float ChargeNormalized => maxCharge > 0f ? Mathf.Clamp01(charge / maxCharge) : 0f;
        public bool IsUnstoppable => IsActive;
        public float UnstoppableSecondsLeft => Mathf.Max(0f, activeUntil - Time.time);
        public float UnstoppableNormalized =>
            unstoppableDuration > 0f ? Mathf.Clamp01(UnstoppableSecondsLeft / unstoppableDuration) : 0f;

        public event Action Activated;
        public event Action Ended;

        protected override void Awake()
        {
            base.Awake();
            if (health == null) health = GetComponentInParent<Health>();
            if (bodySprite == null)
            {
                // Anchor on the player's Rigidbody2D (root GO) to find the animated
                // SpriteRenderer under PlayerController.spriteRoot regardless of where
                // this component is placed in the prefab hierarchy.
                var rb = GetComponentInParent<Rigidbody2D>();
                if (rb != null) bodySprite = rb.GetComponentInChildren<SpriteRenderer>();
            }
            if (bodySprite != null)
            {
                baseSpriteColor = bodySprite.color;
                BuildHalo();
            }

            SubscribeHitSources();
        }

        private void BuildHalo()
        {
            var go = new GameObject("UltimateHalo");
            go.transform.SetParent(bodySprite.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 1.12f;
            halo = go.AddComponent<SpriteRenderer>();
            halo.sprite = bodySprite.sprite;
            halo.sharedMaterial = bodySprite.sharedMaterial;
            halo.sortingLayerID = bodySprite.sortingLayerID;
            halo.sortingOrder = bodySprite.sortingOrder - 1;
            halo.color = new Color(goldColor.r, goldColor.g, goldColor.b, 0f);
        }

        private void SubscribeHitSources()
        {
            var swings = GetComponentsInParent<MeleeSwingAbility>(true);
            foreach (var swing in swings) swing.SwingHit += OnMeleeHit;

            var dashes = GetComponentsInParent<ChargeDashAbility>(true);
            foreach (var dash in dashes) dash.DashHit += OnDashHit;
        }

        private void OnDestroy()
        {
            var swings = GetComponentsInParent<MeleeSwingAbility>(true);
            foreach (var swing in swings) swing.SwingHit -= OnMeleeHit;

            var dashes = GetComponentsInParent<ChargeDashAbility>(true);
            foreach (var dash in dashes) dash.DashHit -= OnDashHit;
        }

        private void OnMeleeHit(int hitCount)
        {
            AddCharge(hitCount);
        }

        private void OnDashHit(IDamageable _)
        {
            AddCharge(1);
        }

        private void AddCharge(int hits)
        {
            if (IsUnstoppable) return;
            if (hits <= 0) return;
            charge = Mathf.Min(maxCharge, charge + chargePerHit * hits);
            lastHitTime = Time.time;
            if (charge >= maxCharge) TryActivate();
        }

        public void ResetCharge()
        {
            charge = 0f;
            lastHitTime = Time.time;
        }

        protected override void OnActivate()
        {
            SetActive(unstoppableDuration);
            charge = 0f;
            if (health != null) health.SetInvulnerableFor(unstoppableDuration);
            wasActive = true;
            Activated?.Invoke();
        }

        private void Update()
        {
            if (IsUnstoppable)
            {
                float secsLeft = UnstoppableSecondsLeft;
                float hz = secsLeft <= endgameThresholdSeconds ? endgamePulseHz : tintPulseHz;
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * hz);

                if (bodySprite != null)
                {
                    Color target = new Color(goldColor.r, goldColor.g, goldColor.b, baseSpriteColor.a);
                    bodySprite.color = Color.Lerp(baseSpriteColor, target, tintAmount * k);
                }
                if (halo != null)
                {
                    float alpha = Mathf.Lerp(haloMinAlpha, haloMaxAlpha, k);
                    halo.color = new Color(goldColor.r, goldColor.g, goldColor.b, alpha);
                }
            }
            else if (wasActive)
            {
                if (bodySprite != null) bodySprite.color = baseSpriteColor;
                if (halo != null) halo.color = new Color(goldColor.r, goldColor.g, goldColor.b, 0f);
                wasActive = false;
                Ended?.Invoke();
            }
            else if (charge > 0f && Time.time - lastHitTime > decayIdleSeconds)
            {
                charge = Mathf.Max(0f, charge - decayPerSecond * Time.deltaTime);
            }
        }

        private void LateUpdate()
        {
            if (halo == null || bodySprite == null) return;
            // Keep the halo locked to the animator's current frame so it doesn't
            // smear behind the player when the sprite swaps.
            halo.sprite = bodySprite.sprite;
            halo.flipX = bodySprite.flipX;
            halo.flipY = bodySprite.flipY;
        }

        public override void Cancel()
        {
            if (bodySprite != null) bodySprite.color = baseSpriteColor;
            if (halo != null) halo.color = new Color(goldColor.r, goldColor.g, goldColor.b, 0f);
            base.Cancel();
        }
    }
}
