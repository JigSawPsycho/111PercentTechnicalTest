using HackSlash.Core;
using UnityEngine;

namespace HackSlash.Abilities
{
    /// <summary>
    /// Short burst of horizontal velocity with i-frames. Direction comes from the input
    /// provider if present (player), else from the owner's facing (enemies).
    /// </summary>
    public class DodgeAbility : Ability
    {
        [SerializeField] private float dodgeSpeed = 14f;
        [SerializeField] private float dodgeDuration = 0.25f;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Health health;

        private IMoveInputProvider inputProvider;

        protected override void Awake()
        {
            base.Awake();
            if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
            if (health == null) health = GetComponentInParent<Health>();
            inputProvider = owner as IMoveInputProvider;
            if (inputProvider == null) inputProvider = GetComponentInParent<IMoveInputProvider>();
        }

        protected override void OnActivate()
        {
            int dir = owner != null ? owner.Facing : 1;
            if (inputProvider != null && Mathf.Abs(inputProvider.MoveInputX) > 0.1f)
                dir = (int)Mathf.Sign(inputProvider.MoveInputX);

            SetActive(dodgeDuration);
            StartCooldown();

            if (health != null) health.SetInvulnerableFor(dodgeDuration);
            if (rb != null) rb.linearVelocity = new Vector2(dir * dodgeSpeed, rb.linearVelocity.y);
        }
    }
}
