using System.Collections.Generic;
using HackSlash.Core;
using UnityEngine;

namespace HackSlash.Abilities
{
    /// <summary>
    /// Hold-to-charge ability: while charging, a sprite flashes; on threshold, the owner
    /// dashes a fixed distance with i-frames and damages each enemy crossed exactly once.
    /// Reads faction from the owner so a player and an enemy could both wield it.
    /// </summary>
    public class ChargeDashAbility : Ability
    {
        [SerializeField] private float chargeDuration = 0.5f;
        [SerializeField] private float baseDashSpeed = 14f;
        [SerializeField] private float baseDashDuration = 0.25f;
        [SerializeField] private float distanceMultiplier = 1.5f;
        [SerializeField] private float speedMultiplier = 2f;
        [SerializeField] private Color flashColor = new Color(1f, 0.45f, 0.15f, 1f);
        [SerializeField] private float flashHz = 8f;
        [SerializeField] private float damage = 25f;
        [SerializeField] private float knockback = 6f;
        [SerializeField] private Vector2 hitSize = new Vector2(1.6f, 1.8f);
        [SerializeField] private LayerMask targetMask = ~0;

        [Header("Refs")]
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Health health;
        [SerializeField] private SpriteRenderer flashSprite;

        private Color baseColor = Color.white;
        private bool charging;
        private float chargeStartedAt;
        private int dashDir = 1;
        private readonly HashSet<IDamageable> hits = new();
        private readonly Collider2D[] buffer = new Collider2D[16];

        public bool IsCharging => charging;
        public bool IsDashing => IsActive;
        public override bool IsLocked => IsCharging || IsActive;

        protected override void Awake()
        {
            base.Awake();
            if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
            if (health == null) health = GetComponentInParent<Health>();
            if (flashSprite == null) flashSprite = GetComponentInChildren<SpriteRenderer>();
            if (flashSprite != null) baseColor = flashSprite.color;
        }

        public void BeginCharge()
        {
            if (!IsReady || IsActive || charging) return;
            charging = true;
            chargeStartedAt = Time.time;
        }

        public void CancelCharge()
        {
            if (!charging) return;
            charging = false;
            RestoreSpriteColor();
        }

        protected override void OnActivate()
        {
            charging = false;
            RestoreSpriteColor();
            hits.Clear();
            dashDir = owner != null ? owner.Facing : 1;
            float dashSpeed = baseDashSpeed * speedMultiplier;
            float distance = baseDashSpeed * baseDashDuration * distanceMultiplier;
            float duration = distance / Mathf.Max(0.01f, dashSpeed);
            SetActive(duration);
            StartCooldown();
            if (health != null) health.SetInvulnerableFor(duration);
            if (rb != null) rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
        }

        private void Update()
        {
            if (!charging) return;

            float held = Time.time - chargeStartedAt;
            if (held >= chargeDuration)
            {
                TryActivate();
                return;
            }

            if (flashSprite != null)
            {
                float t = (Mathf.Sin(held * flashHz * Mathf.PI * 2f) + 1f) * 0.5f;
                flashSprite.color = Color.Lerp(baseColor, flashColor, t);
            }
        }

        private void FixedUpdate()
        {
            if (!IsActive || rb == null) return;
            float dashSpeed = baseDashSpeed * speedMultiplier;
            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
            ScanDashHits();
        }

        private void ScanDashHits()
        {
            Vector2 center = rb.position;
            Faction self = owner != null ? owner.Faction : Faction.Player;
            int count = Physics2D.OverlapBoxNonAlloc(center, hitSize, 0f, buffer, targetMask);
            for (int i = 0; i < count; i++)
            {
                var col = buffer[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null) continue;
                if (dmg.Faction == self) continue;
                if (!dmg.IsAlive) continue;
                if (!hits.Add(dmg)) continue;

                Vector2 dir = new Vector2(dashDir, 0f);
                dmg.TakeDamage(new DamageInfo
                {
                    Amount = damage,
                    Source = self,
                    Origin = center,
                    Knockback = dir * knockback
                });
            }
        }

        private void RestoreSpriteColor()
        {
            if (flashSprite != null) flashSprite.color = baseColor;
        }

        public override void Cancel()
        {
            CancelCharge();
            base.Cancel();
        }
    }
}
