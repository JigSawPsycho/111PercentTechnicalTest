using System;
using UnityEngine;

namespace HackSlash.Core
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private Faction faction = Faction.Enemy;
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float invulnerabilitySeconds = 0.15f;

        private float current;
        private float invulnerableUntil;
        private bool invulnerable;
        private float overrideUntil;

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        public Faction Faction => faction;
        public float Max => maxHealth;
        public float Current => current;
        public float Normalized => maxHealth > 0f ? current / maxHealth : 0f;
        public bool IsAlive => current > 0f;

        public bool InvulnerableOverride
        {
            get => invulnerable;
            set => invulnerable = value;
        }

        private void Awake()
        {
            current = maxHealth;
        }

        /// <summary>
        /// Grant invulnerability for a duration. Stacks by taking the longest window
        /// so overlapping abilities (e.g. dodge into charge-dash) don't shorten i-frames.
        /// </summary>
        public void SetInvulnerableFor(float seconds)
        {
            overrideUntil = Mathf.Max(overrideUntil, Time.time + seconds);
            invulnerable = true;
        }

        private void Update()
        {
            if (invulnerable && overrideUntil > 0f && Time.time >= overrideUntil)
            {
                invulnerable = false;
                overrideUntil = 0f;
            }
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;
            if (info.Source == faction) return;
            if (invulnerable) return;
            if (Time.time < invulnerableUntil) return;

            current = Mathf.Max(0f, current - info.Amount);
            invulnerableUntil = Time.time + invulnerabilitySeconds;
            Damaged?.Invoke(info);

            if (current <= 0f)
                Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            current = Mathf.Min(maxHealth, current + amount);
        }
    }
}
