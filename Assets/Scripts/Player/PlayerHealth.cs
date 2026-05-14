using HackSlash.Core;
using UnityEngine;

namespace HackSlash.Player
{
    [RequireComponent(typeof(Health))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string hurtTrigger = "Hurt";
        [SerializeField] private string deadBool = "Dead";

        private Health health;

        public Health Health => health;
        public bool IsAlive => health != null && health.IsAlive;
        public bool CanAct => IsAlive && !IsInHitstun;
        public bool IsInHitstun { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }
        }

        public void SetInvulnerable(float seconds)
        {
            if (health != null) health.SetInvulnerableFor(seconds);
        }

        private void HandleDamaged(DamageInfo info)
        {
            if (animator != null) animator.SetTrigger(hurtTrigger);
            if (HitFeel.Instance != null) HitFeel.Instance.Pulse();
            IsInHitstun = true;
            CancelInvoke(nameof(ClearHitstun));
            Invoke(nameof(ClearHitstun), 0.2f);
        }

        private void ClearHitstun() => IsInHitstun = false;

        private void HandleDied()
        {
            if (animator != null) animator.SetBool(deadBool, true);
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            enabled = false;
        }
    }
}
